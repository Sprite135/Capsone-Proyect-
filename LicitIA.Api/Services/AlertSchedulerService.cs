using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicitIA.Api.Data;

namespace LicitIA.Api.Services
{
    public class AlertSchedulerService
    {
        private readonly AlertService _alertService;
        private readonly OpportunityRepository _opportunityRepository;
        private readonly AffinityService _affinityService;
        private readonly CompanyProfileRepository _profileRepository;
        private readonly NotificationRepository _notificationRepository;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public AlertSchedulerService(
            AlertService alertService,
            OpportunityRepository opportunityRepository,
            AffinityService affinityService,
            CompanyProfileRepository profileRepository,
            NotificationRepository notificationRepository)
        {
            _alertService = alertService;
            _opportunityRepository = opportunityRepository;
            _affinityService = affinityService;
            _profileRepository = profileRepository;
            _notificationRepository = notificationRepository;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(TimeSpan interval)
        {
            Console.WriteLine($"[AlertScheduler] Starting scheduler with interval: {interval}");
            
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await CheckAndTriggerAlertsAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[AlertScheduler] Scheduler cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error: {ex.Message}");
                }

                await Task.Delay(interval, _cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            Console.WriteLine("[AlertScheduler] Stopping scheduler...");
            _cancellationTokenSource.Cancel();
        }

        private async Task CheckAndTriggerAlertsAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[AlertScheduler] Checking for alerts...");

            // Obtener todas las oportunidades
            var opportunities = await _opportunityRepository.GetAllAsync(cancellationToken);
            Console.WriteLine($"[AlertScheduler] Found {opportunities.Count} opportunities");

            // Obtener todos los usuarios con perfiles configurados
            var profiles = await _profileRepository.GetAllProfilesAsync(cancellationToken);
            Console.WriteLine($"[AlertScheduler] Found {profiles.Count} user profiles");

            foreach (var profile in profiles)
            {
                try
                {
                    await ProcessUserAlertsAsync(profile, opportunities.ToList(), cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error processing user {profile.UserId}: {ex.Message}");
                }
            }

            Console.WriteLine("[AlertScheduler] Alert check completed.");
        }

        private async Task ProcessUserAlertsAsync(
            Models.CompanyProfile profile,
            List<Models.Opportunity> opportunities,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[AlertScheduler] Processing alerts for user {profile.UserId}");

            // Obtener reglas de alerta del usuario
            var rules = await _alertService.GetRulesByUserIdAsync(profile.UserId.Value, cancellationToken);
            if (!rules.Any())
            {
                Console.WriteLine($"[AlertScheduler] No rules found for user {profile.UserId}");
                return;
            }

            Console.WriteLine($"[AlertScheduler] Found {rules.Count} rules for user {profile.UserId}");

            foreach (var rule in rules.Where(r => r.IsActive))
            {
                try
                {
                    await ProcessRuleAsync(rule, profile.UserId.Value, opportunities, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error processing rule {rule.RuleId}: {ex.Message}");
                }
            }
        }

        private async Task ProcessRuleAsync(
            Models.AlertRule rule,
            Guid userId,
            List<Models.Opportunity> opportunities,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[AlertScheduler] Processing rule {rule.RuleId}: {rule.Name}");

            var conditions = System.Text.Json.JsonDocument.Parse(rule.ConditionsJson);
            var affinityThreshold = conditions.RootElement.GetProperty("affinityScore").GetInt32();
            var requiredStatus = conditions.RootElement.GetProperty("status").GetString();

            foreach (var opportunity in opportunities)
            {
                try
                {
                    // Convertir Opportunity a ScrapedOpportunity para calcular afinidad
                    var scrapedOpportunity = new ScrapedOpportunity
                    {
                        ProcessCode = opportunity.ProcessCode,
                        Title = opportunity.Title,
                        Description = opportunity.Summary,
                        EntityName = opportunity.EntityName,
                        EstimatedAmount = opportunity.EstimatedAmount,
                        Category = opportunity.Category,
                        Modality = opportunity.Modality,
                        Location = opportunity.Location,
                        ClosingDate = opportunity.ClosingDate,
                        PublishedDate = opportunity.PublishedDate
                    };

                    // Calcular afinidad para este usuario
                    var affinityScore = await _affinityService.CalculateAffinityScoreAsync(
                        scrapedOpportunity,
                        userId,
                        cancellationToken);

                    // Verificar si cumple la condición
                    if (affinityScore >= affinityThreshold)
                    {
                        Console.WriteLine($"[AlertScheduler] Opportunity {opportunity.ProcessCode} meets threshold: {affinityScore}% >= {affinityThreshold}%");

                        // Verificar si ya fue disparada recientemente (evitar duplicados)
                        if (ShouldTriggerAlert(rule, opportunity))
                        {
                            await TriggerAlertAsync(rule, opportunity, affinityScore, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error processing opportunity {opportunity.ProcessCode}: {ex.Message}");
                }
            }
        }

        private bool ShouldTriggerAlert(Models.AlertRule rule, Models.Opportunity opportunity)
        {
            // Si la regla nunca se disparó, disparar
            if (!rule.LastTriggeredAtUtc.HasValue)
            {
                return true;
            }

            // Si la oportunidad es nueva (publicada después del último disparo), disparar
            if (opportunity.PublishedDate.HasValue && opportunity.PublishedDate > rule.LastTriggeredAtUtc)
            {
                return true;
            }

            // Si pasó más de 24 horas desde el último disparo, disparar
            if (DateTime.UtcNow > rule.LastTriggeredAtUtc.Value.AddHours(24))
            {
                return true;
            }

            return false;
        }

        private async Task TriggerAlertAsync(
            Models.AlertRule rule,
            Models.Opportunity opportunity,
            int affinityScore,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[AlertScheduler] Triggering alert for rule {rule.RuleId} and opportunity {opportunity.ProcessCode}");

            var channels = System.Text.Json.JsonDocument.Parse(rule.ChannelsJson);
            var recipients = System.Text.Json.JsonDocument.Parse(rule.RecipientsJson);

            // Preparar mensaje
            var message = rule.MessageTemplate
                .Replace("{title}", opportunity.Title)
                .Replace("{score}", affinityScore.ToString())
                .Replace("{processCode}", opportunity.ProcessCode)
                .Replace("{entity}", opportunity.EntityName)
                .Replace("{amount}", opportunity.EstimatedAmount.ToString("C"));

            // Enviar por email
            if (channels.RootElement.EnumerateArray().Any(c => c.GetString() == "email"))
            {
                var recipientEmails = recipients.RootElement.EnumerateArray().Select(r => r.GetString()).ToList();
                foreach (var email in recipientEmails)
                {
                    await _alertService.SendAlertEmailAsync(email, $"Alerta: {rule.Name}", message, cancellationToken);
                }
            }

            // Enviar notificación al panel
            if (channels.RootElement.EnumerateArray().Any(c => c.GetString() == "panel"))
            {
                var panelNotification = new Models.PanelNotification
                {
                    UserId = rule.UserId,
                    Title = $"Alerta: {rule.Name}",
                    Message = message,
                    Type = "alert",
                    OpportunityProcessCode = opportunity.ProcessCode,
                    OpportunityTitle = opportunity.Title,
                    AffinityScore = affinityScore,
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _notificationRepository.InsertAsync(panelNotification, cancellationToken);
                Console.WriteLine($"[AlertScheduler] Panel notification saved for user {rule.UserId}");
            }

            // TODO: Implementar notificaciones en Slack

            // Actualizar última fecha de disparo
            await _alertService.UpdateRuleLastTriggeredAsync(rule.RuleId, cancellationToken);

            Console.WriteLine($"[AlertScheduler] Alert triggered successfully");
        }
    }
}
