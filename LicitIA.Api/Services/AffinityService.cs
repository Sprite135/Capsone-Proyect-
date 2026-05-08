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
            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
            if (profile == null)
            {
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
            int score = 0;

            // Keywords en título y descripción (100 puntos) - LicitaLAB style
            var (keywordScore, matchedCount) = CalculateKeywordScore(opportunity, profile);
            score += keywordScore;
            opportunity.MatchedKeywordsCount = matchedCount;

            // Asegurar que el score esté entre 0 y 100
            return Math.Max(0, Math.Min(score, 100));
        }

        private (int score, int matchedCount) CalculateKeywordScore(ScrapedOpportunity opportunity, Models.CompanyProfile profile)
        {
            int score = 0;
            int matchedCount = 0;
            string text = $"{opportunity.Title} {opportunity.Description}".ToLower();

            // Keywords preferidos (suman puntos)
            foreach (var keyword in profile.PreferredKeywords)
            {
                int count = CountOccurrences(text, keyword.ToLower());
                if (count > 0)
                {
                    score += Math.Min(count, 3) * 10; // 10 puntos por ocurrencia, max 30
                    matchedCount++;
                }
            }

            // Keywords excluidos (restan puntos)
            foreach (var keyword in profile.ExcludedKeywords)
            {
                int count = CountOccurrences(text, keyword.ToLower());
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
