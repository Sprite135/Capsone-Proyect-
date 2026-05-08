namespace LicitIA.Api.Models;

public record PanelNotification
{
    public int NotificationId { get; init; }
    
    public Guid UserId { get; init; }
    
    public string Title { get; init; } = string.Empty;
    
    public string Message { get; init; } = string.Empty;
    
    public string Type { get; init; } = string.Empty; // "alert", "info", "warning", "success"
    
    public string? OpportunityProcessCode { get; init; }
    
    public string? OpportunityTitle { get; init; }
    
    public int? AffinityScore { get; init; }
    
    public bool IsRead { get; init; }
    
    public DateTime CreatedAtUtc { get; init; }
    
    public DateTime? ReadAtUtc { get; init; }
}
