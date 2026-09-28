using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AsystentGminy.Desktop.Models;
using AsystentGminy.Desktop.ViewModels;

namespace AsystentGminy.Desktop.Views;

public partial class DocumentsWindow : Window
{
    public DocumentsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is not DocumentsViewModel vm) return;
        vm.ConfirmDeleteRequested += ConfirmDeleteAsync;
    }

    private async Task<bool> ConfirmDeleteAsync(DocumentDto doc)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new Window
        {
            Title = "Potwierdź usunięcie",
            Width = 450,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var yesBtn = new Button { Content = "Usuń", Padding = new Avalonia.Thickness(16, 6) };
        var noBtn = new Button { Content = "Anuluj", Padding = new Avalonia.Thickness(16, 6) };

        yesBtn.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        noBtn.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Czy na pewno usunąć dokument:\n\n„{doc.Title}”?",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { noBtn, yesBtn }
                }
            }
        };

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }
}