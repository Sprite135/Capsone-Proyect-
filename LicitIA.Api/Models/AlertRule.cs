namespace LicitIA.Api.Models;

public record AlertRule
{
    public int RuleId { get; init; }
    
    public Guid UserId { get; init; }
    
    public string Name { get; init; } = string.Empty;
    
    public string TriggerType { get; init; } = string.Empty; // "alta_afinidad", "nueva_oportunidad", "cierre_proximo"
    
    public string ConditionsJson { get; init; } = string.Empty; // JSON con condiciones
    
    public string ChannelsJson { get; init; } = string.Empty; // JSON con canales: ["email", "panel", "slack"]
    
    public string RecipientsJson { get; init; } = string.Empty; // JSON con emails
    
    public string MessageTemplate { get; init; } = string.Empty;
    
    public bool IsActive { get; init; }
    
    public DateTime CreatedAtUtc { get; init; }
    
    public DateTime? LastTriggeredAtUtc { get; init; }
    
    public int TriggerCount { get; init; }
}
