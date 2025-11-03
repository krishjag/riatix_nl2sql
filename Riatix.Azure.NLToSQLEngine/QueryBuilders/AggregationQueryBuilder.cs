using System.Text;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{
    public class AggregationQueryBuilder : BaseQueryBuilder
    {
        public AggregationQueryBuilder(
            IServiceNameNormalizer normalizer,
            IRegionHierarchyCache regionHierarchyCache,
            IProductCategoryMap productCategoryMap)
            : base(normalizer, regionHierarchyCache, productCategoryMap)
        {
        }

        public override bool CanHandle(IntentResponse intent)
        {
            if (intent.Intent.Equals("aggregation", StringComparison.OrdinalIgnoreCase))
                return true;

            if (intent.Intent.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(intent.Parameters?.GroupBy) ||
                    !string.IsNullOrEmpty(intent.Parameters?.CountDistinct))
                    return true;
            }

            return false;
        }

        public override string BuildQuery(IntentResponse intent)
        {
            var sb = new StringBuilder();
            string groupBy = !string.IsNullOrEmpty(intent.Parameters?.GroupBy)
                ? intent.Parameters.GroupBy!
                : "RegionName";

            var filters = new List<string>();
            var offerings = new List<string>(intent.Filters.OfferingName ?? new());
            ExpandProductCategories(offerings, intent.Filters.ProductCategoryName ?? new(), intent);

            if (intent.Filters.RegionName.Count > 0)
                filters.Add($"RegionName IN ({string.Join(",", intent.Filters.RegionName.Select(v => $"'{v}'"))})");
            if (intent.Filters.GeographyName.Count > 0)
                filters.Add($"GeographyName IN ({string.Join(",", intent.Filters.GeographyName.Select(v => $"'{v}'"))})");
            if (intent.Filters.MacroGeographyName.Count > 0)
                filters.Add($"MacroGeographyName IN ({string.Join(",", intent.Filters.MacroGeographyName.Select(v => $"'{v}'"))})");
            if (offerings.Any())
                filters.Add($"OfferingName IN ({string.Join(",", offerings.Select(v => $"'{Normalizer.Normalize(v)}'"))})");
            if (intent.Filters.ProductSkuName.Count > 0)
                filters.Add($"ProductSkuName IN ({string.Join(",", intent.Filters.ProductSkuName.Select(v => $"'{v}'"))})");
            if (intent.Filters.CurrentState.Count > 0)
                filters.Add($"CurrentState IN ({string.Join(",", intent.Filters.CurrentState.Select(v => $"'{v}'"))})");
            else
                filters.Add("CurrentState = 'GA'");

            string whereClause = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty;

            sb.AppendLine("-- Aggregation Data");

            // Determine aggregation expression
            string countExpr = !string.IsNullOrEmpty(intent.Parameters?.CountDistinct)
                ? $"COUNT(DISTINCT {intent.Parameters.CountDistinct})"
                : "COUNT(*)";

            string countAlias = !string.IsNullOrEmpty(intent.Parameters?.CountDistinct)
                ? "DistinctCount"
                : "ServiceCount";

            sb.AppendLine($"SELECT {groupBy}, {countExpr} AS {countAlias}");
            sb.AppendLine("FROM dbo.products_info");
            sb.AppendLine(whereClause);
            sb.AppendLine($"GROUP BY {groupBy}");

            // --- HAVING clause support ---
            var havingConditions = intent.Parameters?.HavingCondition ?? new List<HavingCondition>();
            if (havingConditions.Count > 0)
            {
                var validOps = new[] { "=", ">", "<", ">=", "<=", "<>", "!=" };
                var havingFragments = new List<string>();

                // Special case: two conditions that can form a BETWEEN
                if (havingConditions.Count == 2)
                {
                    var c1 = havingConditions[0];
                    var c2 = havingConditions[1];

                    // Detect lower/upper bound pattern for BETWEEN
                    bool isLower = c1.Operator.Contains(">") || c1.Operator.Contains("≥");
                    bool isUpper = c2.Operator.Contains("<") || c2.Operator.Contains("≤");

                    if (isLower && isUpper)
                    {
                        int lower = Math.Min(c1.Threshold, c2.Threshold);
                        int upper = Math.Max(c1.Threshold, c2.Threshold);
                        sb.AppendLine($"HAVING {countExpr} BETWEEN {lower} AND {upper}");
                    }
                    else
                    {
                        // Fallback to separate conditions
                        foreach (var cond in havingConditions)
                        {
                            var op = validOps.Contains(cond.Operator) ? cond.Operator : ">";
                            havingFragments.Add($"{countExpr} {op} {cond.Threshold}");
                        }
                        sb.AppendLine("HAVING " + string.Join(" AND ", havingFragments));
                    }
                }
                else
                {
                    // Single or multiple distinct HAVING conditions
                    foreach (var cond in havingConditions)
                    {
                        var op = validOps.Contains(cond.Operator) ? cond.Operator : ">";
                        havingFragments.Add($"{countExpr} {op} {cond.Threshold}");
                    }

                    if (havingFragments.Count > 0)
                        sb.AppendLine("HAVING " + string.Join(" AND ", havingFragments));
                }
            }

            sb.AppendLine($"ORDER BY {groupBy};");
            return sb.ToString();
        }
    }
}
