using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsystentGminy.Desktop.Models;

namespace AsystentGminy.Desktop.Services;

public class ChatApiClient
{
    private readonly HttpClient _http;

    public ChatApiClient()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5234"),
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    // ────────────────────────────────────────────────────────────
    // Metoda klasyczna (bez streamingu) – zwraca całą odpowiedź naraz
    // ────────────────────────────────────────────────────────────
    public async Task<ChatResponse> AskAsync(string question)
    {
        var request = new ChatRequest { Question = question };
        var response = await _http.PostAsJsonAsync("/api/chat", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        return result ?? new ChatResponse { Answer = "Brak odpowiedzi." };
    }

    // ────────────────────────────────────────────────────────────
    // Metoda streamingowa – zwraca fragmenty odpowiedzi po kolei
    // ────────────────────────────────────────────────────────────
    public async IAsyncEnumerable<string> AskStreamAsync(
        string question,
        Guid? conversationId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new ChatRequest 
        { 
            Question = question, 
            ConversationId = conversationId 
        };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = content
        };

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var payload = line.Substring(6).Trim();
            if (payload == "[DONE]") yield break;

            // Zwracamy surowy payload – parsowanie w ViewModel
            yield return payload;
        }
    }
    public async Task<string> UploadDocumentAsync(string filePath, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(filePath);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await _http.PostAsync("/api/documents/upload", form, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"API zwróciło {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>(cancellationToken: ct);
        return result?.Message ?? "Dokument zaindeksowany.";
    }
    public async Task<List<DocumentDto>> GetDocumentsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/documents", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<DocumentDto>>(
            cancellationToken: ct);

        return result ?? new List<DocumentDto>();
    }

    public async Task DeleteDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/documents/{id}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"API zwróciło {(int)response.StatusCode}: {errorBody}");
        }
    }

    public async Task<List<ConversationDto>> GetConversationsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/conversations", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<ConversationDto>>(
            cancellationToken: ct);

        return result ?? new List<ConversationDto>();
    }

    public async Task<Guid> CreateConversationAsync(string title, CancellationToken ct = default)
    {
        var request = new { title };
        var response = await _http.PostAsJsonAsync("/api/conversations", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateConversationResponse>(
            cancellationToken: ct);

        if (result is null)
            throw new Exception("API nie zwróciło ID rozmowy.");

        return result.Id;
    }

    public async Task<ConversationDetailsDto?> GetConversationAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/conversations/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ConversationDetailsDto>(
            cancellationToken: ct);
    }

    public async Task DeleteConversationAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/conversations/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    private class CreateConversationResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public Guid Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    private class UploadResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("fileName")]
        public string? FileName { get; set; }
    }
}