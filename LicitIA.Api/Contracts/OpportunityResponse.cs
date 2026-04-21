using LicitIA.Api.Models;

namespace LicitIA.Api.Contracts;

public sealed class OpportunityResponse
{
    public int OpportunityId { get; init; }

    public string ProcessCode { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string EntityName { get; init; } = string.Empty;

    public decimal EstimatedAmount { get; init; }

    public DateTime ClosingDate { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Modality { get; init; } = string.Empty;

    public int MatchScore { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public bool IsPriority { get; init; }

    public static OpportunityResponse FromModel(Opportunity opportunity) =>
        new()
        {
            OpportunityId = opportunity.OpportunityId,
            ProcessCode = opportunity.ProcessCode,
            Title = opportunity.Title,
            EntityName = opportunity.EntityName,
            EstimatedAmount = opportunity.EstimatedAmount,
            ClosingDate = opportunity.ClosingDate,
            Category = opportunity.Category,
            Modality = opportunity.Modality,
            MatchScore = opportunity.MatchScore,
            Summary = opportunity.Summary,
            Location = opportunity.Location,
            IsPriority = opportunity.IsPriority
        };
}
