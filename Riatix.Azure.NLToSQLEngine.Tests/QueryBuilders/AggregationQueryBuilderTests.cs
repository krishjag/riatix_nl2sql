using Moq;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.QueryBuilders;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.QueryBuilders
{
    public class AggregationQueryBuilderTests
    {
        private static (AggregationQueryBuilder sut, Mock<IServiceNameNormalizer> norm, Mock<IRegionHierarchyCache> region, Mock<IProductCategoryMap> cat) CreateSut()
        {
            var normalizer = new Mock<IServiceNameNormalizer>(MockBehavior.Loose);
            normalizer.Setup(n => n.Normalize(It.IsAny<string>()))
            .Returns<string>(s => s); // identity by default

            var regionCache = new Mock<IRegionHierarchyCache>(MockBehavior.Loose);

            var categories = new Mock<IProductCategoryMap>(MockBehavior.Loose);
            categories.Setup(c => c.ContainsCategory(It.IsAny<string>())).Returns(false);
            categories.Setup(c => c.GetOfferingsForCategory(It.IsAny<string>())).Returns(Array.Empty<string>());

            var sut = new AggregationQueryBuilder(normalizer.Object, regionCache.Object, categories.Object);
            return (sut, normalizer, regionCache, categories);
        }

        private static IntentResponse NewIntent(string intent = "aggregation")
        {
            return new IntentResponse
            {
                Intent = intent,
                Filters = new Filters(),
                Parameters = new Parameters()
            };
        }

        // CanHandle
        [Fact]
        public void CanHandle_AggregationIntent_ReturnsTrue()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent("aggregation");

            Assert.True(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_ListWithGroupBy_ReturnsTrue()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent("list");
            intent.Parameters.GroupBy = "OfferingName";

            Assert.True(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_ListWithCountDistinct_ReturnsTrue()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent("list");
            intent.Parameters.CountDistinct = "ProductSkuName";

            Assert.True(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_ListWithoutParams_ReturnsFalse()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent("list");

            Assert.False(sut.CanHandle(intent));
        }

        // BuildQuery basics
        [Fact]
        public void BuildQuery_Defaults_GroupByRegion_WithDefaultGA()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent(); // no filters/params

            var sql = sut.BuildQuery(intent);

            Assert.Contains("-- Aggregation Data", sql);
            Assert.Contains("SELECT RegionName, COUNT(*) AS ServiceCount", sql);
            Assert.Contains("FROM dbo.products_info", sql);
            Assert.Contains("WHERE CurrentState = 'GA'", sql);
            Assert.Contains("GROUP BY RegionName", sql);
            Assert.Contains("ORDER BY RegionName;", sql);
        }

        [Fact]
        public void BuildQuery_GroupByAndCountDistinct_UsesProvidedColumns()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "OfferingName";
            intent.Parameters.CountDistinct = "ProductSkuName";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT OfferingName, COUNT(DISTINCT ProductSkuName) AS DistinctCount", sql);
            Assert.Contains("GROUP BY OfferingName", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        [Fact]
        public void BuildQuery_IncludesRegionGeographyMacroFilters()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Filters.RegionName.AddRange(new[] { "East US", "West US" });
            intent.Filters.GeographyName.AddRange(new[] { "United States", "France" });
            intent.Filters.MacroGeographyName.Add("Europe");

            var sql = sut.BuildQuery(intent);

            string noSpaceSql = sql.Replace(" ", string.Empty);
            Assert.Contains("RegionNameIN('EastUS','WestUS')".Replace(" ", string.Empty), noSpaceSql);
            Assert.Contains("GeographyNameIN('UnitedStates','France')".Replace(" ", string.Empty), noSpaceSql);
            Assert.Contains("MacroGeographyNameIN('Europe')".Replace(" ", string.Empty), noSpaceSql);
        }

        [Fact]
        public void BuildQuery_IncludesOfferings_WithNormalization_AndClarificationWhenCategoryAlsoProvided()
        {
            var (sut, norm, _, cat) = CreateSut();
            norm.Setup(n => n.Normalize("Azure SQL")).Returns("Azure SQL Database");
            cat.Setup(c => c.ContainsCategory("Databases")).Returns(true);
            cat.Setup(c => c.GetOfferingsForCategory("Databases")).Returns(new[] { "Azure SQL", "Azure Cosmos DB" });

            var intent = NewIntent();
            intent.Filters.OfferingName.Add("Azure SQL");
            intent.Filters.ProductCategoryName.Add("Databases");

            var sql = sut.BuildQuery(intent);

            Assert.Contains("OfferingName IN ('Azure SQL Database')".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            Assert.Contains("OfferingName specified; ProductCategoryName retained for traceability only.", intent.Clarifications);
        }

        [Fact]
        public void BuildQuery_ExpandsCategories_WhenNoOfferingsProvided_NormalizesExpanded()
        {
            var (sut, norm, _, cat) = CreateSut();
            cat.Setup(c => c.ContainsCategory("Databases")).Returns(true);
            cat.Setup(c => c.GetOfferingsForCategory("Databases")).Returns(new[] { "Azure SQL", "Azure Cosmos DB" });
            norm.Setup(n => n.Normalize(It.IsAny<string>())).Returns<string>(s => s + " (N)");

            var intent = NewIntent();
            intent.Filters.ProductCategoryName.Add("Databases");

            var sql = sut.BuildQuery(intent);

            Assert.Contains("OfferingName IN (", sql);
            Assert.Contains("'Azure SQL (N)'", sql);
            Assert.Contains("'Azure Cosmos DB (N)'", sql);
        }

        [Fact]
        public void BuildQuery_IncludesSkuAndStateFilters_UsesINForStateWhenProvided()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Filters.ProductSkuName.AddRange(new[] { "Standard", "Premium" });
            intent.Filters.CurrentState.AddRange(new[] { "GA", "Preview" });

            var sql = sut.BuildQuery(intent);

            string noSpaceSql = sql.Replace(" ", string.Empty);
            Assert.Contains("ProductSkuNameIN('Standard','Premium')".Replace(" ", string.Empty), noSpaceSql);
            Assert.Contains("CurrentStateIN('GA','Preview')".Replace(" ", string.Empty), noSpaceSql);
            Assert.DoesNotContain("WHERE CurrentState = 'GA'", sql); // default GA should not appear
        }

        [Fact]
        public void BuildQuery_GroupAndOrderMatch()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "GeographyName";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("GROUP BY GeographyName", sql);
            Assert.Contains("ORDER BY GeographyName;", sql);
        }

        // HAVING clause tests (now list-based)
        [Fact]
        public void BuildQuery_Having_WithValidOperator_UsesCountStarAndOperator()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = ">", Threshold = 5 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("GROUP BY RegionName", sql);
            Assert.Contains("HAVING COUNT(*) > 5", sql);
            Assert.Contains("ORDER BY RegionName;", sql);
        }

        [Fact]
        public void BuildQuery_Having_WithCountDistinct_UsesDistinctInHaving()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "OfferingName";
            intent.Parameters.CountDistinct = "ProductSkuName";
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = ">=", Threshold = 10 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT OfferingName, COUNT(DISTINCT ProductSkuName) AS DistinctCount", sql);
            Assert.Contains("GROUP BY OfferingName", sql);
            Assert.Contains("HAVING COUNT(DISTINCT ProductSkuName) >= 10", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        [Fact]
        public void BuildQuery_Having_WithInvalidOperator_DefaultsToGreaterThan()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = "not-an-op", Threshold = 3 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("HAVING COUNT(*) > 3", sql);
        }

        [Fact]
        public void BuildQuery_Having_WithWhitespaceOperator_DefaultsToGreaterThan()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = "   ", Threshold = 7 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("HAVING COUNT(*) > 7", sql);
        }

        [Fact]
        public void BuildQuery_Having_NotProvided_DoesNotIncludeHaving()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            // intent.Parameters.HavingCondition left null (or could set to empty list)

            var sql = sut.BuildQuery(intent);

            Assert.DoesNotContain("HAVING", sql);
        }

        [Fact]
        public void BuildQuery_Having_WithEmptyOperator_DefaultsToGreaterThan_IncludesHaving()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = "", Threshold = 100 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("HAVING COUNT(*) > 100", sql);
        }

        // Additional coverage

        [Fact]
        public void BuildQuery_Having_WithNotEqualOperator_IsAllowed()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "OfferingName";
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = "!=", Threshold = 2 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("GROUP BY OfferingName", sql);
            Assert.Contains("HAVING COUNT(*) != 2", sql);
        }

        [Fact]
        public void BuildQuery_CategoryExpansion_DedupesCaseInsensitive()
        {
            var (sut, norm, _, cat) = CreateSut();
            // Normalize adds (N) to make it visible in output
            norm.Setup(n => n.Normalize(It.IsAny<string>())).Returns<string>(s => s + " (N)");

            // Two categories that map to overlapping offerings (case variations)
            cat.Setup(c => c.ContainsCategory("CatA")).Returns(true);
            cat.Setup(c => c.ContainsCategory("CatB")).Returns(true);
            cat.Setup(c => c.GetOfferingsForCategory("CatA")).Returns(new[] { "Azure SQL", "Azure Cosmos DB" });
            cat.Setup(c => c.GetOfferingsForCategory("CatB")).Returns(new[] { "azure sql", "Azure Databricks" });

            var intent = NewIntent();
            intent.Filters.ProductCategoryName.AddRange(new[] { "CatA", "CatB" });

            var sql = sut.BuildQuery(intent);

            // Ensure only one 'Azure SQL' entry after case-insensitive de-dup
            var compact = sql.Replace(" ", string.Empty);
            Assert.Contains("OfferingNameIN('AzureSQL(N)','AzureCosmosDB(N)','AzureDatabricks(N)')".Replace(" ", string.Empty), compact);
        }

        [Fact]
        public void BuildQuery_Having_TwoBounds_ProducesBetween()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = ">", Threshold = 3 },
                new HavingCondition { Operator = "<=", Threshold = 10 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("HAVING COUNT(*) BETWEEN 3 AND 10", sql);
        }

        [Fact]
        public void BuildQuery_Having_TwoNonBounds_FallsBackToAnd()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = ">", Threshold = 3 },
                new HavingCondition { Operator = ">", Threshold = 10 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("HAVING COUNT(*) > 3 AND COUNT(*) > 10", sql);
        }

        [Fact]
        public void BuildQuery_Having_MultipleConditions_AreAndCombined()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.HavingCondition = new List<HavingCondition>
            {
                new HavingCondition { Operator = ">", Threshold = 5 },
                new HavingCondition { Operator = "<>", Threshold = 7 },
                new HavingCondition { Operator = "<=", Threshold = 12 }
            };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("HAVING COUNT(*) > 5 AND COUNT(*) <> 7 AND COUNT(*) <= 12", sql);
        }
    }
}