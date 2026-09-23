using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AsystentGminy.Desktop.Models;

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceReference> Sources { get; set; } = new();
}

public class SourceReference
{
    public int Index { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public double Distance { get; set; }
}

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSources))]
    private List<SourceReference> _sources = new();

    public bool IsUser => Role == "user";
    public bool HasSources => Sources.Count > 0;
}