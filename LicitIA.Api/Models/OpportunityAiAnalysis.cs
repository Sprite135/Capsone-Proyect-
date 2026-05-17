namespace LicitIA.Api.Models;

public sealed record OpportunityAiAnalysis
{
    public int AnalysisId { get; init; }

    public Guid UserId { get; init; }

    public int OpportunityId { get; init; }

    public string ModelName { get; init; } = string.Empty;

    public string Recommendation { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Risks { get; init; } = string.Empty;

    public string Requirements { get; init; } = string.Empty;

    public string NextSteps { get; init; } = string.Empty;

    public string RawResponse { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
