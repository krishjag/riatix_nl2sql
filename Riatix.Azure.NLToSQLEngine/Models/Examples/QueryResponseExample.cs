using Riatix.Azure.NLToSQLEngine.Models;
using Swashbuckle.AspNetCore.Filters;

namespace Riatix.Azure.NLToSQLEngine.Models.Examples
{
    public class QueryResponseMultipleExamples : IMultipleExamplesProvider<QueryResponse>
    {
        public IEnumerable<SwaggerExample<QueryResponse>> GetExamples()
        {
            yield return SwaggerExample.Create(
                "Difference Pattern",
                new QueryResponse
                {
                    Intent = "DifferencePattern",
                    NaturalLanguageSummary = "Compares GA services available in East US and Japan East.",
                    Sql = "SELECT RegionName, OfferingName, CurrentState FROM dbo.products_info WHERE RegionName IN ('East US','Japan East') AND CurrentState='GA';",
                    Clarifications = new[] { "GA means General Availability." }.ToList(),
                    ResultSets = new List<List<Dictionary<string, object>>>
                    {
                        new()
                        {
                            new Dictionary<string, object>
                            {
                                { "RegionName", "East US" },
                                { "OfferingName", "API Management" },
                                { "CurrentState", "GA" }
                            },
                            new Dictionary<string, object>
                            {
                                { "RegionName", "Japan East" },
                                { "OfferingName", "API Management" },
                                { "CurrentState", "Preview" }
                            }
                        }
                    }
                }
            );

            yield return SwaggerExample.Create(
                "Ranking Pattern",
                new QueryResponse
                {
                    Intent = "RankingPattern",
                    NaturalLanguageSummary = "Ranks top 5 regions with the most GA services in Europe.",
                    Sql = "SELECT TOP 5 RegionName, COUNT(DISTINCT OfferingName) AS GAServiceCount FROM dbo.products_info WHERE GeographyName = 'Europe' AND CurrentState = 'GA' GROUP BY RegionName ORDER BY GAServiceCount DESC;",
                    ResultSets = new List<List<Dictionary<string, object>>>
                    {
                        new()
                        {
                            new Dictionary<string, object> { { "RegionName", "West Europe" }, { "GAServiceCount", 225 } },
                            new Dictionary<string, object> { { "RegionName", "North Europe" }, { "GAServiceCount", 214 } },
                            new Dictionary<string, object> { { "RegionName", "Germany West Central" }, { "GAServiceCount", 203 } }
                        }
                    }
                }
            );

            yield return SwaggerExample.Create(
                "Summary Pattern",
                new QueryResponse
                {
                    Intent = "SummaryPattern",
                    NaturalLanguageSummary = "Summarizes total number of GA, Preview, and Retired services across all Azure regions.",
                    Sql = "SELECT CurrentState, COUNT(DISTINCT OfferingName) AS ServiceCount FROM dbo.products_info GROUP BY CurrentState;",
                    ResultSets = new List<List<Dictionary<string, object>>>
                    {
                        new()
                        {
                            new Dictionary<string, object> { { "CurrentState", "GA" }, { "ServiceCount", 1550 } },
                            new Dictionary<string, object> { { "CurrentState", "Preview" }, { "ServiceCount", 120 } },
                            new Dictionary<string, object> { { "CurrentState", "Retired" }, { "ServiceCount", 42 } }
                        }
                    }
                }
            );
        }
    }
}
