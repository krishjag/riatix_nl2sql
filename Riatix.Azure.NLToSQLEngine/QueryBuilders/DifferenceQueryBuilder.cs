using System.Text;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{
    /// <summary>
    /// Builds SQL queries that identify differences in service or SKU availability
    /// across regions, geographies, or macro-geographies.
    /// Supports symmetric (A Δ B) and directional (A - B) comparisons.
    /// Includes Global-aware macro expansion for non-government scopes.
    /// </summary>
    public class DifferenceQueryBuilder : BaseQueryBuilder
    {
        public DifferenceQueryBuilder(
            IServiceNameNormalizer normalizer,
            IRegionHierarchyCache regionHierarchyCache,
            IProductCategoryMap productCategoryMap)
            : base(normalizer, regionHierarchyCache, productCategoryMap)
        {
        }

        public override bool CanHandle(IntentResponse intent) =>
            intent.Intent.Equals("difference", StringComparison.OrdinalIgnoreCase);

        public override string BuildQuery(IntentResponse intent)
        {
            var sb = new StringBuilder();

            // --- Determine states ---
            var states = intent.Filters.CurrentState?.Any() == true
                ? intent.Filters.CurrentState
                : new List<string> { "GA" };

            // --- Offerings & Categories ---
            var offerings = new List<string>(intent.Filters.OfferingName ?? new());
            ExpandProductCategories(offerings, intent.Filters.ProductCategoryName ?? new(), intent);

            var skus = intent.Filters.ProductSkuName ?? new List<string>();

            // --- Directionality ---
            var mode = intent.Parameters?.DifferenceMode ?? "symmetric";
            var source = intent.Parameters?.DifferenceSource;
            var target = intent.Parameters?.DifferenceTarget;

            if (mode.Equals("symmetric", StringComparison.OrdinalIgnoreCase) || source == null || target == null)
            {
                return BuildSymmetricComparison(intent, states, offerings, skus);
            }

            // --- Directional Difference (A - B) ---
            string sourceFilter = BuildScopedConditionWithGlobalAwareExpansion(source);
            string targetFilter = BuildScopedConditionWithGlobalAwareExpansion(target);

            sb.AppendLine("-- Directional Difference Query (A - B)");
            sb.AppendLine("SELECT DISTINCT OfferingName AS [Product], ProductSkuName AS [SKU]");
            sb.AppendLine("FROM dbo.products_info");
            sb.AppendLine($"WHERE {sourceFilter}");
            sb.AppendLine($"  AND CurrentState IN ({string.Join(", ", states.Select(s => $"'{s}'"))})");

            if (offerings.Any())
                sb.AppendLine($"  AND OfferingName IN ({string.Join(", ", offerings.Select(o => $"'{Normalizer.Normalize(o).Replace("'", "''")}'"))})");

            if (skus.Any())
                sb.AppendLine($"  AND ProductSkuName IN ({string.Join(", ", skus.Select(sku => $"'{sku.Replace("'", "''")}'"))})");

            sb.AppendLine("EXCEPT");

            sb.AppendLine("SELECT DISTINCT OfferingName, ProductSkuName");
            sb.AppendLine("FROM dbo.products_info");
            sb.AppendLine($"WHERE {targetFilter}");
            sb.AppendLine($"  AND CurrentState IN ({string.Join(", ", states.Select(s => $"'{s}'"))})");

            if (offerings.Any())
                sb.AppendLine($"  AND OfferingName IN ({string.Join(", ", offerings.Select(o => $"'{Normalizer.Normalize(o).Replace("'", "''")}'"))})");

            if (skus.Any())
                sb.AppendLine($"  AND ProductSkuName IN ({string.Join(", ", skus.Select(sku => $"'{sku.Replace("'", "''")}'"))})");

            sb.AppendLine("ORDER BY OfferingName;");
            return sb.ToString();
        }

        // --- Symmetric Difference (Pivot View) ---
        private string BuildSymmetricComparison(
            IntentResponse intentResponse,
            List<string> states,
            List<string> offerings,
            List<string> skus)
        {
            var sb = new StringBuilder();

            var regions = intentResponse.Filters.RegionName ?? new List<string>();
            var geographies = intentResponse.Filters.GeographyName ?? new List<string>();
            var macros = intentResponse.Filters.MacroGeographyName ?? new List<string>();

            // --- Expand hierarchies ---
            if (geographies.Any())
            {
                var expanded = RegionCache.GetRegionsForGeographies(geographies);
                foreach (var r in expanded)
                    if (!regions.Contains(r, StringComparer.OrdinalIgnoreCase))
                        regions.Add(r);
            }

            if (macros.Any())
            {
                var expanded = RegionCache.GetRegionsForMacros(macros);
                foreach (var r in expanded)
                    if (!regions.Contains(r, StringComparer.OrdinalIgnoreCase))
                        regions.Add(r);
            }

            var totalScopes = regions.Count + geographies.Count + macros.Count;
            if (totalScopes < 2)
                throw new ArgumentException("Difference queries require at least two comparison values (regions, geographies, or macro-geographies).");

            // --- Determine comparison dimension ---
            string compareDimension;
            List<string> compareValues = new();

            if (regions.Any() && !geographies.Any() && !macros.Any())
            {
                compareDimension = "RegionName";
                compareValues.AddRange(regions);
            }
            else if (geographies.Any() && !regions.Any() && !macros.Any())
            {
                compareDimension = "GeographyName";
                compareValues.AddRange(geographies);
            }
            else if (macros.Any() && !regions.Any() && !geographies.Any())
            {
                compareDimension = "MacroGeographyName";
                compareValues.AddRange(macros);
            }
            else
            {
                compareDimension = "Scope";
                compareValues.AddRange(regions.Select(r => r));
                if (regions.Count == 0)
                {
                    compareValues.AddRange(geographies.Select(g => $"Country:{g}"));
                    compareValues.AddRange(macros.Select(m => $"MacroGeography:{m}"));
                }
            }

            // --- Outer SELECT ---
            sb.AppendLine("-- Symmetric Comparison Query (Pivot View)");
            sb.AppendLine("SELECT OfferingName AS [Product], ProductSkuName AS [SKU],");

            for (int i = 0; i < compareValues.Count; i++)
            {
                string value = compareValues[i].Replace("'", "''");
                sb.Append($"    CASE WHEN [{value}] = 1 THEN 'Yes' ELSE 'No' END AS [{value}]");
                sb.AppendLine(i < compareValues.Count - 1 ? "," : "");
            }

            sb.AppendLine("FROM (");

            // --- Inner SELECT ---
            sb.AppendLine("    SELECT DISTINCT OfferingName, ProductSkuName,");

            if (compareDimension == "Scope")
            {
                sb.AppendLine("           CASE");
                foreach (var r in regions)
                    sb.AppendLine($"               WHEN RegionName = '{r.Replace("'", "''")}' THEN '{r}'");
                if (regions.Count == 0)
                {
                    foreach (var g in geographies)
                        sb.AppendLine($"               WHEN GeographyName = '{g.Replace("'", "''")}' THEN 'Country:{g}'");
                    foreach (var m in macros)
                        sb.AppendLine($"               WHEN MacroGeographyName = '{m.Replace("'", "''")}' THEN 'MacroGeography:{m}'");
                }
                sb.AppendLine("           END AS Scope,");
            }
            else
            {
                sb.AppendLine($"           {compareDimension},");
            }

            sb.AppendLine("           1 AS OfferingExists");
            sb.AppendLine("    FROM dbo.products_info");

            // --- WHERE ---
            var whereClauses = new List<string>
            {
                $"CurrentState IN ({string.Join(", ", states.Select(s => $"'{s}'"))})"
            };

            if (compareDimension == "Scope")
            {
                var orConditions = new List<string>();
                if (regions.Any())
                    orConditions.Add($"RegionName IN ({string.Join(", ", regions.Select(v => $"'{v}'"))})");
                if (geographies.Any())
                    orConditions.Add($"GeographyName IN ({string.Join(", ", geographies.Select(v => $"'{v}'"))})");
                if (macros.Any())
                    orConditions.Add($"MacroGeographyName IN ({string.Join(", ", macros.Select(v => $"'{v}'"))})");

                if (orConditions.Any())
                    whereClauses.Add("(" + string.Join(" OR ", orConditions) + ")");
            }
            else
            {
                whereClauses.Add($"{compareDimension} IN ({string.Join(", ", compareValues.Select(v => $"'{v}'"))})");
            }

            sb.AppendLine("    WHERE " + string.Join(" AND ", whereClauses));

            if (offerings.Any())
                sb.AppendLine($"      AND OfferingName IN ({string.Join(", ", offerings.Select(o => $"'{Normalizer.Normalize(o).Replace("'", "''")}'"))})");

            if (skus.Any())
                sb.AppendLine($"      AND ProductSkuName IN ({string.Join(", ", skus.Select(sku => $"'{sku.Replace("'", "''")}'"))})");

            sb.AppendLine(") AS a");

            // --- PIVOT ---
            sb.AppendLine("PIVOT (");
            sb.AppendLine("    COUNT(OfferingExists)");
            sb.Append($"    FOR {compareDimension} IN (");
            sb.Append(string.Join(", ", compareValues.Select(v => $"[{v}]")));
            sb.AppendLine(")) AS Comparison");

            sb.AppendLine("ORDER BY OfferingName;");

            return sb.ToString();
        }

        /// <summary>
        /// Builds a SQL WHERE clause for a ComparisonScope, expanding
        /// 'Global' macros into all non-Government MacroGeographyName values.
        /// </summary>
        private string BuildScopedConditionWithGlobalAwareExpansion(ComparisonScope scope)
        {
            var safeValue = scope.ScopeValue.Replace("'", "''");

            if (scope.ScopeType.Equals("MacroGeographyName", StringComparison.OrdinalIgnoreCase)
                && safeValue.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                // Fetch all macros from region cache
                var allMacros = GetAllKnownMacros();
                var nonGovMacros = allMacros
                    .Where(m => !m.Contains("Gov", StringComparison.OrdinalIgnoreCase)
                             && !m.Contains("Government", StringComparison.OrdinalIgnoreCase)
                             && !m.Contains("DoD", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(m => $"'{m.Replace("'", "''")}'");

                return $"MacroGeographyName IN ({string.Join(", ", nonGovMacros)})";
            }

            // Normal case
            return scope.ScopeType switch
            {
                "RegionName" => $"RegionName = '{safeValue}'",
                "GeographyName" => $"GeographyName = '{safeValue}'",
                "MacroGeographyName" => $"MacroGeographyName = '{safeValue}'",
                _ => throw new ArgumentException($"Unknown scope type: {scope.ScopeType}")
            };
        }

        /// <summary>
        /// Helper to retrieve all known MacroGeographyName values
        /// from the region hierarchy cache.
        /// </summary>
        private IEnumerable<string> GetAllKnownMacros()
        {
            var allMacros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allRegions = RegionCache.GetAllRegions();

            foreach (var region in allRegions)
            {
                if (RegionCache.TryGetParentGeography(region, out var _, out var macro)
                    && !string.IsNullOrWhiteSpace(macro))
                {
                    allMacros.Add(macro);
                }
            }

            return allMacros;
        }
    }
}
