using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AsystentGminy.Desktop.ViewModels;

namespace AsystentGminy.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        vm.CopyToClipboardRequested += async content =>
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(content);
        };

        vm.PickFileRequested += async () => await PickAndUploadAsync(vm);
        vm.SaveFileRequested += async (suggestedName) => await PickSaveLocationAsync(suggestedName);

        vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    private async Task PickAndUploadAsync(MainViewModel vm)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Wybierz dokument PDF",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Pliki PDF")
                {
                    Patterns = new[] { "*.pdf" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        await vm.UploadFileAsync(path);
    }

    private async Task<string?> PickSaveLocationAsync(string suggestedName)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Zapisz rozmowę jako PDF",
            SuggestedFileName = suggestedName,
            DefaultExtension = "pdf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Dokument PDF")
                {
                    Patterns = new[] { "*.pdf" }
                }
            }
        });

        if (file is null) return null;
        return file.TryGetLocalPath();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
            scrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private void OpenDocumentsClick(object? sender, RoutedEventArgs e)
    {
        var window = new DocumentsWindow
        {
            DataContext = new DocumentsViewModel()
        };
        window.ShowDialog(this);
    }
}