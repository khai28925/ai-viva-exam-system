using AiViva.Application.Modules.QuestionPreparation.Wf1;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;

public sealed class PdfPigTextExtractor : IDocumentTextExtractor
{
    public IReadOnlyList<string> ExtractPages(byte[] content)
    {
        using var document = PdfDocument.Open(content);
        return document.GetPages().Select(page =>
        {
            // Reading order matters for multi-column syllabi; fall back to raw text on odd layouts.
            try { return ContentOrderTextExtractor.GetText(page); }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            { return page.Text; }
        }).ToArray();
    }
}
