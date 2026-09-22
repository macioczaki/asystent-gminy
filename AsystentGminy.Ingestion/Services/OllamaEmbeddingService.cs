using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AsystentGminy.Ingestion.Services;

public class OllamaEmbeddingService
{
    private readonly HttpClient _http;
    private const string Model = "nomic-embed-text-v2-moe";

    public OllamaEmbeddingService(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri("http://localhost:11434");
        _http.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var request = new { model = Model, prompt = text };
        var response = await _http.PostAsJsonAsync("/api/embeddings", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        if (result?.Embedding is null || result.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama zwróciła pusty embedding.");

        return result.Embedding;
    }

    private class OllamaResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}