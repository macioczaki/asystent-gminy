using System.Security.Cryptography;
using AsystentGminy.Api.Data;
using AsystentGminy.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace AsystentGminy.Api.Services.Ingestion;

public class IngestionService
{
    private readonly AppDbContext _db;
    private readonly OllamaEmbeddingService _embedder;

    public IngestionService(AppDbContext db, OllamaEmbeddingService embedder)
    {
        _db = db;
        _embedder = embedder;
    }

    public async Task IngestFileAsync(string pdfPath, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(pdfPath);
        Console.WriteLine($"\n📄 {fileName}");

        // Sprawdź, czy już zaindeksowany (po checksumie)
        var checksum = ComputeChecksum(pdfPath);
        var existing = await _db.Documents.FirstOrDefaultAsync(d => d.Checksum == checksum, ct);
        if (existing is not null)
        {
            Console.WriteLine($"   ⏭️  Pomijam – już zaindeksowany.");
            return;
        }

        // Czytaj PDF
        string text;
        try
        {
            text = PdfReader.ReadText(pdfPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Pomijam: {ex.Message}");
            return;
        }

        // Podziel na chunki
        var chunks = TextChunker.Chunk(text);
        Console.WriteLine($"   ✂️  Podzielono na {chunks.Count} fragmentów.");

        // Utwórz dokument
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = Path.GetFileNameWithoutExtension(pdfPath),
            SourcePath = pdfPath,
            SourceType = "pdf",
            Checksum = checksum,
            CreatedAt = DateTime.UtcNow
        };
        _db.Documents.Add(document);

        // Generuj embeddingi i zapisuj chunki
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            var embedding = await _embedder.GetEmbeddingAsync(chunkText);

            _db.Chunks.Add(new Chunk
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Content = chunkText,
                ChunkIndex = i,
                Embedding = new Vector(embedding)
            });

            Console.Write($"\r   🧠 Embedding {i + 1}/{chunks.Count}...");
        }

        await _db.SaveChangesAsync(ct);
        Console.WriteLine($"\n   ✅ Zapisano do bazy.");
    }

    private static string ComputeChecksum(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}