using AsystentGminy.Ingestion.Data;
using AsystentGminy.Ingestion.Services;

// ============================================================
// KONFIGURACJA
// ============================================================
var knowledgeBase = @"C:\Projekty\AsystentGminy\knowledge_base";
var runIngestion = args.Contains("--ingest") || args.Length == 0;
var runSearch = args.Contains("--search") || args.Length == 0;
var searchQuestion = "Jakie zadania realizuje wójt gminy?";

using var http = new HttpClient();
var embedder = new OllamaEmbeddingService(http);
using var db = new AppDbContext();

// ============================================================
// ETAP 1: INGESTION (OCR dla skanów)
// ============================================================
if (runIngestion)
{
    Console.WriteLine("═══ ETAP 1: INGESTION ═══\n");

    var pliki = Directory.GetFiles(knowledgeBase, "*.pdf");
    Console.WriteLine($"Znaleziono {pliki.Length} plików PDF.\n");

    var ingestion = new IngestionService(db, embedder);

    foreach (var plik in pliki)
    {
        try
        {
            await ingestion.IngestFileAsync(plik);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Błąd przy {Path.GetFileName(plik)}: {ex.Message}");
        }
    }

    Console.WriteLine($"\n🎉 Zakończono ingestion.");
    Console.WriteLine($"Dokumenty w bazie: {db.Documents.Count()}");
    Console.WriteLine($"Fragmenty w bazie: {db.Chunks.Count()}");
}

// ============================================================
// ETAP 2: TEST WYSZUKIWANIA
// ============================================================
if (runSearch)
{
    Console.WriteLine("\n═══ ETAP 2: TEST WYSZUKIWANIA ═══");

    var search = new SearchService(db, embedder);

    Console.WriteLine($"\n❓ {searchQuestion}");
    Console.WriteLine(new string('─', 80));

    var wyniki = await search.SearchAsync(searchQuestion, topK: 3);

    foreach (var w in wyniki)
    {
        var odleglosc = w.Distance.ToString("F3");
        var podglad = w.Content.Length > 250
            ? w.Content.Substring(0, 250) + "..."
            : w.Content;
        podglad = podglad.Replace("\n", " ");

        Console.WriteLine($"\n📄 [{w.DocumentTitle}] (odległość: {odleglosc})");
        Console.WriteLine($"   {podglad}");
    }
}