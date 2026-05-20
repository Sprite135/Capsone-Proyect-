using LicitIA.Api.Data;
using LicitIA.Api.Models;
using System.Net;

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
            Pending = Math.Max(0, todayTriggered - activeCount)
        };
    }

    public async Task CheckAndTriggerAlertsAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[AlertService] Checking for opportunities that match alert rules...");
    }

    public async Task SendTestEmailAsync(string toEmail, string ruleName, CancellationToken cancellationToken)
    {
        var subject = $"[LicitIA] Prueba de Alerta: {ruleName}";
        var safeRuleName = WebUtility.HtmlEncode(ruleName);
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ margin: 0; background: #071427; font-family: 'Segoe UI', Arial, sans-serif; color: #dbe7ff; line-height: 1.6; }}
        .container {{ max-width: 720px; margin: 0 auto; padding: 24px; }}
        .header {{ background: #0d1b31; border: 1px solid #203756; padding: 28px; border-radius: 18px 18px 0 0; }}
        .eyebrow {{ margin: 0 0 8px; color: #8fb6ff; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; font-weight: 700; }}
        h1 {{ margin: 0; color: #f4f7ff; font-size: 28px; }}
        .content {{ background: #101d31; border: 1px solid #203756; border-top: 0; padding: 28px; border-radius: 0 0 18px 18px; }}
        .panel {{ background: #17243a; border: 1px solid #2b4265; border-radius: 14px; padding: 18px; }}
        .footer {{ text-align: center; margin-top: 18px; color: #8ea1bd; font-size: 12px; }}
        strong {{ color: #ffffff; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <p class='eyebrow'>LicitIA Alertas</p>
            <h1>Prueba de alerta</h1>
        </div>
        <div class='content'>
            <div class='panel'>
                <p>La regla <strong>{safeRuleName}</strong> esta configurada correctamente.</p>
                <p>Cuando una oportunidad supere la afinidad minima, recibiras un resumen ejecutivo con los procesos recomendados.</p>
            </div>
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
        var safeSubject = WebUtility.HtmlEncode(subject);
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ margin: 0; background: #071427; font-family: 'Segoe UI', Arial, sans-serif; color: #dbe7ff; line-height: 1.6; }}
        .container {{ max-width: 760px; margin: 0 auto; padding: 24px; }}
        .header {{ background: #0d1b31; border: 1px solid #203756; padding: 28px; border-radius: 18px 18px 0 0; }}
        .eyebrow {{ margin: 0 0 8px; color: #8fb6ff; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; font-weight: 700; }}
        h1 {{ margin: 0; color: #f4f7ff; font-size: 28px; line-height: 1.2; }}
        h2 {{ color: #f4f7ff; font-size: 20px; margin: 0 0 16px; }}
        h3 {{ color: #f4f7ff; font-size: 18px; margin: 10px 0 4px; }}
        .content {{ background: #101d31; border: 1px solid #203756; border-top: 0; padding: 28px; border-radius: 0 0 18px 18px; }}
        .alert-intro {{ background: #17243a; border: 1px solid #2b4265; border-radius: 14px; padding: 16px 18px; margin-bottom: 18px; }}
        .alert-intro p {{ margin: 0; color: #c8d6ee; }}
        .summary-grid {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; margin: 0 0 18px; }}
        .summary-grid div {{ background: #0b1729; border: 1px solid #203756; border-radius: 12px; padding: 12px; }}
        .summary-grid span {{ display: block; color: #8ea1bd; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }}
        .summary-grid strong {{ display: block; color: #58a0ff; font-size: 24px; margin-top: 4px; }}
        .rule-note {{ color: #9fb0ca; margin: 0 0 18px; }}
        .opportunity-card {{ background: #17243a; border: 1px solid #2b4265; border-radius: 16px; padding: 18px; margin: 14px 0; }}
        .card-topline {{ display: flex; justify-content: space-between; align-items: center; gap: 10px; }}
        .rank {{ display: inline-block; background: #ffd21f; color: #16213a; border-radius: 8px; padding: 4px 9px; font-weight: 800; }}
        .score {{ display: inline-block; border-radius: 999px; padding: 6px 12px; color: #ffffff; font-weight: 800; }}
        .score.high, .status-badge.high {{ background: #24c6b0; }}
        .score.medium, .status-badge.medium {{ background: #3b82f6; }}
        .score.low, .status-badge.low {{ background: #ff9f6e; }}
        .entity {{ margin: 0 0 8px; color: #dbe7ff; font-weight: 700; }}
        .title {{ margin: 0 0 14px; color: #a8b8d2; }}
        .meta-grid {{ display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; }}
        .meta-grid div {{ background: #0b1729; border: 1px solid #203756; border-radius: 10px; padding: 10px; }}
        .meta-grid span {{ display: block; color: #8ea1bd; font-size: 11px; text-transform: uppercase; letter-spacing: 1px; }}
        .meta-grid strong {{ display: block; color: #f4f7ff; margin-top: 3px; }}
        .card-footer {{ margin: 14px 0 10px; }}
        .status-badge {{ display: inline-block; border-radius: 999px; padding: 6px 10px; margin-right: 6px; color: #ffffff; font-size: 12px; font-weight: 800; }}
        .status-badge.urgent {{ background: #ef4444; }}
        .status-badge.neutral {{ background: #263a59; color: #c8d6ee; }}
        .reason {{ margin: 0; color: #c8d6ee; }}
        .footer {{ text-align: center; margin-top: 18px; color: #8ea1bd; font-size: 12px; }}
        strong {{ color: #ffffff; }}
        @media (max-width: 640px) {{
            .summary-grid, .meta-grid {{ display: block; }}
            .summary-grid div, .meta-grid div {{ margin-bottom: 10px; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <p class='eyebrow'>LicitIA Alertas</p>
            <h1>Alerta activada</h1>
        </div>
        <div class='content'>
            <h2>{safeSubject}</h2>
            {message}
        </div>
        <div class='footer'>
            <p>Mensaje automatico de LicitIA. Valida siempre los datos contra SEACE.</p>
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
