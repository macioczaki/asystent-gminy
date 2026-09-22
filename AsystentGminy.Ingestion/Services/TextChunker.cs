namespace AsystentGminy.Ingestion.Services;

public static class TextChunker
{
    private const int ChunkSize = 800;
    private const int Overlap = 150;

    public static List<string> Chunk(string text)
    {
        var chunks = new List<string>();
        var cleaned = CleanupWhitespace(text);

        int start = 0;
        while (start < cleaned.Length)
        {
            int end = Math.Min(start + ChunkSize, cleaned.Length);

            // Spróbuj zakończyć chunk na końcu zdania lub akapitu
            if (end < cleaned.Length)
            {
                int lastBreak = cleaned.LastIndexOfAny(
                    new[] { '.', '!', '?', '\n' }, end - 1, Math.Min(200, end - start));
                if (lastBreak > start + 100)
                    end = lastBreak + 1;
            }

            var chunk = cleaned.Substring(start, end - start).Trim();
            if (chunk.Length > 50)
                chunks.Add(chunk);

            if (end >= cleaned.Length) break;
            start = end - Overlap;
        }

        return chunks;
    }

    private static string CleanupWhitespace(string text)
    {
        // Wielokrotne spacje, taby, puste linie → pojedyncze
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }
}