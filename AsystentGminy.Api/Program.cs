using AsystentGminy.Api.Data;
using AsystentGminy.Api.Services;
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