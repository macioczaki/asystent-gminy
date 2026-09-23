using AsystentGminy.Api.Data;
using AsystentGminy.Api.Services;
using AsystentGminy.Api.Services.Ingestion;
using Scalar.AspNetCore;

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
app.MapPost("/api/chat/stream", async (ChatRequest request, ChatService chat, HttpContext context, CancellationToken ct) =>
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

    try
    {
        await foreach (var evt in chat.AskStreamAsync(request.Question, topK: 2, ct))
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(evt);
            await context.Response.WriteAsync($"data: {payload}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }

        await context.Response.WriteAsync("data: [DONE]\n\n", ct);
        await context.Response.Body.FlushAsync(ct);
    }
    catch (Exception ex)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { type = "error", message = ex.Message });
        await context.Response.WriteAsync($"data: {payload}\n\n", ct);
    }
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

app.Run();

// ============================================================
// MODELE DTO
// ============================================================
public record ChatRequest(string Question);

public class ChatOptions
{
    public string Model { get; set; } = "bielik-4.5b";
    public string Endpoint { get; set; } = "http://localhost:11434";
}