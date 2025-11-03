using Moq;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.QueryBuilders;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.QueryBuilders
{
    public class RankingQueryBuilderTests
    {
        private static (RankingQueryBuilder sut, Mock<IServiceNameNormalizer> norm, Mock<IRegionHierarchyCache> region, Mock<IProductCategoryMap> cat) CreateSut()
        {
            var normalizer = new Mock<IServiceNameNormalizer>(MockBehavior.Loose);
            normalizer.Setup(n => n.Normalize(It.IsAny<string>()))
            .Returns<string>(s => s);

            var regionCache = new Mock<IRegionHierarchyCache>(MockBehavior.Loose);

            var categories = new Mock<IProductCategoryMap>(MockBehavior.Loose);
            categories.Setup(c => c.ContainsCategory(It.IsAny<string>())).Returns(false);
            categories.Setup(c => c.GetOfferingsForCategory(It.IsAny<string>())).Returns(Array.Empty<string>());

            var sut = new RankingQueryBuilder(normalizer.Object, regionCache.Object, categories.Object);
            return (sut, normalizer, regionCache, categories);
        }

        private static IntentResponse NewIntent(string intent = "ranking")
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
        public void CanHandle_RankingIntent_ReturnsTrue()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent("ranking");

            Assert.True(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_NonRankingIntent_ReturnsFalse()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent("list");

            Assert.False(sut.CanHandle(intent));
        }

        // BuildQuery basics
        [Fact]
        public void BuildQuery_Defaults_GroupByRegion_Top20_WithDefaultGA()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();

            var sql = sut.BuildQuery(intent);

            Assert.Contains("-- Ranking Data", sql);
            Assert.Contains("SELECT TOP 20 RegionName [Region], COUNT(*) AS [Service Count]", sql);
            Assert.Contains("RANK() OVER (ORDER BY COUNT(*) DESC) AS [Rank Id]", sql);
            Assert.Contains("FROM dbo.products_info", sql);
            Assert.Contains("WHERE CurrentState = 'GA'", sql);
            Assert.Contains("GROUP BY RegionName", sql);
            Assert.Contains("ORDER BY [Service Count] DESC;", sql);
        }

        [Fact]
        public void BuildQuery_CustomTopNAndGroupBy_UsesAliasAndOrderByCountDesc()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.TopN = 5;
            intent.Parameters.GroupBy = "GeographyName"; // exact key to hit alias

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT TOP 5 GeographyName [Geography], COUNT(*) AS [Service Count]", sql);
            Assert.Contains("GROUP BY GeographyName", sql);
            Assert.Contains("ORDER BY [Service Count] DESC;", sql);
        }

        [Fact]
        public void BuildQuery_GroupByUnknown_UsesRawColumnAliasSyntax()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "SomeUnknown";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT TOP 20 SomeUnknown AS [SomeUnknown], COUNT(*) AS [Service Count]", sql);
            Assert.Contains("GROUP BY SomeUnknown", sql);
        }

        // Filters
        [Fact]
        public void BuildQuery_IncludesRegionGeographyMacroFilters()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Filters.RegionName.AddRange(new[] { "East US", "West US" });
            intent.Filters.GeographyName.AddRange(new[] { "United States", "France" });
            intent.Filters.MacroGeographyName.Add("Europe");

            var sql = sut.BuildQuery(intent);

            string noSpace = sql.Replace(" ", string.Empty);
            Assert.Contains("RegionNameIN('EastUS','WestUS')".Replace(" ", string.Empty), noSpace);
            Assert.Contains("GeographyNameIN('UnitedStates','France')".Replace(" ", string.Empty), noSpace);
            Assert.Contains("MacroGeographyNameIN('Europe')".Replace(" ", string.Empty), noSpace);
            // Still includes default GA when CurrentState not provided
            Assert.Contains("CurrentState='GA'".Replace(" ", string.Empty), noSpace);
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

            string noSpace = sql.Replace(" ", string.Empty);
            Assert.Contains("ProductSkuNameIN('Standard','Premium')".Replace(" ", string.Empty), noSpace);
            Assert.Contains("CurrentStateIN('GA','Preview')".Replace(" ", string.Empty), noSpace);
        }

        [Fact]
        public void BuildQuery_UsesAliasForCommonGroupByColumns()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "ProductSkuName"; // has alias in ColumnAliases

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT TOP 20 ProductSkuName [Product SKU], COUNT(*) AS [Service Count]", sql);
            Assert.Contains("GROUP BY ProductSkuName", sql);
        }

        [Fact]
        public void BuildQuery_ContainsRankingWindowFunction()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();

            var sql = sut.BuildQuery(intent);

            Assert.Contains("RANK() OVER (ORDER BY COUNT(*) DESC) AS [Rank Id]", sql);
        }

        // Sort order handling
        [Fact]
        public void BuildQuery_SortOrderAscending_AppliesAscToRankAndOrderBy()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.SortOrder = "Ascending";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("RANK() OVER (ORDER BY COUNT(*) ASC) AS [Rank Id]", sql);
            Assert.Contains("ORDER BY [Service Count] ASC;", sql);
        }

        [Theory]
        [InlineData("ascending", "ASC")]
        [InlineData("ASC", "ASC")]
        [InlineData("Asc", "ASC")]
        [InlineData("ASCENDING", "ASC")]
        [InlineData("descending", "DESC")]
        [InlineData("DESC", "DESC")]
        [InlineData("Desc", "DESC")]
        [InlineData("DESCENDING", "DESC")]
        public void BuildQuery_SortOrderSynonyms_AreCaseInsensitive(string input, string expected)
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.SortOrder = input;

            var sql = sut.BuildQuery(intent);

            Assert.Contains($"RANK() OVER (ORDER BY COUNT(*) {expected}) AS [Rank Id]", sql);
            Assert.Contains($"ORDER BY [Service Count] {expected};", sql);
        }

        [Fact]
        public void BuildQuery_InvalidSortOrder_DefaultsToDesc()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.SortOrder = "fastest"; // invalid token

            var sql = sut.BuildQuery(intent);

            Assert.Contains("RANK() OVER (ORDER BY COUNT(*) DESC) AS [Rank Id]", sql);
            Assert.Contains("ORDER BY [Service Count] DESC;", sql);
        }

        // CountDistinct handling (new)
        [Fact]
        public void BuildQuery_CountDistinct_OnOfferingName_UsesDistinctCountAlias_AndRankingByDistinct()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.CountDistinct = "OfferingName";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT TOP 20 RegionName [Region], COUNT(DISTINCT OfferingName) AS [Distinct Count]", sql);
            Assert.Contains("RANK() OVER (ORDER BY COUNT(DISTINCT OfferingName) DESC) AS [Rank Id]", sql);
            Assert.Contains("ORDER BY [Distinct Count] DESC;", sql);
        }

        [Fact]
        public void BuildQuery_CountDistinct_WithAscendingOrder_AppliesAscEverywhere()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.CountDistinct = "ProductSkuName";
            intent.Parameters.SortOrder = "asc";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("COUNT(DISTINCT ProductSkuName) AS [Distinct Count]", sql);
            Assert.Contains("RANK() OVER (ORDER BY COUNT(DISTINCT ProductSkuName) ASC) AS [Rank Id]", sql);
            Assert.Contains("ORDER BY [Distinct Count] ASC;", sql);
        }

        [Fact]
        public void BuildQuery_CountDistinct_WithCustomGroupBy_DoesNotAffectGrouping()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "GeographyName";
            intent.Parameters.CountDistinct = "OfferingName";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT TOP 20 GeographyName [Geography], COUNT(DISTINCT OfferingName) AS [Distinct Count]", sql);
            Assert.Contains("GROUP BY GeographyName", sql);
            Assert.Contains("ORDER BY [Distinct Count] DESC;", sql);
        }
    }
}
