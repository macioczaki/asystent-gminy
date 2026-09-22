using System.Collections.Generic;

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

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;   // "user" lub "assistant"
    public string Content { get; set; } = string.Empty;
    public List<SourceReference> Sources { get; set; } = new();

    public bool IsUser => Role == "user";
    public bool HasSources => Sources.Count > 0;
}