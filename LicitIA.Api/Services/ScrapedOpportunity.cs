namespace LicitIA.Api.Services;

public class ScrapedOpportunity
{
    public string ProcessCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public decimal EstimatedAmount { get; set; }
    public DateTime? ClosingDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? PublishedDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public int MatchScore { get; set; }
    public int MatchedKeywordsCount { get; set; }
    public string SelectionType { get; set; } = string.Empty;
    public string ConvocationNumber { get; set; } = string.Empty;
    public string ApplicableRegulation { get; set; } = string.Empty;
    public string SeaceVersion { get; set; } = string.Empty;
    public string EntityLegalAddress { get; set; } = string.Empty;
    public string EntityWebsite { get; set; } = string.Empty;
    public string EntityPhone { get; set; } = string.Empty;
    public string ContractObject { get; set; } = string.Empty;
    public string ParticipationCost { get; set; } = string.Empty;
    public string BasesReproductionCost { get; set; } = string.Empty;
    public string SeaceDetailJson { get; set; } = string.Empty;
    public string SeaceScheduleJson { get; set; } = string.Empty;
    public string SeaceDetailButtonId { get; set; } = string.Empty;
    public int SeaceRowIndex { get; set; }
}
