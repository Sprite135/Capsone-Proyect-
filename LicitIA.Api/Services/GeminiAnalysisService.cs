using System.Net.Http.Json;
using System.Text.Json;
using LicitIA.Api.Configuration;
using LicitIA.Api.Models;
using Microsoft.Extensions.Options;

namespace LicitIA.Api.Services;

public sealed class GeminiAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiAnalysisService(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public string Model => string.IsNullOrWhiteSpace(_options.Model)
        ? "gemini-2.5-flash-lite"
        : _options.Model;

    public int DailyLimitPerUser => Math.Max(1, _options.DailyLimitPerUser);

    public async Task<OpportunityAiAnalysis> AnalyzeAsync(
        Guid userId,
        Opportunity opportunity,
        RecommendationAnalysis recommendation,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gemini no esta configurado. Agrega Gemini:ApiKey en appsettings o variable de entorno.");
        }

        var prompt = BuildPrompt(opportunity, recommendation);
        var request = new GeminiRequest(new[]
        {
            new GeminiContent(new[]
            {
                new GeminiPart(prompt)
            })
        });

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(Model)}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
        using var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini respondio {((int)response.StatusCode)}: {body}");
        }

        var text = ExtractText(body);
        var parsed = ParseAnalysis(text);

        return new OpportunityAiAnalysis
        {
            UserId = userId,
            OpportunityId = opportunity.OpportunityId,
            ModelName = Model,
            Recommendation = parsed.Recommendation,
            Summary = parsed.Summary,
            Risks = parsed.Risks,
            Requirements = parsed.Requirements,
            NextSteps = parsed.NextSteps,
            RawResponse = text
        };
    }

    private static string BuildPrompt(Opportunity opportunity, RecommendationAnalysis recommendation) =>
        $$"""
        Eres un analista peruano de licitaciones publicas. Analiza la oportunidad usando solo los datos entregados.
        Devuelve exclusivamente JSON valido, sin markdown, con estas propiedades:
        recommendation, summary, risks, requirements, nextSteps.

        Datos:
        Codigo: {{opportunity.ProcessCode}}
        Titulo: {{opportunity.Title}}
        Entidad: {{opportunity.EntityName}}
        Categoria: {{opportunity.Category}}
        Modalidad: {{opportunity.Modality}}
        Monto estimado: {{opportunity.EstimatedAmount}}
        Fecha de cierre: {{opportunity.ClosingDate:yyyy-MM-dd HH:mm}}
        Ubicacion: {{opportunity.Location}}
        Descripcion: {{opportunity.Summary}}

        Afinidad local: {{recommendation.Score}}%
        Etiqueta local: {{recommendation.Label}}
        Motivo local: {{recommendation.Reason}}
        Keywords encontradas: {{string.Join(", ", recommendation.MatchedKeywords)}}

        Reglas:
        - Escribe en espanol claro y breve.
        - No inventes requisitos que no esten en la descripcion.
        - Si faltan bases o documentos, indicalo como riesgo o pendiente.
        - recommendation debe ser una frase corta: "Postular", "Revisar antes de postular" o "Descartar por ahora".
        """;

    private static string ExtractText(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return body;
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        return parts.EnumerateArray()
            .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Aggregate(string.Empty, (current, next) => current + next);
    }

    private static ParsedAiAnalysis ParseAnalysis(string text)
    {
        var json = StripCodeFence(text);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new ParsedAiAnalysis(
                ReadString(root, "recommendation", "Revisar antes de postular"),
                ReadString(root, "summary", text),
                ReadString(root, "risks", "Revisar bases y requisitos antes de postular."),
                ReadString(root, "requirements", "No se identificaron requisitos especificos en la informacion disponible."),
                ReadString(root, "nextSteps", "Revisar documentos oficiales y validar experiencia requerida."));
        }
        catch (JsonException)
        {
            return new ParsedAiAnalysis(
                "Revisar antes de postular",
                text,
                "La respuesta IA no vino en formato estructurado; revisar manualmente.",
                "Revisar bases oficiales.",
                "Validar requisitos y fechas antes de preparar propuesta.");
        }
    }

    private static string StripCodeFence(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBreak = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (firstBreak >= 0 && lastFence > firstBreak)
            {
                cleaned = cleaned[(firstBreak + 1)..lastFence].Trim();
            }
        }

        return cleaned;
    }

    private static string ReadString(JsonElement root, string name, string fallback) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private sealed record GeminiRequest(IEnumerable<GeminiContent> Contents);

    private sealed record GeminiContent(IEnumerable<GeminiPart> Parts);

    private sealed record GeminiPart(string Text);

    private sealed record ParsedAiAnalysis(
        string Recommendation,
        string Summary,
        string Risks,
        string Requirements,
        string NextSteps);
}
