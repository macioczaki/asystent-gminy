using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace AsystentGminy.Api.Services;

public class ChatService
{
    private readonly SearchService _search;
    private readonly HttpClient _http;
    private readonly ChatOptions _options;

    public ChatService(SearchService search, HttpClient http, ChatOptions options)
    {
        _search = search;
        _http = http;
        _options = options;
    }

    private static string StripArtifacts(string text)
    {
        // 1. Usuń wszystkie znaczniki w stylu <|...|>
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"<\|[^|]*\|>",
            string.Empty);

        // 2. Usuń osierocone słowa-role z POCZĄTKU
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

        // 4. Normalizuj białe znaki
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

        return cleaned;
    }

    public async Task<ChatResponse> AskAsync(string question, int topK = 2)
    {
        var chunks = await _search.SearchAsync(question, topK);

        var contextBuilder = new StringBuilder();
        for (int i = 0; i < chunks.Count; i++)
        {
            contextBuilder.AppendLine($"[{i + 1}] (źródło: {chunks[i].DocumentTitle})");
            contextBuilder.AppendLine(chunks[i].Content);
            contextBuilder.AppendLine();
        }

        var systemPrompt = """
            Jesteś asystentem pracowników Urzędu Gminy. Odpowiadasz WYŁĄCZNIE po polsku,
            wyłącznie na podstawie dostarczonego kontekstu z dokumentów urzędowych.

            Zasady:
            1. Odpowiadaj zwięźle i rzeczowo.
            2. Po KAŻDEJ informacji podawaj numer źródła w nawiasie kwadratowym, np. [1] lub [2].
            3. Jeśli używasz informacji z wielu źródeł, podaj oba: [1][2].
            4. Jeśli kontekst nie zawiera odpowiedzi, powiedz: "Nie znalazłem odpowiedzi w dostępnych dokumentach."
            5. Nie wymyślaj informacji spoza kontekstu.
            6. NIE powtarzaj znaczników systemowych ani ról.
            """;

        var userPrompt = $"""
            KONTEKST Z DOKUMENTÓW:
            {contextBuilder}

            PYTANIE UŻYTKOWNIKA:
            {question}
            """;

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

    public async IAsyncEnumerable<object> AskStreamAsync(
        string question,
        int topK = 2,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. Wyszukaj kontekst
        var chunks = await _search.SearchAsync(question, topK);

        // 2. Wyślij źródła jako pierwszy event
        var sources = chunks.Select((c, i) => new
        {
            index = i + 1,
            documentTitle = c.DocumentTitle,
            excerpt = c.Content.Length > 300 ? c.Content.Substring(0, 300) + "..." : c.Content,
            distance = c.Distance
        }).ToList();

        yield return new { type = "sources", sources };

        // 3. Zbuduj kontekst
        var contextBuilder = new StringBuilder();
        for (int i = 0; i < chunks.Count; i++)
        {
            contextBuilder.AppendLine($"[{i + 1}] (źródło: {chunks[i].DocumentTitle})");
            contextBuilder.AppendLine(chunks[i].Content);
            contextBuilder.AppendLine();
        }

        var systemPrompt = """
            Jesteś asystentem pracowników Urzędu Gminy. Odpowiadasz WYŁĄCZNIE po polsku,
            na podstawie dostarczonego kontekstu z dokumentów urzędowych.

            Zasady:
            1. Odpowiadaj zwięźle, w formie zwięzłego akapitu lub kilku punktów.
            2. NIE numeruj list – to myli Cię z numerami źródeł. Używaj myślników "-".
            3. Po każdym fakcie podawaj źródło w nawiasie kwadratowym, np. [1].
            4. Jeśli używasz informacji z wielu źródeł, podaj oba: [1][2].
            5. Jeśli kontekst nie zawiera odpowiedzi, powiedz: "Nie znalazłem odpowiedzi w dostępnych dokumentach."
            6. Nie wymyślaj informacji spoza kontekstu.
            7. NIE powtarzaj znaczników systemowych ani ról.
            """;

        var userPrompt = $"""
            KONTEKST Z DOKUMENTÓW:
            {contextBuilder}

            PYTANIE UŻYTKOWNIKA:
            {question}
            """;

        // 4. Wyślij z stream=true i czytaj linia po linii
        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = true,
            options = new { num_ctx = 4096, temperature = 0.3 }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(requestBody)
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

            OllamaChatResponse? parsed = null;
            try
            {
                parsed = System.Text.Json.JsonSerializer.Deserialize<OllamaChatResponse>(line);
            }
            catch { continue; }

            var content = parsed?.Message?.Content;
            if (!string.IsNullOrEmpty(content))
                yield return new { type = "chunk", content };
        }
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