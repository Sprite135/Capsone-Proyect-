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

    public int MatchedKeywordsCount { get; set; }

    public string Summary { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public bool IsPriority { get; init; }

    public DateTime? PublishedDate { get; init; }

    public int? SeaceIndex { get; init; }

    public string SelectionType { get; init; } = string.Empty;

    public string ConvocationNumber { get; init; } = string.Empty;

    public string ApplicableRegulation { get; init; } = string.Empty;

    public string SeaceVersion { get; init; } = string.Empty;

    public string EntityLegalAddress { get; init; } = string.Empty;

    public string EntityWebsite { get; init; } = string.Empty;

    public string EntityPhone { get; init; } = string.Empty;

    public string ContractObject { get; init; } = string.Empty;

    public string ParticipationCost { get; init; } = string.Empty;

    public string BasesReproductionCost { get; init; } = string.Empty;

    public string SeaceDetailJson { get; init; } = string.Empty;

    public string SeaceScheduleJson { get; init; } = string.Empty;
}
