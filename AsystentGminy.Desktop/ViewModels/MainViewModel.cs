using System;
using System.Collections.ObjectModel;
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

        try
        {
            var response = await _api.AskAsync(q);
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response.Answer,
                Sources = response.Sources
            });
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = $"❌ Błąd połączenia z API: {ex.Message}"
            });
        }
        finally
        {
            IsBusy = false;
            StatusText = string.Empty;
        }
    }

    [RelayCommand]
    private void CopyMessage(string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        CopyToClipboardRequested?.Invoke(content);
    }

    public event Action<string>? CopyToClipboardRequested;
}