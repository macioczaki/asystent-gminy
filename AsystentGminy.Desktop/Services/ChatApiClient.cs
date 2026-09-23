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
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new ChatRequest { Question = question };
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
}