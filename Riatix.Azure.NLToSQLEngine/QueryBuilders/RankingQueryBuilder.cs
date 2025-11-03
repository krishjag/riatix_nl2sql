using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{
    /// <summary>
    /// Builds SQL queries that rank Azure services or SKUs by counts or frequency,
    /// supporting Top-N, CountDistinct, grouping, and SortOrder semantics.
    /// </summary>
    public class RankingQueryBuilder : BaseQueryBuilder
    {
        public RankingQueryBuilder(
            IServiceNameNormalizer normalizer,
            IRegionHierarchyCache regionHierarchyCache,
            IProductCategoryMap productCategoryMap)
            : base(normalizer, regionHierarchyCache, productCategoryMap)
        {
        }

        public override bool CanHandle(IntentResponse intent) =>
            intent.Intent.Equals("ranking", StringComparison.OrdinalIgnoreCase);

        public override string BuildQuery(IntentResponse intent)
        {
            var sb = new StringBuilder();

            // --- Grouping logic ---
            string groupBy = !string.IsNullOrEmpty(intent.Parameters?.GroupBy)
                ? intent.Parameters.GroupBy!
                : "RegionName";

            string groupBySelect = ColumnAliases.ContainsKey(groupBy)
                ? ColumnAliases[groupBy]
                : $"{groupBy} AS [{groupBy}]";

            // Default Top-N to 20 for ranking unless explicitly provided
            int topValue = (intent.Parameters?.TopN is > 0) ? intent.Parameters!.TopN!.Value : 20;
            string topN = $"TOP {topValue}";

            // --- Determine sort direction ---
            string sortOrder = intent.Parameters?.SortOrder?.Trim().ToLowerInvariant() switch
            {
                "ascending" or "asc" => "ASC",
                "descending" or "desc" => "DESC",
                _ => "DESC"
            };

            // --- Determine count expression ---
            bool useDistinct = !string.IsNullOrEmpty(intent.Parameters?.CountDistinct);
            string countExpr = useDistinct
                ? $"COUNT(DISTINCT {intent.Parameters!.CountDistinct})"
                : "COUNT(*)";

            string countAlias = useDistinct ? "Distinct Count" : "Service Count";

            // --- Filters ---
            var filters = new List<string>();
            var offerings = new List<string>(intent.Filters.OfferingName ?? new());
            ExpandProductCategories(offerings, intent.Filters.ProductCategoryName ?? new(), intent);
            var skus = intent.Filters.ProductSkuName ?? new();

            if (intent.Filters.RegionName.Count > 0)
                filters.Add($"RegionName IN ({string.Join(",", intent.Filters.RegionName.Select(v => $"'{v}'"))})");

            if (intent.Filters.GeographyName.Count > 0)
                filters.Add($"GeographyName IN ({string.Join(",", intent.Filters.GeographyName.Select(v => $"'{v}'"))})");

            if (intent.Filters.MacroGeographyName.Count > 0)
                filters.Add($"MacroGeographyName IN ({string.Join(",", intent.Filters.MacroGeographyName.Select(v => $"'{v}'"))})");

            if (offerings.Any())
                filters.Add($"OfferingName IN ({string.Join(",", offerings.Select(v => $"'{Normalizer.Normalize(v)}'"))})");

            if (skus.Any())
                filters.Add($"ProductSkuName IN ({string.Join(",", skus.Select(v => $"'{v}'"))})");

            if (intent.Filters.CurrentState.Count > 0)
                filters.Add($"CurrentState IN ({string.Join(",", intent.Filters.CurrentState.Select(v => $"'{v}'"))})");
            else
                filters.Add("CurrentState = 'GA'");

            string whereClause = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty;

            // --- SQL Assembly ---
            sb.AppendLine("-- Ranking Data");
            sb.AppendLine($"SELECT {topN} {groupBySelect}, {countExpr} AS [{countAlias}],");
            sb.AppendLine($"       RANK() OVER (ORDER BY {countExpr} {sortOrder}) AS [Rank Id]");
            sb.AppendLine("FROM dbo.products_info");
            if (!string.IsNullOrWhiteSpace(whereClause))
                sb.AppendLine(whereClause);
            sb.AppendLine($"GROUP BY {groupBy}");
            sb.AppendLine($"ORDER BY [{countAlias}] {sortOrder};");

            return sb.ToString();
        }
    }
}
