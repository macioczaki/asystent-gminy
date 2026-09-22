using AsystentGminy.Api.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AsystentGminy.Api.Services;

public class SearchService
{
    private readonly AppDbContext _db;
    private readonly OllamaEmbeddingService _embedder;

    public SearchService(AppDbContext db, OllamaEmbeddingService embedder)
    {
        _db = db;
        _embedder = embedder;
    }

    public async Task<List<SearchResult>> SearchAsync(string question, int topK = 5)
    {
        var embedding = await _embedder.GetEmbeddingAsync(question);
        var queryVector = new Vector(embedding);

        var results = await _db.Chunks
            .OrderBy(c => c.Embedding.CosineDistance(queryVector))
            .Take(topK)
            .Select(c => new SearchResult
            {
                DocumentTitle = c.Document.Title,
                Content = c.Content,
                Distance = c.Embedding.CosineDistance(queryVector)
            })
            .ToListAsync();

        return results;
    }
}

public class SearchResult
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Distance { get; set; }
}