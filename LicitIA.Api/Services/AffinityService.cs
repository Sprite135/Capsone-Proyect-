using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicitIA.Api.Data;

namespace LicitIA.Api.Services
{
    public class AffinityService
    {
        private readonly CompanyProfileRepository _profileRepository;
        private Models.CompanyProfile? _cachedProfile;

        public AffinityService(CompanyProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
        }

        private async Task<Models.CompanyProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Affinity] Getting profile for userId: {userId}");
            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
            Console.WriteLine($"[Affinity] Profile found: {profile != null}");
            if (profile != null)
            {
                Console.WriteLine($"[Affinity] Profile details: {profile.CompanyName}, PreferredKeywords: [{string.Join(", ", profile.PreferredKeywords)}], ExcludedKeywords: [{string.Join(", ", profile.ExcludedKeywords)}]");
            }
            if (profile == null)
            {
                Console.WriteLine($"[Affinity] No profile found, using default profile");
                profile = await _profileRepository.GetDefaultProfileAsync(cancellationToken);
            }
            
            if (profile == null)
            {
                profile = new Models.CompanyProfile
                {
                    CompanyName = "Default",
                    PreferredKeywords = new List<string>(),
                    ExcludedKeywords = new List<string>()
                };
            }

            return profile;
        }

        private async Task<Models.CompanyProfile> GetDefaultProfileAsync(CancellationToken cancellationToken)
        {
            if (_cachedProfile != null)
            {
                return _cachedProfile;
            }

            var profile = await _profileRepository.GetDefaultProfileAsync(cancellationToken);
            if (profile == null)
            {
                profile = new Models.CompanyProfile
                {
                    CompanyName = "Default",
                    PreferredKeywords = new List<string>(),
                    ExcludedKeywords = new List<string>()
                };
            }

            _cachedProfile = profile;
            return profile;
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

        public int CalculateAffinityScore(ScrapedOpportunity opportunity, Models.CompanyProfile profile)
        {
            // Calcular afinidad porcentual basada en coincidencias
            var (keywordScore, matchedCount) = CalculateKeywordScore(opportunity, profile);
            opportunity.MatchedKeywordsCount = matchedCount;

            // Total de keywords disponibles
            int totalKeywords = profile.PreferredKeywords.Count + profile.ExcludedKeywords.Count;
            
            if (totalKeywords == 0)
            {
                return 0; // Sin keywords configuradas = 0% afinidad
            }

            // Calcular porcentaje de coincidencias
            double affinityPercentage = (double)matchedCount / totalKeywords * 100;
            
            // Redondear a entero y asegurar que esté entre 0 y 100
            return Math.Max(0, Math.Min(100, (int)Math.Round(affinityPercentage)));
        }

        private (int score, int matchedCount) CalculateKeywordScore(ScrapedOpportunity opportunity, Models.CompanyProfile profile)
        {
            int score = 0;
            int matchedCount = 0;
            string text = $"{opportunity.Title} {opportunity.Description}";

            // Keywords preferidos (suman puntos)
            foreach (var keyword in profile.PreferredKeywords)
            {
                int count = CountOccurrences(text, keyword);
                Console.WriteLine($"[Affinity] Checking keyword '{keyword}' in text: '{text.Substring(0, Math.Min(100, text.Length))}...' - Count: {count}");
                if (count > 0)
                {
                    score += Math.Min(count, 3) * 10; // 10 puntos por ocurrencia, max 30
                    matchedCount++;
                    Console.WriteLine($"[Affinity] Keyword matched! Score: +{Math.Min(count, 3) * 10}, Total: {score}");
                }
            }

            // Keywords excluidos (restan puntos)
            foreach (var keyword in profile.ExcludedKeywords)
            {
                int count = CountOccurrences(text, keyword);
                if (count > 0)
                {
                    score -= Math.Min(count, 3) * 20; // Penalización fuerte
                }
            }

            return (Math.Max(0, Math.Min(score, 100)), matchedCount);
        }

        private int CountOccurrences(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += keyword.Length;
            }
            return count;
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
                >= 70 => "Prioridad alta",
                >= 40 => "Prioridad media",
                _ => "Prioridad baja"
            };
        }

        public async Task<List<ScrapedOpportunity>> RankOpportunitiesAsync(List<ScrapedOpportunity> opportunities, Guid userId, CancellationToken cancellationToken)
        {
            var profile = await GetProfileAsync(userId, cancellationToken);

            foreach (var opportunity in opportunities)
            {
                opportunity.MatchScore = CalculateAffinityScore(opportunity, profile);
            }

            return opportunities.OrderByDescending(o => o.MatchScore).ToList();
        }

        public async Task<List<ScrapedOpportunity>> RankOpportunitiesAsync(List<ScrapedOpportunity> opportunities, CancellationToken cancellationToken)
        {
            var profile = await GetDefaultProfileAsync(cancellationToken);

            foreach (var opportunity in opportunities)
            {
                opportunity.MatchScore = CalculateAffinityScore(opportunity, profile);
            }

            return opportunities.OrderByDescending(o => o.MatchScore).ToList();
        }

        public void InvalidateCache()
        {
            _cachedProfile = null;
        }
    }
}
