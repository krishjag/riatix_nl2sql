using System.Text;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{

    public class IntersectionQueryBuilderHavingSemantic : BaseQueryBuilder
    {
        public IntersectionQueryBuilderHavingSemantic(
            IServiceNameNormalizer normalizer,
            IRegionHierarchyCache regionHierarchyCache,
            IProductCategoryMap productCategoryMap)
            : base(normalizer, regionHierarchyCache, productCategoryMap)
        {
        }

        public override bool CanHandle(IntentResponse intent) =>
            intent.Intent.Equals("intersection_having_semantic", StringComparison.OrdinalIgnoreCase);

        public override string BuildQuery(IntentResponse intent)
        {
            // --- Step 1: Expand all scopes down to regions ---
            var expandedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var regions = intent.Filters.RegionName ?? new();
            var geographies = intent.Filters.GeographyName ?? new();
            var macros = ExpandGlobalMacros(intent.Filters.MacroGeographyName ?? new());

            // Always include explicitly mentioned regions
            foreach (var r in regions)
                expandedRegions.Add(r);

            // Expand macros and geographies
            foreach (var r in RegionCache.GetRegionsForGeographies(geographies))
                expandedRegions.Add(r);

            foreach (var r in RegionCache.GetRegionsForMacros(macros))
                expandedRegions.Add(r);

            if (expandedRegions.Count < 2)
                throw new ArgumentException("Intersection queries require at least two distinct scopes (regions, geographies, or macro-geographies).");

            // --- Step 2: Resolve states, offerings, and SKUs ---
            var states = intent.Filters.CurrentState?.Any() == true
                ? intent.Filters.CurrentState
                : new List<string> { "GA" };

            var offerings = new List<string>(intent.Filters.OfferingName ?? new());
            ExpandProductCategories(offerings, intent.Filters.ProductCategoryName ?? new(), intent);

            var skus = intent.Filters.ProductSkuName ?? new List<string>();

            // --- Step 3: Build SQL ---
            var sb = new StringBuilder();
            sb.AppendLine("-- Intersection Query [HAVING COUNT instead of SQL INTERSECT for multi-scope consistency]");
            sb.AppendLine();

            sb.AppendLine("WITH RegionalData AS (");
            sb.AppendLine("    SELECT DISTINCT OfferingName, ProductSkuName, RegionName");
            sb.AppendLine("    FROM dbo.products_info");

            // Build WHERE clause
            var whereClauses = new List<string>
            {
                $"RegionName IN ({string.Join(", ", expandedRegions.Select(r => $"'{r.Replace("'", "''")}'"))})",
                $"CurrentState IN ({string.Join(", ", states.Select(s => $"'{s.Replace("'", "''")}'"))})"
            };

            if (offerings.Any())
                whereClauses.Add($"OfferingName IN ({string.Join(", ", offerings.Select(o => $"'{Normalizer.Normalize(o).Replace("'", "''")}'"))})");

            if (skus.Any())
                whereClauses.Add($"ProductSkuName IN ({string.Join(", ", skus.Select(sku => $"'{sku.Replace("'", "''")}'"))})");

            sb.AppendLine("    WHERE " + string.Join(" AND ", whereClauses));
            sb.AppendLine(")");

            sb.AppendLine("SELECT OfferingName AS [Product], ProductSkuName AS [Product SKU]");
            sb.AppendLine("FROM RegionalData");
            sb.AppendLine("GROUP BY OfferingName, ProductSkuName");
            sb.AppendLine($"HAVING COUNT(DISTINCT RegionName) = {expandedRegions.Count}");
            sb.AppendLine("ORDER BY OfferingName;");

            return sb.ToString();
        }

        /// <summary>
        /// Expands 'Global' macros to include all non-Government macro-geographies.
        /// </summary>
        private IEnumerable<string> ExpandGlobalMacros(IEnumerable<string> macros)
        {
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var macro in macros)
            {
                if (macro.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    var allMacros = RegionCache.GetRegionsForMacros(new[] { "All" }) ?? new List<string>();
                    foreach (var m in allMacros)
                    {
                        if (!m.Contains("Gov", StringComparison.OrdinalIgnoreCase) &&
                            !m.Contains("Government", StringComparison.OrdinalIgnoreCase) &&
                            !m.Contains("DoD", StringComparison.OrdinalIgnoreCase))
                        {
                            expanded.Add(m);
                        }
                    }
                }
                else
                {
                    expanded.Add(macro);
                }
            }

            return expanded;
        }
    }    
}
