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

    [ObservableProperty]
    private string _question = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [RelayCommand]
    private async Task AskAsync()
    {
        var q = Question?.Trim();
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
            await foreach (var rawPayload in _api.AskStreamAsync(q))
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
        }
        catch (Exception ex)
        {
            assistantMessage.Content = $"❌ Błąd: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusText = string.Empty;
        }
    }

    private static string StripArtifacts(string text)
    {
        // 1. Usuń znaczniki <|...|>
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text, @"<\|[^|]*\|>", string.Empty);

        // 2. Usuń osierocone słowa-role z początku i końca
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"^\s*(system|user|assistant)\s*", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"\s*(system|user|assistant)\s*$", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 3. Usuń znaczniki Markdown: **bold**, *italic*, `code`, # nagłówki
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\*\*(.+?)\*\*", "$1");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\*(.+?)\*", "$1");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"`(.+?)`", "$1");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"^#{1,6}\s+", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline);

        // 4. Normalizuj spacje i tabulatory, ZACHOWAJ newline
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[ \t]+", " ");

        return cleaned.Trim();
    }

    [RelayCommand]
    private void CopyMessage(string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        CopyToClipboardRequested?.Invoke(content);
    }

    public event Action<string>? CopyToClipboardRequested;
}