using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AsystentGminy.Desktop.Models;

namespace AsystentGminy.Desktop.Services;

public class PdfExportService
{
    public PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void ExportConversation(
        string filePath,
        string title,
        IReadOnlyList<ChatMessage> messages)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                // ============================================================
                // NAGŁÓWEK
                // ============================================================
                page.Header().Column(col =>
                {
                    col.Item().Text("Asystent Gminy")
                        .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);

                    col.Item().Text(title)
                        .FontSize(14).SemiBold();

                    col.Item().PaddingTop(4).Text($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                // ============================================================
                // TREŚĆ
                // ============================================================
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);

                    foreach (var msg in messages)
                    {
                        col.Item().Column(block =>
                        {
                            var roleLabel = msg.IsUser ? "Pytanie" : "Odpowiedź asystenta";
                            var roleColor = msg.IsUser ? Colors.Blue.Darken2 : Colors.Green.Darken2;

                            block.Item().Text(roleLabel)
                                .FontSize(11).Bold().FontColor(roleColor);

                            block.Item().PaddingTop(2).Text(msg.Content)
                                .FontSize(11).LineHeight(1.3f);

                            if (msg.HasSources)
                            {
                                block.Item().PaddingTop(6).Column(srcCol =>
                                {
                                    srcCol.Item().Text("Źródła:")
                                        .FontSize(10).SemiBold().FontColor(Colors.Grey.Darken2);

                                    foreach (var src in msg.Sources)
                                    {
                                        srcCol.Item().PaddingLeft(10).PaddingTop(3).Column(s =>
                                        {
                                            s.Item().Text($"[{src.Index}] {src.DocumentTitle}")
                                                .FontSize(9).SemiBold();

                                            var excerpt = src.Excerpt.Length > 250
                                                ? src.Excerpt.Substring(0, 250) + "..."
                                                : src.Excerpt;
                                            s.Item().Text(excerpt)
                                                .FontSize(9).FontColor(Colors.Grey.Darken2)
                                                .LineHeight(1.2f);
                                        });
                                    }
                                });
                            }
                        });
                    }

                    if (!messages.Any())
                    {
                        col.Item().Text("(rozmowa jest pusta)")
                            .FontSize(11).Italic().FontColor(Colors.Grey.Darken1);
                    }
                });

                // ============================================================
                // STOPKA
                // ============================================================
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Strona ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.Span(" z ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        })
        .GeneratePdf(filePath);
    }
}