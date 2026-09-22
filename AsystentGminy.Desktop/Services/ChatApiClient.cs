using System;
using System.Net.Http;
using System.Net.Http.Json;
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

    public async Task<ChatResponse> AskAsync(string question)
    {
        var request = new ChatRequest { Question = question };
        var response = await _http.PostAsJsonAsync("/api/chat", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        return result ?? new ChatResponse { Answer = "Brak odpowiedzi." };
    }
}