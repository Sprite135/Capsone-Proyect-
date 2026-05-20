using LicitIA.Api.Data;
using System.Net;
using System.Text.Json;

namespace LicitIA.Api.Services
{
    public class AlertSchedulerService
    {
        private readonly AlertService _alertService;
        private readonly OpportunityRepository _opportunityRepository;
        private readonly SeaceScraperService _seaceScraperService;
        private readonly AffinityService _affinityService;
        private readonly CompanyProfileRepository _profileRepository;
        private readonly NotificationRepository _notificationRepository;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public AlertSchedulerService(
            AlertService alertService,
            OpportunityRepository opportunityRepository,
            SeaceScraperService seaceScraperService,
            AffinityService affinityService,
            CompanyProfileRepository profileRepository,
            NotificationRepository notificationRepository)
        {
            _alertService = alertService;
            _opportunityRepository = opportunityRepository;
            _seaceScraperService = seaceScraperService;
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

            if (rules.Any(rule => rule.IsActive && ShouldSyncSeaceBeforeCheck(rule)))
            {
                var syncCount = await SyncSeaceForProfileAsync(profile, cancellationToken);
                Console.WriteLine($"[AlertScheduler] SEACE sync before alerts saved {syncCount} opportunities for user {profile.UserId}");
                opportunities = (await _opportunityRepository.GetAllAsync(cancellationToken)).ToList();
            }

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

        private async Task<int> SyncSeaceForProfileAsync(Models.CompanyProfile profile, CancellationToken cancellationToken)
        {
            if (!profile.UserId.HasValue)
            {
                return 0;
            }

            var callYear = profile.SeaceCallYear > 0 ? profile.SeaceCallYear : DateTime.UtcNow.Year;
            Console.WriteLine($"[AlertScheduler] Syncing SEACE before alert check. Year: {callYear}, Description: {profile.SeaceObjectDescription}");

            var scraped = await _seaceScraperService.ScrapeOpportunitiesAsync(
                maxResults: 30,
                cancellationToken: cancellationToken,
                objectDescription: profile.SeaceObjectDescription,
                callYear: callYear,
                contractObject: profile.SeaceContractObject,
                entityAcronym: profile.SeaceEntityAcronym,
                department: profile.SeaceDepartment,
                province: profile.SeaceProvince,
                district: profile.SeaceDistrict);

            await _opportunityRepository.ClearAllOpportunitiesAsync(cancellationToken);

            if (scraped.Count == 0)
            {
                return 0;
            }

            var ranked = await _affinityService.RankOpportunitiesAsync(scraped, profile.UserId.Value, cancellationToken);
            var savedCount = 0;
            for (var i = 0; i < ranked.Count; i++)
            {
                var item = ranked[i];
                try
                {
                    await _opportunityRepository.InsertOpportunityAsync(new Models.Opportunity
                    {
                        ProcessCode = item.ProcessCode,
                        Title = item.Title,
                        EntityName = item.EntityName,
                        EstimatedAmount = item.EstimatedAmount,
                        ClosingDate = item.ClosingDate,
                        Category = item.Category,
                        Modality = item.Modality,
                        MatchScore = item.MatchScore,
                        MatchedKeywordsCount = item.MatchedKeywordsCount,
                        Summary = item.Description,
                        Location = item.Location,
                        IsPriority = item.MatchScore >= 85,
                        PublishedDate = item.PublishedDate,
                        SeaceIndex = i + 1,
                        SelectionType = item.SelectionType,
                        ConvocationNumber = item.ConvocationNumber,
                        ApplicableRegulation = item.ApplicableRegulation,
                        SeaceVersion = item.SeaceVersion,
                        EntityLegalAddress = item.EntityLegalAddress,
                        EntityWebsite = item.EntityWebsite,
                        EntityPhone = item.EntityPhone,
                        ContractObject = item.ContractObject,
                        ParticipationCost = item.ParticipationCost,
                        BasesReproductionCost = item.BasesReproductionCost,
                        SeaceDetailJson = item.SeaceDetailJson,
                        SeaceScheduleJson = item.SeaceScheduleJson,
                        SeaceDocumentsJson = item.SeaceDocumentsJson
                    }, cancellationToken);
                    savedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertScheduler] Error saving synced SEACE opportunity {item.ProcessCode}: {ex.Message}");
                }
            }

            return savedCount;
        }

