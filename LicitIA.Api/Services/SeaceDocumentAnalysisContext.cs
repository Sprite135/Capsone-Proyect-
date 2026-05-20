namespace LicitIA.Api.Services;

public sealed class SeaceDocumentAnalysisContext
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<GeminiInlineDocument> InlineDocuments { get; init; } = Array.Empty<GeminiInlineDocument>();
}

public sealed record GeminiInlineDocument(string Name, string MimeType, byte[] Content);
