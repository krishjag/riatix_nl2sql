using System.Text;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{
    /// <summary>
    /// Builds SQL queries identifying common Azure products (Offerings/SKUs)
    /// across regions, geographies, or macro-geographies.
    /// Uses a hybrid mode:
    /// - INTERSECT for multi-scope comparisons
    /// - HAVING COUNT for single-scope consistency checks.
    /// Includes exclusion-aware logic for regions, geographies, and macro-geographies.
    /// </summary>
    public class IntersectionQueryBuilder : BaseQueryBuilder
    {
        public IntersectionQueryBuilder(
            IServiceNameNormalizer normalizer,
            IRegionHierarchyCache regionHierarchyCache,
            IProductCategoryMap productCategoryMap)
            : base(normalizer, regionHierarchyCache, productCategoryMap)
        {
        }

        public override bool CanHandle(IntentResponse intent) =>
            intent.Intent.Equals("intersection", StringComparison.OrdinalIgnoreCase);

        public override string BuildQuery(IntentResponse intent)
        {
            var regions = intent.Filters.RegionName ?? new();
            var geographies = intent.Filters.GeographyName ?? new();
            var macros = ExpandGlobalMacros(intent.Filters.MacroGeographyName ?? new());

            // Analyze total scopes to decide mode
            int totalScopes = regions.Count + geographies.Count + macros.Count();
            bool multiScope = totalScopes >= 2 ||
                              (regions.Any() && geographies.Any()) ||
                              (geographies.Any() && macros.Any());

            return multiScope
                ? BuildIntersectBasedQuery(intent, regions, geographies, macros.ToList())
                : BuildCountBasedQuery(intent, regions, geographies, macros);
        }

        // ---------------------------------------------------------------------
        // INTERSECT-BASED LOGIC (multi-scope)
        // ---------------------------------------------------------------------
        private string BuildIntersectBasedQuery(
            IntentResponse intent,
            List<string> regions,
            List<string> geographies,
            List<string> macros)
        {
            var sb = new StringBuilder();

            var states = intent.Filters.CurrentState?.Any() == true
                ? intent.Filters.CurrentState
                : new List<string> { "GA" };

            var offerings = new List<string>(intent.Filters.OfferingName ?? new());
            ExpandProductCategories(offerings, intent.Filters.ProductCategoryName ?? new(), intent);

            var skus = intent.Filters.ProductSkuName ?? new();

            // Resolve exclusions (regions, geographies, macros)
            var excludedRegions = ResolveExclusions(intent.Filters);

            // Build subqueries
            var subqueries = new List<string>();
            foreach (var m in macros)
                subqueries.Add(BuildSubquery("MacroGeographyName", new[] { m }, states, offerings, skus, excludedRegions));
            foreach (var g in geographies)
                subqueries.Add(BuildSubquery("GeographyName", new[] { g }, states, offerings, skus, excludedRegions));
            foreach (var r in regions)
                subqueries.Add(BuildSubquery("RegionName", new[] { r }, states, offerings, skus, excludedRegions));

            sb.AppendLine("-- Intersection Query (INTERSECT Mode)");
            sb.AppendLine(string.Join($"{Environment.NewLine}INTERSECT{Environment.NewLine}", subqueries));
            sb.AppendLine("ORDER BY OfferingName;");

            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // HAVING COUNT LOGIC (single-scope)
        // ---------------------------------------------------------------------
        private string BuildCountBasedQuery(
            IntentResponse intent,
            List<string> regions,
            List<string> geographies,
            IEnumerable<string> macros)
        {
            var sb = new StringBuilder();

            var states = intent.Filters.CurrentState?.Any() == true
                ? intent.Filters.CurrentState
                : new List<string> { "GA" };

            var offerings = new List<string>(intent.Filters.OfferingName ?? new());
            ExpandProductCategories(offerings, intent.Filters.ProductCategoryName ?? new(), intent);

            var skus = intent.Filters.ProductSkuName ?? new();

            // Resolve exclusions
            var excludedRegions = ResolveExclusions(intent.Filters);

            // --- Determine the active scope ---
            string scopeColumn;
            IEnumerable<string> scopeValues;
            HashSet<string> targetRegions = new(StringComparer.OrdinalIgnoreCase);

            if (regions.Any())
            {
                scopeColumn = "RegionName";
                scopeValues = regions;
                targetRegions.UnionWith(regions);
            }
            else if (geographies.Any())
            {
                scopeColumn = "GeographyName";
                scopeValues = geographies;
                targetRegions.UnionWith(RegionCache.GetRegionsForGeographies(geographies));
            }
            else
            {
                scopeColumn = "MacroGeographyName";
                scopeValues = macros;
                targetRegions.UnionWith(RegionCache.GetRegionsForMacros(macros));
            }

            // Apply exclusions to target region set
            targetRegions.ExceptWith(excludedRegions);
            int effectiveRegionCount = targetRegions.Count;

            sb.AppendLine("-- Intersection Query (HAVING COUNT Mode, Cache Optimized)");
            sb.AppendLine("SELECT OfferingName AS [Product], ProductSkuName AS [Product SKU]");
            sb.AppendLine("FROM dbo.products_info");

            var whereClauses = new List<string>
            {
                $"CurrentState IN ({string.Join(", ", states.Select(Quote))})",
                $"{scopeColumn} IN ({string.Join(", ", scopeValues.Select(Quote))})"
            };

            if (excludedRegions.Any())
                whereClauses.Add($"RegionName NOT IN ({string.Join(", ", excludedRegions.Select(Quote))})");

            if (offerings.Any())
                whereClauses.Add($"OfferingName IN ({string.Join(", ", offerings.Select(o => Quote(Normalizer.Normalize(o))))})");

            if (skus.Any())
                whereClauses.Add($"ProductSkuName IN ({string.Join(", ", skus.Select(Quote))})");

            sb.AppendLine("WHERE " + string.Join(" AND ", whereClauses));
            sb.AppendLine("GROUP BY OfferingName, ProductSkuName");

            // Apply intersection rule only if >1 effective regions
            if (effectiveRegionCount > 1)
                sb.AppendLine($"HAVING COUNT(DISTINCT RegionName) = {effectiveRegionCount}");

            sb.AppendLine("ORDER BY OfferingName;");
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // Helper: Build one subquery block
        // ---------------------------------------------------------------------
        private string BuildSubquery(
            string scopeColumn,
            IEnumerable<string> scopeValues,
            IEnumerable<string> states,
            IEnumerable<string> offerings,
            IEnumerable<string> skus,
            HashSet<string> excludedRegions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SELECT DISTINCT OfferingName AS [Product], ProductSkuName AS [Product SKU]");
            sb.AppendLine("FROM dbo.products_info");

            var whereClauses = new List<string>
            {
                $"CurrentState IN ({string.Join(", ", states.Select(Quote))})",
                $"{scopeColumn} IN ({string.Join(", ", scopeValues.Select(Quote))})"
            };

            if (excludedRegions.Any())
                whereClauses.Add($"RegionName NOT IN ({string.Join(", ", excludedRegions.Select(Quote))})");

            if (offerings.Any())
                whereClauses.Add($"OfferingName IN ({string.Join(", ", offerings.Select(o => Quote(Normalizer.Normalize(o))))})");

            if (skus.Any())
                whereClauses.Add($"ProductSkuName IN ({string.Join(", ", skus.Select(Quote))})");

            sb.AppendLine("WHERE " + string.Join(" AND ", whereClauses));
            return sb.ToString().TrimEnd();
        }

        // ---------------------------------------------------------------------
        // Helper: Expand "Global" macros
        // ---------------------------------------------------------------------
        private IEnumerable<string> ExpandGlobalMacros(IEnumerable<string> macros)
        {
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var macro in macros)
            {
                if (macro.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    // Fetch all macros from cache
                    var allMacros = RegionCache
                        .GetRegionsForMacros(new[] { "All" })
                        .Where(m =>
                            !m.Contains("Gov", StringComparison.OrdinalIgnoreCase) &&
                            !m.Contains("Government", StringComparison.OrdinalIgnoreCase) &&
                            !m.Contains("DoD", StringComparison.OrdinalIgnoreCase));

                    foreach (var m in allMacros)
                        expanded.Add(m);
                }
                else
                {
                    expanded.Add(macro);
                }
            }

            return expanded;
        }

        // ---------------------------------------------------------------------
        // Helper: Quote strings for SQL
        // ---------------------------------------------------------------------
        private static string Quote(string value) =>
            $"'{value.Replace("'", "''")}'";
    }
}
