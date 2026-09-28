namespace AsystentGminy.Api.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string Role { get; set; } = string.Empty;   // "user" lub "assistant"
    public string Content { get; set; } = string.Empty;
    public string? SourcesJson { get; set; }           // JSON z listą źródeł
    public DateTime CreatedAt { get; set; }
}