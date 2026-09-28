using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AsystentGminy.Desktop.Models;
using AsystentGminy.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsystentGminy.Desktop.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly ChatApiClient _api = new();

    public ObservableCollection<DocumentDto> Documents { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public event Func<DocumentDto, Task<bool>>? ConfirmDeleteRequested;

    public DocumentsViewModel()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Ładowanie...";

        try
        {
            var docs = await _api.GetDocumentsAsync();
            Documents.Clear();
            foreach (var d in docs)
                Documents.Add(d);

            StatusText = $"Załadowano {Documents.Count} dokumentów.";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Błąd: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(DocumentDto? doc)
    {
        if (doc is null) return;

        // Zapytaj o potwierdzenie
        if (ConfirmDeleteRequested is not null)
        {
            var confirmed = await ConfirmDeleteRequested(doc);
            if (!confirmed) return;
        }

        IsLoading = true;
        StatusText = $"Usuwanie: {doc.Title}...";

        try
        {
            await _api.DeleteDocumentAsync(doc.Id);
            Documents.Remove(doc);
            StatusText = $"✅ Usunięto: {doc.Title}";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Błąd: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}