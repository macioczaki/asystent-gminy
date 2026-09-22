using Pgvector;

namespace AsystentGminy.Ingestion.Models;

public class Chunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public Vector Embedding { get; set; } = null!;
}