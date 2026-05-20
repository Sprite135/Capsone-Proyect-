using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace LicitIA.Api.Services;

public sealed class SeaceDocumentTextService
{
    private const int MaxInlineFiles = 2;
    private const int MaxInlineBytesPerFile = 45 * 1024 * 1024;
    private const int MaxSummaryChars = 14000;

    public SeaceDocumentAnalysisContext BuildContext(IEnumerable<SeaceDocumentDownloadResult> documents)
    {
        var summary = new StringBuilder();
        var inlineDocuments = new List<GeminiInlineDocument>();

        foreach (var document in documents)
        {
            if (document.Content.Length == 0)
            {
                continue;
            }

            AppendLine(summary, $"Documento SEACE: {document.FileName} ({document.ContentType}, {document.Content.Length / 1024} KB)");
            var extension = Path.GetExtension(document.FileName).ToLowerInvariant();

            if (IsZip(document, extension))
            {
                ExtractZip(document, summary, inlineDocuments);
                continue;
            }

            AddDocument(document.FileName, document.ContentType, document.Content, summary, inlineDocuments);
        }

        return new SeaceDocumentAnalysisContext
        {
            Summary = Truncate(summary.ToString(), MaxSummaryChars),
            InlineDocuments = inlineDocuments
        };
    }

    private static void ExtractZip(SeaceDocumentDownloadResult zipDocument, StringBuilder summary, List<GeminiInlineDocument> inlineDocuments)
    {
        try
        {
            using var memory = new MemoryStream(zipDocument.Content);
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
            AppendLine(summary, $"ZIP detectado: {zipDocument.FileName}. Archivos internos: {archive.Entries.Count}");

            foreach (var entry in archive.Entries
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .OrderByDescending(item => LooksRelevant(item.Name))
                .ThenBy(item => item.FullName)
                .Take(12))
            {
                AppendLine(summary, $"- {entry.FullName} ({entry.Length / 1024} KB)");

                if (entry.Length <= 0 || entry.Length > MaxInlineBytesPerFile)
                {
                    continue;
                }

                using var entryStream = entry.Open();
                using var copy = new MemoryStream();
                entryStream.CopyTo(copy);
                AddDocument(entry.Name, GuessMimeType(entry.Name), copy.ToArray(), summary, inlineDocuments);
            }
        }
        catch (InvalidDataException)
        {
            AppendLine(summary, "No se pudo descomprimir el ZIP; requiere revision manual.");
        }
    }

    private static void AddDocument(string name, string mimeType, byte[] content, StringBuilder summary, List<GeminiInlineDocument> inlineDocuments)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();

        if (extension is ".txt" or ".csv")
        {
            AppendLine(summary, $"Texto de {name}:");
            AppendLine(summary, DecodeText(content));
            return;
        }

        if (extension == ".docx")
        {
            AppendLine(summary, $"Texto extraido de {name}:");
            AppendLine(summary, ExtractDocxText(content));
            return;
        }

        if (extension == ".xlsx")
        {
            AppendLine(summary, $"Texto extraido de {name}:");
            AppendLine(summary, ExtractXlsxText(content));
            return;
        }

        if ((extension == ".pdf" || mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase)) &&
            content.Length <= MaxInlineBytesPerFile &&
            inlineDocuments.Count < MaxInlineFiles)
        {
            inlineDocuments.Add(new GeminiInlineDocument(name, "application/pdf", content));
            AppendLine(summary, $"PDF adjunto para lectura directa/OCR visual por Gemini: {name}");
            return;
        }

        AppendLine(summary, $"Archivo no enviado a Gemini por tipo/tamano: {name}");
    }

    private static string ExtractDocxText(byte[] content)
    {
        try
        {
            using var memory = new MemoryStream(content);
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
            {
                return "No se encontro word/document.xml en el DOCX.";
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            return Truncate(string.Join(" ", document.DescendantNodes().OfType<XText>().Select(item => item.Value)), MaxSummaryChars);
        }
        catch
        {
            return "No se pudo extraer texto del DOCX.";
        }
    }

    private static string ExtractXlsxText(byte[] content)
    {
        try
        {
            using var memory = new MemoryStream(content);
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
            var text = new StringBuilder();
            foreach (var entry in archive.Entries.Where(item =>
                item.FullName.StartsWith("xl/sharedStrings", StringComparison.OrdinalIgnoreCase) ||
                item.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)).Take(8))
            {
                using var stream = entry.Open();
                var document = XDocument.Load(stream);
                AppendLine(text, string.Join(" ", document.DescendantNodes().OfType<XText>().Select(item => item.Value)));
            }

            return Truncate(text.ToString(), MaxSummaryChars);
        }
        catch
        {
            return "No se pudo extraer texto del XLSX.";
        }
    }

    private static bool IsZip(SeaceDocumentDownloadResult document, string extension) =>
        extension == ".zip" ||
        document.ContentType.Contains("zip", StringComparison.OrdinalIgnoreCase) ||
        (document.Content.Length >= 4 && document.Content[0] == 0x50 && document.Content[1] == 0x4B);

    private static bool LooksRelevant(string fileName)
    {
        var normalized = fileName.ToLowerInvariant();
        return normalized.Contains("base") ||
            normalized.Contains("tdr") ||
            normalized.Contains("termino") ||
            normalized.Contains("requer") ||
            normalized.Contains("especific") ||
            normalized.Contains("anexo");
    }

    private static string GuessMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };

    private static string DecodeText(byte[] content)
    {
        try
        {
            return Truncate(Encoding.UTF8.GetString(content), MaxSummaryChars);
        }
        catch
        {
            return "No se pudo decodificar el texto.";
        }
    }

    private static void AppendLine(StringBuilder builder, string value)
    {
        if (builder.Length >= MaxSummaryChars)
        {
            return;
        }

        builder.AppendLine(Truncate(value, Math.Max(0, MaxSummaryChars - builder.Length)));
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars];
}
