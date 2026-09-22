using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace AsystentGminy.Api.Services;

public class ChatService
{
    private readonly SearchService _search;
    private readonly HttpClient _http;
    private readonly ChatOptions _options;
    private static string StripArtifacts(string text)
    {
        // 1. Usuń wszystkie znaczniki w stylu <|...|>
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"<\|[^|]*\|>",
            string.Empty);

        // 2. Usuń osierocone słowa-role z POCZĄTKU (system, user, assistant)
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^\s*(system|user|assistant)\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 3. Usuń osierocone słowa-role z KOŃCA
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\s*(system|user|assistant)\s*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 4. Normalizuj białe znaki (wiele spacji → jedna)
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

        return cleaned;
    }

    public ChatService(SearchService search, HttpClient http, ChatOptions options)
    {
        _search = search;
        _http = http;
        _options = options;
    }

    public async Task<ChatResponse> AskAsync(string question, int topK = 2)
    {
        // 1. Wyszukaj kontekst w pgvector
        var chunks = await _search.SearchAsync(question, topK);

        // 2. Zbuduj kontekst z numerowanymi źródłami
        var contextBuilder = new StringBuilder();
        for (int i = 0; i < chunks.Count; i++)
        {
            contextBuilder.AppendLine($"[{i + 1}] (źródło: {chunks[i].DocumentTitle})");
            contextBuilder.AppendLine(chunks[i].Content);
            contextBuilder.AppendLine();
        }

        // 3. Prompt systemowy
        var systemPrompt = """
            Jesteś asystentem pracowników Urzędu Gminy. Odpowiadasz WYŁĄCZNIE po polsku,
            wyłącznie na podstawie dostarczonego kontekstu z dokumentów urzędowych.

            Zasady:
            1. Odpowiadaj zwięźle i rzeczowo.
            2. Po KAŻDEJ informacji podawaj numer źródła w nawiasie kwadratowym, np. [1] lub [2].
            3. Jeśli używasz informacji z wielu źródeł, podaj oba: [1][2].
            4. Jeśli kontekst nie zawiera odpowiedzi, powiedz: "Nie znalazłem odpowiedzi w dostępnych dokumentach."
            5. Nie wymyślaj informacji spoza kontekstu.
            6. NIE powtarzaj znaczników systemowych ani ról (np. <|start_header_id|>).
            
            Przykład poprawnej odpowiedzi:
            "Zadania wójta obejmują kierowanie bieżącymi sprawami gminy [1] oraz wydawanie 
            decyzji administracyjnych [1][2]."
            """;

        var userPrompt = $"""
            KONTEKST Z DOKUMENTÓW:
            {contextBuilder}

            PYTANIE UŻYTKOWNIKA:
            {question}
            """;

        // 4. Wywołaj Ollamę przez HTTP
        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            options = new { num_ctx = 4096, temperature = 0.3 }
        };

        var httpResponse = await _http.PostAsJsonAsync("/api/chat", requestBody);
        httpResponse.EnsureSuccessStatusCode();

        var ollamaResponse = await httpResponse.Content.ReadFromJsonAsync<OllamaChatResponse>();
        var rawAnswer = ollamaResponse?.Message?.Content ?? "Brak odpowiedzi od modelu.";
        var answer = StripArtifacts(rawAnswer);

        // 5. Zwróć odpowiedź + źródła
        return new ChatResponse
        {
            Answer = answer,
            Sources = chunks.Select((c, i) => new SourceReference
            {
                Index = i + 1,
                DocumentTitle = c.DocumentTitle,
                Excerpt = c.Content.Length > 300
                    ? c.Content.Substring(0, 300) + "..."
                    : c.Content,
                Distance = c.Distance
            }).ToList()
        };
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceReference> Sources { get; set; } = new();
}

public class SourceReference
{
    public int Index { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public double Distance { get; set; }
}