        private static bool ShouldSyncSeaceBeforeCheck(Models.AlertRule rule)
        {
            try
            {
                var conditions = System.Text.Json.JsonDocument.Parse(rule.ConditionsJson);
                return conditions.RootElement.TryGetProperty("syncBeforeCheck", out var value) &&
                    value.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            catch
            {
                return false;
            }
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
            var orderedMatches = matches
                .OrderByDescending(match => match.AffinityScore)
                .ToList();
            var highAffinityCount = orderedMatches.Count(match => match.AffinityScore >= 85);
            var urgentCount = orderedMatches.Count(match => IsClosingSoon(match.Opportunity.ClosingDate));
            var bestMatch = orderedMatches.FirstOrDefault()?.AffinityScore ?? 0;
            var threshold = GetAffinityThreshold(rule);

            var cards = orderedMatches
                .Select((match, index) =>
                {
                    var opportunity = match.Opportunity;
                    var amount = opportunity.EstimatedAmount > 0
                        ? $"S/ {opportunity.EstimatedAmount:N2}"
                        : "No disponible";
                    var closingDate = opportunity.ClosingDate?.ToString("dd/MM/yyyy") ?? "No disponible";
                    var publishedDate = opportunity.PublishedDate?.ToString("dd/MM/yyyy") ?? "No disponible";
                    var priorityLabel = GetPriorityLabel(match.AffinityScore);
                    var priorityClass = GetPriorityClass(match.AffinityScore);
                    var reason = BuildRecommendationReason(opportunity, match.AffinityScore);
                    var closingBadge = IsClosingSoon(opportunity.ClosingDate)
                        ? "<span class='status-badge urgent'>Vence pronto</span>"
                        : "<span class='status-badge neutral'>En plazo</span>";

                    return $@"
<article class='opportunity-card'>
    <div class='card-topline'>
        <span class='rank'>#{index + 1}</span>
        <span class='score {priorityClass}'>{match.AffinityScore}%</span>
    </div>
    <h3>{Encode(opportunity.ProcessCode)}</h3>
    <p class='entity'>{Encode(opportunity.EntityName)}</p>
    <p class='title'>{Encode(opportunity.Title)}</p>
    <div class='meta-grid'>
        <div><span>Monto</span><strong>{Encode(amount)}</strong></div>
        <div><span>Cierre</span><strong>{Encode(closingDate)}</strong></div>
        <div><span>Publicado</span><strong>{Encode(publishedDate)}</strong></div>
        <div><span>Modalidad</span><strong>{Encode(opportunity.Modality)}</strong></div>
        <div><span>Ubicacion</span><strong>{Encode(opportunity.Location)}</strong></div>
        <div><span>Categoria</span><strong>{Encode(opportunity.Category)}</strong></div>
    </div>
    <div class='card-footer'>
        <span class='status-badge {priorityClass}'>{priorityLabel}</span>
        {closingBadge}
    </div>
    <p class='reason'><strong>Motivo:</strong> {Encode(reason)}</p>
</article>";
                });

            return $@"
<section class='alert-intro'>
    <p>La regla <strong>{Encode(rule.Name)}</strong> encontro oportunidades que superan la afinidad configurada.</p>
</section>
<section class='summary-grid'>
    <div><span>Total</span><strong>{matches.Count}</strong></div>
    <div><span>Alta afinidad</span><strong>{highAffinityCount}</strong></div>
    <div><span>Mayor afinidad</span><strong>{bestMatch}%</strong></div>
    <div><span>Vencen pronto</span><strong>{urgentCount}</strong></div>
</section>
<p class='rule-note'>Umbral configurado: <strong>{threshold}%</strong>. Revisa primero las oportunidades con mayor afinidad y las que estan cerca de su fecha de cierre.</p>
<section class='opportunity-list'>
    {string.Join("", cards)}
</section>";
        }

        private static int GetAffinityThreshold(Models.AlertRule rule)
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.ConditionsJson);
                if (doc.RootElement.TryGetProperty("affinityThreshold", out var thresholdElement)
                    && thresholdElement.TryGetInt32(out var threshold))
                {
                    return threshold;
                }
            }
            catch
            {
                return 80;
            }

            return 80;
        }

        private static bool IsClosingSoon(DateTime? closingDate)
        {
            if (!closingDate.HasValue)
            {
                return false;
            }

            var daysLeft = (closingDate.Value.Date - DateTime.Today).TotalDays;
            return daysLeft >= 0 && daysLeft <= 5;
        }

        private static string GetPriorityLabel(int affinityScore)
        {
            if (affinityScore >= 85)
            {
                return "Alta afinidad";
            }

            if (affinityScore >= 70)
            {
                return "Afinidad media";
            }

            return "Afinidad baja";
        }

        private static string GetPriorityClass(int affinityScore)
        {
            if (affinityScore >= 85)
            {
                return "high";
            }

            if (affinityScore >= 70)
            {
                return "medium";
            }

            return "low";
        }

        private static string BuildRecommendationReason(Models.Opportunity opportunity, int affinityScore)
        {
            var parts = new List<string>();

            if (affinityScore >= 85)
            {
                parts.Add("coincide fuertemente con las preferencias configuradas");
            }
            else if (affinityScore >= 70)
            {
                parts.Add("tiene coincidencias relevantes con tus criterios");
            }
            else
            {
                parts.Add("cumple el umbral minimo definido en la regla");
            }

            if (opportunity.MatchedKeywordsCount > 0)
            {
                parts.Add($"incluye {opportunity.MatchedKeywordsCount} coincidencia(s) de palabras clave");
            }

            if (IsClosingSoon(opportunity.ClosingDate))
            {
                parts.Add("requiere revision rapida por cercania de cierre");
            }

            return string.Join("; ", parts) + ".";
        }

        private static string DisplayValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "No disponible" : value.Trim();
        }

        private static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(DisplayValue(value));
        }

        private sealed record AlertOpportunityMatch(Models.Opportunity Opportunity, int AffinityScore);
    }

    public sealed record AlertCheckResult(int RulesProcessed, int OpportunitiesMatched, int SummariesCreated);
}
