using LicitIA.Api.Models;
using LicitIA.Api.Services;

namespace LicitIA.Api.Contracts;

public sealed class OpportunityResponse
{
    public int OpportunityId { get; init; }

    public string ProcessCode { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string EntityName { get; init; } = string.Empty;

    public decimal EstimatedAmount { get; init; }

    public DateTime? ClosingDate { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Modality { get; init; } = string.Empty;

    public int MatchScore { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public bool IsPriority { get; init; }

    public DateTime? PublishedDate { get; init; }

    public int MatchedKeywordsCount { get; init; }

    public string RecommendationLabel { get; init; } = string.Empty;

    public string RecommendationReason { get; init; } = string.Empty;

    public string PriorityLevel { get; init; } = string.Empty;

    public IReadOnlyList<string> MatchedKeywords { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ExcludedKeywords { get; init; } = Array.Empty<string>();

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
            IsPriority = opportunity.IsPriority,
            PublishedDate = opportunity.PublishedDate,
            MatchedKeywordsCount = opportunity.MatchedKeywordsCount,
            SelectionType = opportunity.SelectionType,
            ConvocationNumber = opportunity.ConvocationNumber,
            ApplicableRegulation = opportunity.ApplicableRegulation,
            SeaceVersion = opportunity.SeaceVersion,
            EntityLegalAddress = opportunity.EntityLegalAddress,
            EntityWebsite = opportunity.EntityWebsite,
            EntityPhone = opportunity.EntityPhone,
            ContractObject = opportunity.ContractObject,
            ParticipationCost = opportunity.ParticipationCost,
            BasesReproductionCost = opportunity.BasesReproductionCost,
            SeaceDetailJson = opportunity.SeaceDetailJson,
            SeaceScheduleJson = opportunity.SeaceScheduleJson,
            RecommendationLabel = opportunity.MatchScore >= 80 ? "Alta prioridad" :
                opportunity.MatchScore >= 60 ? "Recomendada" :
                opportunity.MatchScore >= 40 ? "Revisar" : "Baja afinidad",
            RecommendationReason = "Recomendacion calculada con el puntaje de afinidad almacenado.",
            PriorityLevel = opportunity.MatchScore >= 80 ? "Prioridad alta" :
                opportunity.MatchScore >= 60 ? "Prioridad media" :
                opportunity.MatchScore >= 40 ? "Revision sugerida" : "Baja prioridad"
        };

    public static OpportunityResponse FromModel(Opportunity opportunity, RecommendationAnalysis analysis) =>
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
            MatchScore = analysis.Score,
            Summary = opportunity.Summary,
            Location = opportunity.Location,
            IsPriority = analysis.Score >= 80,
            PublishedDate = opportunity.PublishedDate,
            MatchedKeywordsCount = analysis.MatchedKeywords.Count,
            SelectionType = opportunity.SelectionType,
            ConvocationNumber = opportunity.ConvocationNumber,
            ApplicableRegulation = opportunity.ApplicableRegulation,
            SeaceVersion = opportunity.SeaceVersion,
            EntityLegalAddress = opportunity.EntityLegalAddress,
            EntityWebsite = opportunity.EntityWebsite,
            EntityPhone = opportunity.EntityPhone,
            ContractObject = opportunity.ContractObject,
            ParticipationCost = opportunity.ParticipationCost,
            BasesReproductionCost = opportunity.BasesReproductionCost,
            SeaceDetailJson = opportunity.SeaceDetailJson,
            SeaceScheduleJson = opportunity.SeaceScheduleJson,
            RecommendationLabel = analysis.Label,
            RecommendationReason = analysis.Reason,
            PriorityLevel = analysis.PriorityLevel,
            MatchedKeywords = analysis.MatchedKeywords,
            ExcludedKeywords = analysis.ExcludedKeywords
        };
}
