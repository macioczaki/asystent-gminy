using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using AsystentGminy.Desktop.Models;
using AsystentGminy.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsystentGminy.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ChatApiClient _api = new();

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<ConversationDto> Conversations { get; } = new();

    [ObservableProperty]
    private string _question = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _uploadStatus = string.Empty;

    [ObservableProperty]
    private Guid? _currentConversationId;

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    public event Action<string>? CopyToClipboardRequested;
    public event Action? PickFileRequested;

    public MainViewModel()
    {
        _ = LoadConversationsAsync();
    }

    // ============================================================
    // ROZMOWY
    // ============================================================
    [RelayCommand]
    public async Task LoadConversationsAsync()
    {
        try
        {
            var list = await _api.GetConversationsAsync();
            Conversations.Clear();
            foreach (var c in list)
                Conversations.Add(c);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadConversations] {ex}");
        }
    }

    [RelayCommand]
    private void NewConversation()
    {
        CurrentConversationId = null;
        Messages.Clear();
    }

    [RelayCommand]
    private async Task OpenConversation(ConversationDto? conversation)
    {
        System.Diagnostics.Debug.WriteLine($"[OpenConversation] START: {conversation?.Title ?? "NULL"}");

        if (conversation is null) return;
        if (IsBusy) return;

        try
        {
            var details = await _api.GetConversationAsync(conversation.Id);
            if (details is null) return;

            CurrentConversationId = details.Id;
            Messages.Clear();

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (var msg in details.Messages)
            {
                var chatMessage = new ChatMessage
                {
                    Role = msg.Role,
                    Content = StripArtifacts(msg.Content)
                };

                if (!string.IsNullOrEmpty(msg.SourcesJson))
                {
                    try
                    {
                        var sources = JsonSerializer.Deserialize<List<SourceReference>>(
                            msg.SourcesJson, opts);
                        chatMessage.Sources = sources ?? new();
                    }
                    catch { }
                }

                Messages.Add(chatMessage);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenConversation ERROR] {ex}");
            StatusText = $"❌ Błąd: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteConversation(ConversationDto? conversation)
    {
        if (conversation is null) return;

        try
        {
            await _api.DeleteConversationAsync(conversation.Id);
            Conversations.Remove(conversation);

            if (CurrentConversationId == conversation.Id)
            {
                CurrentConversationId = null;
                Messages.Clear();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Błąd: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    // ============================================================
    // CZAT
    // ============================================================
    [RelayCommand]
    private async Task Ask()
    {
        var q = Question?.Trim();
        System.Diagnostics.Debug.WriteLine($"[Ask] START: '{q}', IsBusy={IsBusy}, ConvId={CurrentConversationId}");

        if (string.IsNullOrEmpty(q) || IsBusy) return;

        Messages.Add(new ChatMessage { Role = "user", Content = q });
        Question = string.Empty;
        IsBusy = true;
        StatusText = "Asystent analizuje dokumenty...";

        var assistantMessage = new ChatMessage { Role = "assistant", Content = "" };
        Messages.Add(assistantMessage);

        var buffer = new System.Text.StringBuilder();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            if (CurrentConversationId is null)
            {
                var title = q.Length > 60 ? q.Substring(0, 60) + "..." : q;
                System.Diagnostics.Debug.WriteLine($"[Ask] Tworzę nową rozmowę: {title}");
                CurrentConversationId = await _api.CreateConversationAsync(title);
                System.Diagnostics.Debug.WriteLine($"[Ask] Nowa rozmowa ID: {CurrentConversationId}");
            }

            await foreach (var rawPayload in _api.AskStreamAsync(q, CurrentConversationId))
            {
                using var doc = JsonDocument.Parse(rawPayload);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeEl)) continue;
                var type = typeEl.GetString();

                if (type == "sources" && root.TryGetProperty("sources", out var sourcesEl))
                {
                    var sources = JsonSerializer.Deserialize<List<SourceReference>>(
                        sourcesEl.GetRawText(), opts);
                    assistantMessage.Sources = sources ?? new();
                }
                else if (type == "chunk" && root.TryGetProperty("content", out var contentEl))
                {
                    var chunk = contentEl.GetString() ?? "";
                    buffer.Append(chunk);
                    assistantMessage.Content = StripArtifacts(buffer.ToString());
                }
                else if (type == "error" && root.TryGetProperty("message", out var msgEl))
                {
                    assistantMessage.Content = $"❌ Błąd: {msgEl.GetString()}";
                }
            }

            await LoadConversationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ask ERROR] {ex}");
            assistantMessage.Content = $"❌ Błąd: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusText = string.Empty;
        }
    }

    // ============================================================
    // UPLOAD
    // ============================================================
    [RelayCommand]
    private void PickAndUploadFile()
    {
        PickFileRequested?.Invoke();
    }

    public async Task UploadFileAsync(string filePath)
    {
        if (IsBusy) return;
        IsBusy = true;
        UploadStatus = $"⏳ Indeksuję: {System.IO.Path.GetFileName(filePath)}...";

        try
        {
            var message = await _api.UploadDocumentAsync(filePath);
            UploadStatus = $"✅ {message}";
        }
        catch (Exception ex)
        {
            UploadStatus = $"❌ Błąd: {ex.Message}";
        }
        finally
        {
            IsBusy = false;

            _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UploadStatus = string.Empty);
            });
        }
    }

    // ============================================================
    // KOPIOWANIE
    // ============================================================
    [RelayCommand]
    private void CopyMessage(string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        CopyToClipboardRequested?.Invoke(content);
    }

    // ============================================================
    // CZYSZCZENIE
    // ============================================================
    private static string StripArtifacts(string text)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text, @"<\|[^|]*\|>", string.Empty);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"^\s*(system|user|assistant)\s*", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"\s*(system|user|assistant)\s*$", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\*\*(.+?)\*\*", "$1");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\*(.+?)\*", "$1");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"`(.+?)`", "$1");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"^#{1,6}\s+", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[ \t]+", " ");

        return cleaned.Trim();
    }
}