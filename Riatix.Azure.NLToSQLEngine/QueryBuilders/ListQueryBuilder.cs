using System.Text;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{
    public class ListQueryBuilder : BaseQueryBuilder
    {
        public ListQueryBuilder(
            IServiceNameNormalizer normalizer,
            IRegionHierarchyCache regionHierarchyCache,
            IProductCategoryMap productCategoryMap)
            : base(normalizer, regionHierarchyCache, productCategoryMap)
        {
        }

        public override bool CanHandle(IntentResponse intent)
        {
            if (!intent.Intent.Equals("list", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(intent.Parameters?.GroupBy))
                return false;

            if (!string.IsNullOrEmpty(intent.Parameters?.CountDistinct))
                return false;

            return true;
        }

        public override string BuildQuery(IntentResponse intent)
        {
            var sb = new StringBuilder();
            const string tableName = "dbo.products_info";

            // --Filters ---
            var filters = CollectFilters(intent);
            string whereClause = filters.Count > 0
                ? "WHERE " + string.Join(" AND ", filters)
                : string.Empty;

            // --Projection inference ---
            var selectColumns = InferSelectColumns(intent);

            var projectedColumns = selectColumns
                .Select(col => ColumnAliases.ContainsKey(col) ? ColumnAliases[col] : col)
                .ToList();

            int topN = intent.Parameters?.TopN ?? 100;

            sb.AppendLine($"SELECT {string.Join(", ", projectedColumns)}");
            sb.AppendLine($"FROM {tableName}");
            if (!string.IsNullOrWhiteSpace(whereClause))
                sb.AppendLine(whereClause);
            sb.AppendLine($"ORDER BY {string.Join(", ", selectColumns)};");

            return sb.ToString();
        }

        private List<string> CollectFilters(IntentResponse intent)
        {
            var f = new List<string>();
            var offerings = new List<string>(intent.Filters.OfferingName ?? new());
            ExpandProductCategories(offerings, intent.Filters.ProductCategoryName ?? new(), intent);

            if (intent.Filters.RegionName.Any())
                f.Add($"RegionName IN ({string.Join(",", intent.Filters.RegionName.Select(v => $"'{v}'"))})");
            if (intent.Filters.GeographyName.Any())
                f.Add($"GeographyName IN ({string.Join(",", intent.Filters.GeographyName.Select(v => $"'{v}'"))})");
            if (intent.Filters.MacroGeographyName.Any())
                f.Add($"MacroGeographyName IN ({string.Join(",", intent.Filters.MacroGeographyName.Select(v => $"'{v}'"))})");
            if (offerings.Any())
                f.Add($"OfferingName IN ({string.Join(",", offerings.Select(v => $"'{Normalizer.Normalize(v)}'"))})");
            if (intent.Filters.ProductSkuName.Any())
                f.Add($"ProductSkuName IN ({string.Join(",", intent.Filters.ProductSkuName.Select(v => $"'{v}'"))})");
            if (intent.Filters.CurrentState.Any())
                f.Add($"CurrentState IN ({string.Join(",", intent.Filters.CurrentState.Select(v => $"'{v}'"))})");

            return f;
        }

        private List<string> InferSelectColumns(IntentResponse intent)
        {
            var cols = new List<string>();
            int regionCount = intent.Filters.RegionName.Count;
            int geoCount = intent.Filters.GeographyName.Count;
            int macroCount = intent.Filters.MacroGeographyName.Count;
            int offerCount = intent.Filters.OfferingName.Count;
            int skuCount = intent.Filters.ProductSkuName.Count;

            if (!string.IsNullOrEmpty(intent.Parameters.GroupBy))
            {
                string g = intent.Parameters.GroupBy.ToLowerInvariant();
                if (g.Contains("macro"))
                    return new() { "MacroGeographyName" };
                if (g.Contains("geo"))
                    return new() { "GeographyName", "RegionName" };
                if (g.Contains("region"))
                    return new() { "RegionName", "GeographyName" };
            }

            if (!string.IsNullOrEmpty(intent.Parameters.CountDistinct))
            {
                string c = intent.Parameters.CountDistinct.ToLowerInvariant();
                if (c.Contains("offering"))
                    return new() { "OfferingName" };
                if (c.Contains("sku"))
                    return new() { "OfferingName", "ProductSkuName" };
            }

            if (offerCount > 0 && skuCount == 0)
            {
                cols.AddRange(new[] { "OfferingName", "ProductSkuName", "GeographyName", "RegionName", "CurrentState" });
            }
            else
            {
                cols.AddRange(new[] { "OfferingName", "ProductSkuName", "GeographyName", "RegionName", "CurrentState" });
            }

            return cols.Distinct()
                .OrderBy(c => c switch
                {
                    "OfferingName" => 1,
                    "ProductSkuName" => 2,
                    "GeographyName" => 3,
                    "RegionName" => 4,
                    "CurrentState" => 5,
                    "MacroGeographyName" => 6,
                    _ => 99
                })
                .ToList();
        }
    }
}
