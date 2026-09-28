using AsystentGminy.Api.Data;
using AsystentGminy.Api.Services;
using AsystentGminy.Api.Services.Ingestion;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using AsystentGminy.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// KONFIGURACJA
// ============================================================
var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"] 
    ?? "http://localhost:11434";
var chatModel = builder.Configuration["Ollama:ChatModel"] ?? "bielik-4.5b";

// Rejestracja usług
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddScoped<AppDbContext>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<IngestionService>();

// ChatService z HttpClientem o wydłużonym timeoutcie
builder.Services.AddHttpClient<ChatService>(client =>
{
    client.BaseAddress = new Uri(ollamaEndpoint);
    client.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddSingleton(new ChatOptions
{
    Model = chatModel,
    Endpoint = ollamaEndpoint
});

// OpenAPI + Scalar
builder.Services.AddOpenApi();

var app = builder.Build();

// ============================================================
// ENDPOINTY
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Endpoint klasyczny (bez streamingu)
app.MapPost("/api/chat", async (ChatRequest request, ChatService chat) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Pytanie nie może być puste." });

    try
    {
        var response = await chat.AskAsync(request.Question, topK: 2);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "Błąd generowania odpowiedzi",
            statusCode: 500);
    }
});

// Endpoint streamingowy (SSE)
app.MapPost("/api/chat/stream", async (ChatRequest request, ChatService chat, AppDbContext db, HttpContext context, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "Pytanie nie może być puste." }, ct);
        return;
    }

    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    // Zapisz wiadomość użytkownika od razu (jeśli mamy rozmowę)
    Conversation? conversation = null;
    if (request.ConversationId.HasValue)
    {
        conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value, ct);

        if (conversation is not null)
        {
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = "user",
                Content = request.Question,
                CreatedAt = DateTime.UtcNow
            });
            conversation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    var buffer = new System.Text.StringBuilder();
    string? sourcesJson = null;

    try
    {
        await foreach (var evt in chat.AskStreamAsync(request.Question, topK: 2, ct))
        {
            var type = evt.GetType();

            // Zbierz źródła do zapisu
            if (type.GetProperty("type")?.GetValue(evt)?.ToString() == "sources")
            {
                var sources = type.GetProperty("sources")?.GetValue(evt);
                sourcesJson = System.Text.Json.JsonSerializer.Serialize(sources);
            }

            if (type.GetProperty("type")?.GetValue(evt)?.ToString() == "chunk")
            {
                var content = type.GetProperty("content")?.GetValue(evt)?.ToString();
                if (!string.IsNullOrEmpty(content))
                    buffer.Append(content);
            }

            var payload = System.Text.Json.JsonSerializer.Serialize(evt);
            await context.Response.WriteAsync($"data: {payload}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }

        await context.Response.WriteAsync("data: [DONE]\n\n", ct);
        await context.Response.Body.FlushAsync(ct);

        // Zapisz odpowiedź asystenta po zakończeniu streamu
        if (conversation is not null)
        {
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = "assistant",
                Content = buffer.ToString(),
                SourcesJson = sourcesJson,
                CreatedAt = DateTime.UtcNow
            });
            conversation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
    catch (Exception ex)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { type = "error", message = ex.Message });
        await context.Response.WriteAsync($"data: {payload}\n\n", ct);
    }
});

// Lista dokumentów
app.MapGet("/api/documents", async (AppDbContext db) =>
{
    var documents = await db.Documents
        .OrderByDescending(d => d.CreatedAt)
        .Select(d => new
        {
            id = d.Id,
            title = d.Title,
            sourcePath = d.SourcePath,
            sourceType = d.SourceType,
            createdAt = d.CreatedAt,
            chunkCount = d.Chunks.Count
        })
        .ToListAsync();

    return Results.Ok(documents);
});

// Usuń dokument (razem z jego chunkami)
app.MapDelete("/api/documents/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var doc = await db.Documents
        .Include(d => d.Chunks)
        .FirstOrDefaultAsync(d => d.Id == id);

    if (doc is null)
        return Results.NotFound(new { error = "Dokument nie istnieje." });

    db.Documents.Remove(doc);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Dokument usunięty." });
});

// Endpoint uploadu dokumentów
app.MapPost("/api/documents/upload", async (
    IFormFile file,
    IngestionService ingestion,
    CancellationToken ct) =>
{
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "Brak pliku." });

    if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Obsługiwane są tylko pliki PDF." });

    var tempPath = Path.Combine(Path.GetTempPath(), $"upload_{Guid.NewGuid()}.pdf");
    try
    {
        await using (var stream = File.Create(tempPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        await ingestion.IngestFileAsync(tempPath, ct);

        return Results.Ok(new
        {
            message = "Dokument zaindeksowany.",
            fileName = file.FileName
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "Błąd indeksowania dokumentu",
            statusCode: 500);
    }
    finally
    {
        if (File.Exists(tempPath))
            File.Delete(tempPath);
    }
})
.DisableAntiforgery();

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// ============================================================
// ROZMOWY (HISTORIA)
// ============================================================

// Lista rozmów (bez wiadomości)
app.MapGet("/api/conversations", async (AppDbContext db) =>
{
    var conversations = await db.Conversations
        .OrderByDescending(c => c.UpdatedAt)
        .Select(c => new
        {
            id = c.Id,
            title = c.Title,
            createdAt = c.CreatedAt,
            updatedAt = c.UpdatedAt,
            messageCount = c.Messages.Count
        })
        .ToListAsync();

    return Results.Ok(conversations);
});

// Utwórz nową rozmowę
app.MapPost("/api/conversations", async (CreateConversationRequest request, AppDbContext db) =>
{
    var title = string.IsNullOrWhiteSpace(request.Title) ? "Nowa rozmowa" : request.Title;

    var conversation = new Conversation
    {
        Id = Guid.NewGuid(),
        Title = title.Length > 100 ? title.Substring(0, 100) : title,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.Conversations.Add(conversation);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        id = conversation.Id,
        title = conversation.Title,
        createdAt = conversation.CreatedAt
    });
});

// Pobierz rozmowę z wiadomościami
app.MapGet("/api/conversations/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var conversation = await db.Conversations
        .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
        .FirstOrDefaultAsync(c => c.Id == id);

    if (conversation is null)
        return Results.NotFound(new { error = "Rozmowa nie istnieje." });

    return Results.Ok(new
    {
        id = conversation.Id,
        title = conversation.Title,
        createdAt = conversation.CreatedAt,
        updatedAt = conversation.UpdatedAt,
        messages = conversation.Messages.Select(m => new
        {
            id = m.Id,
            role = m.Role,
            content = m.Content,
            sourcesJson = m.SourcesJson,
            createdAt = m.CreatedAt
        })
    });
});

// Usuń rozmowę
app.MapDelete("/api/conversations/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var conversation = await db.Conversations
        .Include(c => c.Messages)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (conversation is null)
        return Results.NotFound(new { error = "Rozmowa nie istnieje." });

    db.Conversations.Remove(conversation);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Rozmowa usunięta." });
});

app.Run();

// ============================================================
// MODELE DTO
// ============================================================
public record ChatRequest(string Question, Guid? ConversationId = null);

public record CreateConversationRequest(string Title);

public class ChatOptions
{
    public string Model { get; set; } = "bielik-4.5b";
    public string Endpoint { get; set; } = "http://localhost:11434";
}