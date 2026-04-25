namespace LicitIA.Api.Models;

public sealed class Opportunity
{
    public int OpportunityId { get; init; }

    public string ProcessCode { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string EntityName { get; init; } = string.Empty;

    public decimal EstimatedAmount { get; init; }

    public DateTime? ClosingDate { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Modality { get; init; } = string.Empty;

    public int MatchScore { get; set; }

    public string Summary { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public bool IsPriority { get; init; }

    public DateTime? PublishedDate { get; init; }

    public int? SeaceIndex { get; init; }
}
