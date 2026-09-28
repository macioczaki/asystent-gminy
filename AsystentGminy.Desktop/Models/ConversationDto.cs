using System;
using System.Collections.Generic;

namespace AsystentGminy.Desktop.Models;

public class ConversationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MessageCount { get; set; }

    public string UpdatedAtFormatted => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string Summary => $"{MessageCount} wiadomości";
}

public class ConversationDetailsDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ConversationMessageDto> Messages { get; set; } = new();
}

public class ConversationMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SourcesJson { get; set; }
    public DateTime CreatedAt { get; set; }
}