using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using SimMetrics.Net.Metric;
using Fastenshtein;
using Levenshtein = Fastenshtein.Levenshtein;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    /// <summary>
    /// Consensus-based fuzzy normalizer for Azure service names.
    /// Uses a canonical map supplied by ICanonicalMapLoader.
    /// </summary>
    public class ConsensusServiceNameNormalizer : IServiceNameNormalizer
    {
        private readonly Lazy<Dictionary<string, List<string>>> _canonicalMap;
        private readonly JaroWinkler _jaro = new();
        private readonly Dictionary<string, string> _cache = new();
        private readonly object _sync = new();

        public double ConfidenceThreshold { get; set; } = 0.8;

        private readonly (double tokenSet, double jaro, double levenshtein) _weights =
            (tokenSet: 0.4, jaro: 0.3, levenshtein: 0.3);

        public ConsensusServiceNameNormalizer(ICanonicalMapLoader loader)
        {
            _canonicalMap = new Lazy<Dictionary<string, List<string>>>(() => loader.Load());
        }

        public string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            lock (_sync)
                if (_cache.TryGetValue(input, out var cached))
                    return cached;

            var normInput = NormalizeText(input);
            var scores = new List<(string Canonical, double Score)>();

            foreach (var kvp in _canonicalMap.Value)
            {
                foreach (var alias in kvp.Value.Append(kvp.Key))
                {
                    var normAlias = NormalizeText(alias);

                    double tokenSet = Fuzz.TokenSetRatio(normInput, normAlias) / 100.0;
                    double jaro = _jaro.GetSimilarity(normInput, normAlias);
                    double lev = 1.0 - (Levenshtein.Distance(normInput, normAlias)
                                       / (double)Math.Max(normInput.Length, normAlias.Length));

                    double consensus = (_weights.tokenSet * tokenSet) +
                                       (_weights.jaro * jaro) +
                                       (_weights.levenshtein * lev);

                    scores.Add((kvp.Key, consensus));
                }
            }

            var best = scores.GroupBy(s => s.Canonical)
                             .Select(g => (Canonical: g.Key, Score: g.Max(x => x.Score)))
                             .OrderByDescending(g => g.Score);

            var result = best.FirstOrDefault().Score >= ConfidenceThreshold ? best.FirstOrDefault().Canonical : input;

            lock (_sync)
                _cache[input] = result;

            return result;
        }

        private static string NormalizeText(string text)
        {
            text = text.ToLowerInvariant();
            text = Regex.Replace(text, @"[^a-z0-9\s]", "");
            text = Regex.Replace(text, @"(?<=\s|^)ai(?=\s|$)", " artificial intelligence ", RegexOptions.IgnoreCase);
            var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(t => t);
            return string.Join(' ', tokens);
        }
    }
}
