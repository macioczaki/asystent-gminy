namespace AsystentGminy.Api.Models;

public class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<Chunk> Chunks { get; set; } = new();
}