using LicitIA.Api.Data;
using LicitIA.Api.Models;

namespace LicitIA.Api.Services
{
    public class AffinityService
    {
        private readonly CompanyProfileRepository _profileRepository;
        private CompanyProfile? _cachedProfile;

        public AffinityService(CompanyProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
        }

        private async Task<CompanyProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Affinity] Getting profile for userId: {userId}");
            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
            Console.WriteLine($"[Affinity] Profile found: {profile != null}");

            if (profile != null)
            {
                Console.WriteLine($"[Affinity] Profile details: {profile.CompanyName}, PreferredKeywords: [{string.Join(", ", profile.PreferredKeywords)}], ExcludedKeywords: [{string.Join(", ", profile.ExcludedKeywords)}]");
                return profile;
            }

            Console.WriteLine("[Affinity] No profile found, using default profile");
            return await GetDefaultProfileAsync(cancellationToken);
        }

        private async Task<CompanyProfile> GetDefaultProfileAsync(CancellationToken cancellationToken)
        {
            if (_cachedProfile != null)
            {
                return _cachedProfile;
            }

            _cachedProfile = await _profileRepository.GetDefaultProfileAsync(cancellationToken)
                ?? new CompanyProfile
                {
                    CompanyName = "Default",
                    PreferredKeywords = new List<string>(),
                    ExcludedKeywords = new List<string>()
                };

            return _cachedProfile;
        }

        public async Task<int> CalculateAffinityScoreAsync(ScrapedOpportunity opportunity, Guid userId, CancellationToken cancellationToken)
        {
            var profile = await GetProfileAsync(userId, cancellationToken);
            return CalculateAffinityScore(opportunity, profile);
        }

        public async Task<int> CalculateAffinityScoreAsync(ScrapedOpportunity opportunity, CancellationToken cancellationToken)
        {
            var profile = await GetDefaultProfileAsync(cancellationToken);
            return CalculateAffinityScore(opportunity, profile);
        }

        public int CalculateAffinityScore(ScrapedOpportunity opportunity, CompanyProfile profile)
        {
            var analysis = AnalyzeScrapedOpportunity(opportunity, profile);
            opportunity.MatchedKeywordsCount = analysis.MatchedKeywords.Count;
            return analysis.Score;
        }

        public async Task<RecommendationAnalysis> AnalyzeOpportunityAsync(Opportunity opportunity, Guid? userId, CancellationToken cancellationToken)
        {
            var profile = userId.HasValue
                ? await GetProfileAsync(userId.Value, cancellationToken)
                : await GetDefaultProfileAsync(cancellationToken);

            return AnalyzeOpportunity(opportunity, profile);
        }

        public RecommendationAnalysis AnalyzeOpportunity(Opportunity opportunity, CompanyProfile profile)
        {
            var text = BuildSearchText(
                opportunity.Title,
                opportunity.Summary,
                opportunity.Category,
                opportunity.Modality,
                opportunity.EntityName,
                opportunity.Location);

            return AnalyzeText(
                text,
                opportunity.EstimatedAmount,
                opportunity.ClosingDate,
                opportunity.Category,
                opportunity.Modality,
                opportunity.EntityName,
                opportunity.Location,
                profile);
        }

        public RecommendationAnalysis AnalyzeScrapedOpportunity(ScrapedOpportunity opportunity, CompanyProfile profile)
        {
            var text = BuildSearchText(
                opportunity.Title,
                opportunity.Description,
                opportunity.Category,
                opportunity.Modality,
                opportunity.EntityName,
                opportunity.Location);

            return AnalyzeText(
                text,
                opportunity.EstimatedAmount,
                opportunity.ClosingDate,
                opportunity.Category,
                opportunity.Modality,
                opportunity.EntityName,
                opportunity.Location,
                profile);
        }

        private RecommendationAnalysis AnalyzeText(
            string text,
            decimal amount,
            DateTime? closingDate,
            string category,
            string modality,
            string entityName,
            string location,
            CompanyProfile profile)
        {
            var preferredKeywords = CleanList(profile.PreferredKeywords);
            var excludedKeywords = CleanList(profile.ExcludedKeywords);
            var keywordOccurrences = preferredKeywords
                .Select(keyword => new KeywordMatch(keyword, CountOccurrences(text, keyword)))
                .Where(match => match.Count > 0)
                .ToList();
            var excludedOccurrences = excludedKeywords
                .Select(keyword => new KeywordMatch(keyword, CountOccurrences(text, keyword)))
                .Where(match => match.Count > 0)
                .ToList();
            var matchedKeywords = keywordOccurrences
                .Select(match => match.Keyword)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var excludedMatches = excludedOccurrences
                .Select(match => match.Keyword)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var score = 0;
            var signals = new List<string>();

            if (preferredKeywords.Count > 0)
            {
                var keywordScore = CalculateKeywordScore(preferredKeywords.Count, keywordOccurrences);
                score += keywordScore;

                if (matchedKeywords.Count > 0)
                {
                    signals.Add($"coincide con {FormatList(matchedKeywords.Take(4))}");
                }
            }
            else
            {
                signals.Add("no hay keywords preferidas configuradas");
            }

            if (MatchesAny(category, profile.PreferredCategories))
            {
                score += 10;
                signals.Add($"categoria afin: {category}");
            }

            if (MatchesAny(modality, profile.PreferredModalities))
            {
                score += 8;
                signals.Add($"modalidad afin: {modality}");
            }

            if (MatchesAny(location, profile.PreferredLocations))
            {
                score += 7;
                signals.Add($"ubicacion afin: {location}");
            }

            if (MatchesAny(entityName, profile.FavoriteEntities))
            {
                score += 10;
                signals.Add($"entidad priorizada: {entityName}");
            }

            if (profile.IdealAmount > 0 && amount > 0)
            {
                var distance = Math.Abs((double)((amount - profile.IdealAmount) / profile.IdealAmount));
                if (distance <= 0.25)
                {
                    score += 8;
                    signals.Add("monto cercano al ideal");
                }
            }
            else if (profile.MinAmount > 0 || profile.MaxAmount > 0)
            {
                var minOk = profile.MinAmount <= 0 || amount >= profile.MinAmount;
                var maxOk = profile.MaxAmount <= 0 || amount <= profile.MaxAmount;
                if (amount > 0 && minOk && maxOk)
                {
                    score += 6;
                    signals.Add("monto dentro del rango configurado");
                }
            }

            if (closingDate.HasValue)
            {
                var daysToClose = (closingDate.Value.Date - DateTime.UtcNow.Date).Days;
                if (daysToClose >= profile.MinDaysToClose && daysToClose <= profile.MaxDaysToClose)
                {
                    score += 7;
                    signals.Add("plazo dentro del rango de postulacion");
                }
            }

            if (MatchesAny(entityName, profile.ExcludedEntities))
            {
                score -= 25;
                signals.Add("entidad excluida por el perfil");
            }

            if (excludedMatches.Count > 0)
            {
                score -= Math.Min(35, excludedMatches.Count * 15);
                signals.Add($"contiene excluidas: {FormatList(excludedMatches.Take(3))}");
            }

            score = Math.Max(0, Math.Min(100, score));

            return new RecommendationAnalysis(
                score,
                GetRecommendationLabel(score),
                GetPriorityLevel(score),
                BuildReason(score, matchedKeywords, excludedMatches, signals),
                matchedKeywords,
                excludedMatches);
        }

        private int CountOccurrences(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += keyword.Length;
            }

            return count;
        }

        private static int CalculateKeywordScore(int preferredKeywordCount, IReadOnlyList<KeywordMatch> keywordOccurrences)
        {
            if (preferredKeywordCount == 0 || keywordOccurrences.Count == 0)
            {
                return 0;
            }

            if (keywordOccurrences.Count >= preferredKeywordCount)
            {
                return 100;
            }

            var coverageScore = Math.Min(35, keywordOccurrences.Count * 18);
            var frequencyScore = Math.Min(20, keywordOccurrences.Sum(match => Math.Min(match.Count, 3)) * 5);
            var breadthBonus = preferredKeywordCount <= 3
                ? keywordOccurrences.Count * 5
                : (int)Math.Round((double)keywordOccurrences.Count / preferredKeywordCount * 15);
            var firstMatchFloor = keywordOccurrences.Count > 0 ? 45 : 0;

            return Math.Min(75, Math.Max(firstMatchFloor, coverageScore + frequencyScore + breadthBonus));
        }

        public string GetAffinityLabel(int score)
        {
            return score switch
            {
                >= 70 => "Alta afinidad",
                >= 40 => "Afinidad media",
                _ => "Baja afinidad"
            };
        }

        public string GetPriorityLevel(int score)
        {
            return score switch
            {
                >= 80 => "Prioridad alta",
                >= 60 => "Prioridad media",
                >= 40 => "Revision sugerida",
                _ => "Baja prioridad"
            };
        }

        public string GetRecommendationLabel(int score)
        {
            return score switch
            {
                >= 80 => "Alta prioridad",
                >= 60 => "Recomendada",
                >= 40 => "Revisar",
                _ => "Baja afinidad"
            };
        }

        public async Task<List<ScrapedOpportunity>> RankOpportunitiesAsync(List<ScrapedOpportunity> opportunities, Guid userId, CancellationToken cancellationToken)
        {
            var profile = await GetProfileAsync(userId, cancellationToken);

            foreach (var opportunity in opportunities)
            {
                var analysis = AnalyzeScrapedOpportunity(opportunity, profile);
                opportunity.MatchScore = analysis.Score;
                opportunity.MatchedKeywordsCount = analysis.MatchedKeywords.Count;
            }

            return opportunities.OrderByDescending(o => o.MatchScore).ToList();
        }

        public async Task<List<ScrapedOpportunity>> RankOpportunitiesAsync(List<ScrapedOpportunity> opportunities, CancellationToken cancellationToken)
        {
            var profile = await GetDefaultProfileAsync(cancellationToken);

            foreach (var opportunity in opportunities)
            {
                var analysis = AnalyzeScrapedOpportunity(opportunity, profile);
                opportunity.MatchScore = analysis.Score;
                opportunity.MatchedKeywordsCount = analysis.MatchedKeywords.Count;
            }

            return opportunities.OrderByDescending(o => o.MatchScore).ToList();
        }

        public void InvalidateCache()
        {
            _cachedProfile = null;
        }

        private static string BuildSearchText(params string[] values) =>
            string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));

        private static List<string> CleanList(IEnumerable<string> values) =>
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static bool MatchesAny(string value, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return CleanList(candidates).Any(candidate =>
                value.Contains(candidate, StringComparison.OrdinalIgnoreCase) ||
                candidate.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatList(IEnumerable<string> values) =>
            string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)));

        private string BuildReason(
            int score,
            IReadOnlyList<string> matchedKeywords,
            IReadOnlyList<string> excludedMatches,
            IReadOnlyList<string> signals)
        {
            if (excludedMatches.Count > 0 && score < 60)
            {
                return $"Afinidad reducida porque contiene palabras excluidas: {FormatList(excludedMatches.Take(3))}.";
            }

            if (matchedKeywords.Count > 0)
            {
                return $"Recomendada porque {signals.FirstOrDefault() ?? $"coincide con {FormatList(matchedKeywords.Take(4))}"}." +
                    (signals.Count > 1 ? $" Tambien suma: {FormatList(signals.Skip(1).Take(2))}." : string.Empty);
            }

            if (score >= 40)
            {
                return $"Conviene revisarla por senales del perfil: {FormatList(signals.Take(3))}.";
            }

            return "Baja afinidad porque no se encontraron coincidencias fuertes con las keywords o preferencias configuradas.";
        }
    }

    public sealed record RecommendationAnalysis(
        int Score,
        string Label,
        string PriorityLevel,
        string Reason,
        IReadOnlyList<string> MatchedKeywords,
        IReadOnlyList<string> ExcludedKeywords);

    internal sealed record KeywordMatch(string Keyword, int Count);
}
