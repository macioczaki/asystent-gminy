using System.Text;
using PdfOxide.Core;
using Tesseract;
using PDFtoImage;

namespace AsystentGminy.Ingestion.Services;

public static class PdfReader
{
    private const string TesseractDataPath = @"C:\Program Files\Tesseract-OCR\tessdata";
    private const int MinTextLength = 100;

    public static string ReadText(string path)
    {
        // Szybka ścieżka: próba bezpośredniej ekstrakcji tekstu
        var directText = ExtractDirectText(path);
        if (directText.Trim().Length >= MinTextLength)
            return directText;

        // Wolna ścieżka: OCR
        Console.WriteLine("      [OCR] Wykryto skan – uruchamiam OCR...");
        var ocrText = ExtractTextWithOcr(path);

        if (ocrText.Trim().Length < MinTextLength)
            throw new InvalidOperationException(
                "Nie udało się wyciągnąć tekstu (nawet po OCR).");

        return ocrText;
    }

    private static string ExtractDirectText(string path)
    {
        using var doc = PdfDocument.Open(path);
        return doc.ExtractAllText() ?? string.Empty;
    }

    private static string ExtractTextWithOcr(string path)
    {
        var sb = new StringBuilder();

        // Pobierz liczbę stron przez PdfOxide
        int pageCount;
        using (var doc = PdfDocument.Open(path))
        {
            pageCount = doc.PageCount;
        }

        // Wczytaj PDF raz do pamięci
        var pdfBytes = File.ReadAllBytes(path);

        using var engine = new TesseractEngine(TesseractDataPath, "pol", EngineMode.Default);

        for (int i = 0; i < pageCount; i++)
        {
            using var pdfStream = new MemoryStream(pdfBytes);
            using var pngStream = new MemoryStream();

            // Renderuj stronę do PNG
            Conversion.SavePng(pngStream, pdfStream, page: i);
            pngStream.Position = 0;

            // OCR na obrazie
            using var img = Pix.LoadFromMemory(pngStream.ToArray());
            using var page = engine.Process(img);

            sb.AppendLine(page.GetText());
            Console.Write($"\r      [OCR] Strona {i + 1}/{pageCount}...");
        }

        Console.WriteLine();
        return sb.ToString();
    }
}