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

        public Task<AlertCheckResult> RunOnceAsync(CancellationToken cancellationToken)
        {
            return CheckAndTriggerAlertsAsync(cancellationToken);
        }

        public async Task<AlertCheckResult> RunOnceForUserAsync(Guid userId, bool force, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[AlertScheduler] Manual alert check for user {userId}");

            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
            if (profile is null)
            {
                Console.WriteLine($"[AlertScheduler] No profile found for user {userId}");
                return new AlertCheckResult(0, 0, 0);
            }

            var opportunities = await _opportunityRepository.GetAllAsync(cancellationToken);
            return await ProcessUserAlertsAsync(profile, opportunities.ToList(), force, cancellationToken);
        }

        private async Task<AlertCheckResult> CheckAndTriggerAlertsAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[AlertScheduler] Checking for alerts...");

            var totalRulesProcessed = 0;
            var totalMatches = 0;
            var totalSummaries = 0;
            var opportunities = await _opportunityRepository.GetAllAsync(cancellationToken);
            Console.WriteLine($"[AlertScheduler] Found {opportunities.Count} opportunities");

            var profiles = await _profileRepository.GetAllProfilesAsync(cancellationToken);
            Console.WriteLine($"[AlertScheduler] Found {profiles.Count} user profiles");

            foreach (var profile in profiles)
            {
                try
                {
                    var result = await ProcessUserAlertsAsync(profile, opportunities.ToList(), force: false, cancellationToken);
                    totalRulesProcessed += result.RulesProcessed;
                    totalMatches += result.OpportunitiesMatched;
                    totalSummaries += result.SummariesCreated;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error processing user {profile.UserId}: {ex.Message}");
                }
            }

            Console.WriteLine("[AlertScheduler] Alert check completed.");
            return new AlertCheckResult(totalRulesProcessed, totalMatches, totalSummaries);
        }

        private async Task<AlertCheckResult> ProcessUserAlertsAsync(
            Models.CompanyProfile profile,
            List<Models.Opportunity> opportunities,
            bool force,
            CancellationToken cancellationToken)
        {
            var rulesProcessed = 0;
            var opportunitiesMatched = 0;
            var summariesCreated = 0;

            if (!profile.UserId.HasValue)
            {
                Console.WriteLine("[AlertScheduler] Profile without user id skipped.");
                return new AlertCheckResult(0, 0, 0);
            }

            Console.WriteLine($"[AlertScheduler] Processing alerts for user {profile.UserId}");

            var rules = await _alertService.GetRulesByUserIdAsync(profile.UserId.Value, cancellationToken);
            if (!rules.Any())
            {
                Console.WriteLine($"[AlertScheduler] No rules found for user {profile.UserId}");
                return new AlertCheckResult(0, 0, 0);
            }

            Console.WriteLine($"[AlertScheduler] Found {rules.Count} rules for user {profile.UserId}");

            foreach (var rule in rules.Where(r => r.IsActive))
            {
                try
                {
                    var matches = await ProcessRuleAsync(rule, profile.UserId.Value, opportunities, force, cancellationToken);
                    rulesProcessed++;
                    opportunitiesMatched += matches;
                    if (matches > 0)
                    {
                        summariesCreated++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error processing rule {rule.RuleId}: {ex.Message}");
                }
            }

            return new AlertCheckResult(rulesProcessed, opportunitiesMatched, summariesCreated);
        }

        private async Task<int> ProcessRuleAsync(
            Models.AlertRule rule,
            Guid userId,
            List<Models.Opportunity> opportunities,
            bool force,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[AlertScheduler] Processing rule {rule.RuleId}: {rule.Name}");

            var conditions = System.Text.Json.JsonDocument.Parse(rule.ConditionsJson);
            var affinityThreshold = conditions.RootElement.GetProperty("affinityScore").GetInt32();
            var matches = new List<AlertOpportunityMatch>();

            foreach (var opportunity in opportunities)
            {
                try
                {
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

                    var affinityScore = await _affinityService.CalculateAffinityScoreAsync(
                        scrapedOpportunity,
                        userId,
                        cancellationToken);

                    if (affinityScore >= affinityThreshold && ShouldTriggerAlert(rule, opportunity, force))
                    {
                        Console.WriteLine($"[AlertScheduler] Opportunity {opportunity.ProcessCode} meets threshold: {affinityScore}% >= {affinityThreshold}%");
                        matches.Add(new AlertOpportunityMatch(opportunity, affinityScore));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error processing opportunity {opportunity.ProcessCode}: {ex.Message}");
                }
            }

            if (matches.Count > 0)
            {
                await TriggerAlertSummaryAsync(rule, matches, cancellationToken);
            }

            return matches.Count;
        }

        private bool ShouldTriggerAlert(Models.AlertRule rule, Models.Opportunity opportunity, bool force)
        {
            if (force)
            {
                return true;
            }

            if (!rule.LastTriggeredAtUtc.HasValue)
            {
                return true;
            }

            if (opportunity.PublishedDate.HasValue && opportunity.PublishedDate > rule.LastTriggeredAtUtc)
            {
                return true;
            }

            if (DateTime.UtcNow > rule.LastTriggeredAtUtc.Value.AddHours(24))
            {
                return true;
            }

            return false;
        }

        private async Task TriggerAlertSummaryAsync(
            Models.AlertRule rule,
            List<AlertOpportunityMatch> matches,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[AlertScheduler] Triggering summary alert for rule {rule.RuleId} with {matches.Count} opportunities");

            var channels = System.Text.Json.JsonDocument.Parse(rule.ChannelsJson);
            var recipients = System.Text.Json.JsonDocument.Parse(rule.RecipientsJson);
            var subject = $"LicitIA - {matches.Count} oportunidad(es) para {rule.Name}";
            var panelMessage = BuildPanelSummaryMessage(matches);
            var emailMessage = BuildEmailSummaryMessage(rule, matches);

            if (channels.RootElement.EnumerateArray().Any(c => c.GetString() == "email"))
            {
                var recipientEmails = recipients.RootElement
                    .EnumerateArray()
                    .Select(r => r.GetString())
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .ToList();

                foreach (var email in recipientEmails)
                {
                    await _alertService.SendAlertEmailAsync(email!, subject, emailMessage, cancellationToken);
                }
            }

            if (channels.RootElement.EnumerateArray().Any(c => c.GetString() == "panel"))
            {
                var topMatch = matches.OrderByDescending(match => match.AffinityScore).First();
                var panelNotification = new Models.PanelNotification
                {
                    UserId = rule.UserId,
                    Title = subject,
                    Message = panelMessage,
                    Type = "alert",
                    OpportunityProcessCode = topMatch.Opportunity.ProcessCode,
                    OpportunityTitle = topMatch.Opportunity.Title,
                    AffinityScore = topMatch.AffinityScore,
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _notificationRepository.InsertAsync(panelNotification, cancellationToken);
                Console.WriteLine($"[AlertScheduler] Panel notification saved for user {rule.UserId}");
            }

            await _alertService.UpdateRuleLastTriggeredAsync(rule.RuleId, cancellationToken);

            Console.WriteLine("[AlertScheduler] Summary alert triggered successfully");
        }

        private static string BuildPanelSummaryMessage(List<AlertOpportunityMatch> matches)
        {
            var topMatches = matches
                .OrderByDescending(match => match.AffinityScore)
                .Take(5)
                .Select(match => $"{match.Opportunity.ProcessCode} - {match.AffinityScore}% - {match.Opportunity.Title}");

            return $"Se encontraron {matches.Count} oportunidad(es) compatibles: {string.Join(" | ", topMatches)}";
        }

        private static string BuildEmailSummaryMessage(Models.AlertRule rule, List<AlertOpportunityMatch> matches)
        {
            var lines = matches
                .OrderByDescending(match => match.AffinityScore)
                .Select((match, index) =>
                {
                    var opportunity = match.Opportunity;
                    var amount = opportunity.EstimatedAmount > 0
                        ? $"S/ {opportunity.EstimatedAmount:N2}"
                        : "No disponible";
                    var closingDate = opportunity.ClosingDate?.ToString("dd/MM/yyyy") ?? "No disponible";

                    return $@"
<strong>{index + 1}. {opportunity.ProcessCode}</strong><br>
Afinidad: <strong>{match.AffinityScore}%</strong><br>
Objeto: {opportunity.Title}<br>
Entidad: {opportunity.EntityName}<br>
Monto estimado: {amount}<br>
Cierre: {closingDate}<br>
Modalidad: {opportunity.Modality}<br>
Ubicacion: {opportunity.Location}<br>";
                });

            return $@"
La regla <strong>{rule.Name}</strong> encontro {matches.Count} oportunidad(es) que cumplen la afinidad configurada.<br><br>
{string.Join("<hr>", lines)}";
        }

        private sealed record AlertOpportunityMatch(Models.Opportunity Opportunity, int AffinityScore);
    }

    public sealed record AlertCheckResult(int RulesProcessed, int OpportunitiesMatched, int SummariesCreated);
}
