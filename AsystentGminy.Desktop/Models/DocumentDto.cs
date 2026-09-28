using System;

namespace AsystentGminy.Desktop.Models;

public class DocumentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ChunkCount { get; set; }

    public string CreatedAtFormatted => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string ChunkCountText => $"{ChunkCount} fragmentów";
}