using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{
    /// <summary>
    /// Minimal base for all Query Builders.
    /// Provides normalization logic, region cache, and
    /// shared product category expansion functionality.
    /// </summary>
    public abstract class BaseQueryBuilder : IQueryBuilder
    {
        protected readonly IServiceNameNormalizer Normalizer;
        protected readonly IRegionHierarchyCache RegionCache;
        protected readonly IProductCategoryMap? ProductCategoryMap;

        protected BaseQueryBuilder(
            IServiceNameNormalizer normalizer,
            IRegionHierarchyCache regionCache,
            IProductCategoryMap? productCategoryMap = null)
        {
            Normalizer = normalizer;
            RegionCache = regionCache;
            ProductCategoryMap = productCategoryMap;
        }

        public abstract bool CanHandle(IntentResponse intent);
        public abstract string BuildQuery(IntentResponse response);

        protected string NormalizeServiceName(string? name)
            => string.IsNullOrWhiteSpace(name) ? string.Empty : Normalizer.Normalize(name);

        protected List<string> NormalizeServiceNames(IEnumerable<string>? names)
            => names?.Select(Normalizer.Normalize).ToList() ?? new();

        /// <summary>
        /// Expands product categories into offerings using the provided ProductCategoryMap.
        /// Non-destructive: preserves original offerings and ensures uniqueness.
        /// </summary>
        protected void ExpandProductCategories(List<string> offerings, List<string>? categories, IntentResponse intent)
        {
            if (ProductCategoryMap == null || categories == null || categories.Count == 0)
                return;

            if (offerings.Count > 0)
            {
                if (offerings.Count > 0 && intent.Filters.ProductCategoryName.Any())
                    intent.Clarifications.Add("OfferingName specified; ProductCategoryName retained for traceability only.");
                return;
            }

            var expandedOffers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var category in categories)
            {
                if (ProductCategoryMap.ContainsCategory(category))
                {
                    var mapped = ProductCategoryMap.GetOfferingsForCategory(category);
                    foreach (var offer in mapped)
                        expandedOffers.Add(offer);
                }
            }

            foreach (var offer in expandedOffers)
                if (!offerings.Contains(offer, StringComparer.OrdinalIgnoreCase))
                    offerings.Add(offer);
        }

        public Dictionary<string, string> ColumnAliases { get; } = new()
        {
            { "RegionName", "RegionName [Region]" },
            { "GeographyName", "GeographyName [Geography]" },
            { "MacroGeographyName", "MacroGeographyName [Macro Geography]" },
            { "OfferingName", "OfferingName [Product]" },
            { "ProductSkuName", "ProductSkuName [Product SKU]" },
            { "CurrentState", "CurrentState [Current State]" }
        };

        protected HashSet<string> ResolveExclusions(Filters filters)
        {
            var excludedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var excl = filters.Exclusions;
            if (excl == null || excl.ScopeValue.Count == 0)
                return excludedRegions;

            // Get full canonical region list (with suffixes)
            var allRegions = RegionCache.GetAllRegions()
                .ToList();

            foreach (var rawValue in excl.ScopeValue)
            {
                var value = rawValue?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(value))
                    continue;

                var matchedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Exact case-insensitive match (e.g., "East US 2" == "east us 2")
                foreach (var r in allRegions)
                {
                    if (r.Equals(value, StringComparison.OrdinalIgnoreCase))
                        matchedRegions.Add(r);
                }

                // Substring match (e.g., "Taiwan" -> "Taiwan North**")
                if (matchedRegions.Count == 0)
                {
                    foreach (var r in allRegions)
                    {
                        if (r.Contains(value, StringComparison.OrdinalIgnoreCase))
                            matchedRegions.Add(r);
                    }
                }

                // Wildcard / fuzzy match (e.g., "Taiwan*" -> "Taiwan North**")
                if (matchedRegions.Count == 0 && (value.Contains('*') || value.Contains('%')))
                {
                    var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(value)
                        .Replace("\\*", ".*")
                        .Replace("%", ".*") + "$";
                    var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    foreach (var r in allRegions)
                    {
                        if (regex.IsMatch(r))
                            matchedRegions.Add(r);
                    }
                }

                //Geography resolution fallback (e.g., "Taiwan" --> all regions under Taiwan geography)
                if (matchedRegions.Count == 0)
                {
                    var geoRegions = RegionCache.GetRegionsForGeographies(new[] { value });
                    foreach (var r in geoRegions)
                        matchedRegions.Add(r);
                }

                // Macro-geography fallback (e.g., "Asia Pacific" -> all APAC regions)
                if (matchedRegions.Count == 0)
                {
                    var macroRegions = RegionCache.GetRegionsForMacros(new[] { value });
                    foreach (var r in macroRegions)
                        matchedRegions.Add(r);
                }

                //Add all resolved matches (preserve suffixes)
                foreach (var region in matchedRegions)
                    excludedRegions.Add(region);
            }

            return excludedRegions;
        }
    }
}
