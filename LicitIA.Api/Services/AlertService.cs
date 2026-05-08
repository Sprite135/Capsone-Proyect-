using LicitIA.Api.Data;
using LicitIA.Api.Models;
using System.Text.Json;

namespace LicitIA.Api.Services;

public class AlertService
{
    private readonly AlertRepository _alertRepository;
    private readonly OpportunityRepository _opportunityRepository;
    private readonly EmailService _emailService;

    public AlertService(
        AlertRepository alertRepository,
        OpportunityRepository opportunityRepository,
        EmailService emailService)
    {
        _alertRepository = alertRepository;
        _opportunityRepository = opportunityRepository;
        _emailService = emailService;
    }

    public async Task<List<AlertRule>> GetAlertRulesByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _alertRepository.GetByUserIdAsync(userId, cancellationToken);
    }

    public async Task<AlertRule?> GetAlertRuleByIdAsync(int ruleId, CancellationToken cancellationToken)
    {
        return await _alertRepository.GetByIdAsync(ruleId, cancellationToken);
    }

    public async Task<int> CreateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        return await _alertRepository.InsertAsync(rule, cancellationToken);
    }

    public async Task<int> UpdateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        return await _alertRepository.UpdateAsync(rule, cancellationToken);
    }

    public async Task<int> DeleteAlertRuleAsync(int ruleId, CancellationToken cancellationToken)
    {
        return await _alertRepository.DeleteAsync(ruleId, cancellationToken);
    }

    public async Task<AlertSummary> GetAlertSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeCount = await _alertRepository.GetActiveCountByUserIdAsync(userId, cancellationToken);
        var todayTriggered = await _alertRepository.GetTodayTriggeredCountByUserIdAsync(userId, cancellationToken);

        return new AlertSummary
        {
            ActiveRules = activeCount,
            TodayTriggered = todayTriggered,
            Pending = Math.Max(0, todayTriggered - activeCount) // Simplified logic
        };
    }

    public async Task CheckAndTriggerAlertsAsync(CancellationToken cancellationToken)
    {
        // Get all active alert rules
        // This is a simplified version - in production you'd want to cache this
        // For now, we'll skip the full implementation since we need a way to iterate all users
        Console.WriteLine("[AlertService] Checking for opportunities that match alert rules...");
        
        // TODO: Implement full alert checking logic
        // 1. Get all active alert rules
        // 2. For each rule, check if conditions match new opportunities
        // 3. If match, send notifications via configured channels
        // 4. Update LastTriggeredAtUtc and TriggerCount
    }

    public async Task SendTestEmailAsync(string toEmail, string ruleName, CancellationToken cancellationToken)
    {
        var subject = $"[LicitIA] Prueba de Alerta: {ruleName}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #3b82f6, #1cc8b7); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f8fafc; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; margin-top: 20px; color: #64748b; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📢 Prueba de Alerta</h1>
        </div>
        <div class='content'>
            <h2>Esta es una prueba de notificación</h2>
            <p>La regla de alerta <strong>{ruleName}</strong> se ha configurado correctamente.</p>
            <p>Cuando se cumplan las condiciones de esta regla, recibirás notificaciones como esta.</p>
            <p><em>Este es un mensaje de prueba automático.</em></p>
        </div>
        <div class='footer'>
            <p>LicitIA - Sistema de Inteligencia de Licitaciones</p>
        </div>
    </div>
</body>
</html>";

        await _emailService.SendEmailAsync(toEmail, subject, body, true, cancellationToken);
    }

    public async Task<List<AlertRule>> GetRulesByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _alertRepository.GetByUserIdAsync(userId, cancellationToken);
    }

    public async Task SendAlertEmailAsync(string toEmail, string subject, string message, CancellationToken cancellationToken)
    {
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #3b82f6, #1cc8b7); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f8fafc; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; margin-top: 20px; color: #64748b; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📢 Alerta Activada</h1>
        </div>
        <div class='content'>
            <h2>{subject}</h2>
            <p>{message}</p>
            <p><em>Esta es una notificación automática de LicitIA.</em></p>
        </div>
        <div class='footer'>
            <p>LicitIA - Sistema de Inteligencia de Licitaciones</p>
        </div>
    </div>
</body>
</html>";

        await _emailService.SendEmailAsync(toEmail, subject, body, true, cancellationToken);
    }

    public async Task UpdateRuleLastTriggeredAsync(int ruleId, CancellationToken cancellationToken)
    {
        await _alertRepository.UpdateLastTriggeredAsync(ruleId, cancellationToken);
    }
}

public record AlertSummary
{
    public int ActiveRules { get; init; }
    public int TodayTriggered { get; init; }
    public int Pending { get; init; }
}
