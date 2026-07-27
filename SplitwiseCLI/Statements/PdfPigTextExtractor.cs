using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SplitwiseCLI.Statements;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    // ContentOrderTextExtractor is used instead of the raw Page.Text property -
    // Page.Text concatenates words without reliable line breaks, which would
    // break every statement parser's line-anchored regex below.
    public string ExtractText(string filePath)
    {
        using var pdf = PdfDocument.Open(filePath);
        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(ContentOrderTextExtractor.GetText(page));
        }

        return sb.ToString();
    }
}
