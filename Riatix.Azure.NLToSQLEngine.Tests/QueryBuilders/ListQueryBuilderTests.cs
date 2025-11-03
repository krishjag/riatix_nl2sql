using Moq;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.QueryBuilders;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.QueryBuilders
{
    public class ListQueryBuilderTests
    {
        private static (ListQueryBuilder sut, Mock<IServiceNameNormalizer> norm, Mock<IRegionHierarchyCache> region, Mock<IProductCategoryMap> cat) CreateSut()
        {
            var normalizer = new Mock<IServiceNameNormalizer>(MockBehavior.Loose);
            // Default normalization returns the input unchanged unless overridden in a test
            normalizer.Setup(n => n.Normalize(It.IsAny<string>()))
                .Returns<string>(s => s);

            var regionCache = new Mock<IRegionHierarchyCache>(MockBehavior.Loose);
            var categories = new Mock<IProductCategoryMap>(MockBehavior.Loose);
            // Default: no categories
            categories.Setup(c => c.ContainsCategory(It.IsAny<string>())).Returns(false);
            categories.Setup(c => c.GetOfferingsForCategory(It.IsAny<string>()))
                .Returns(Array.Empty<string>());

            var sut = new ListQueryBuilder(normalizer.Object, regionCache.Object, categories.Object);
            return (sut, normalizer, regionCache, categories);
        }

        private static IntentResponse NewIntent()
        {
            return new IntentResponse
            {
                Intent = "list",
                Filters = new Filters(),
                Parameters = new Parameters()
            };
        }

        // CanHandle tests
        [Fact]
        public void CanHandle_ListWithoutGroupOrCountDistinct_ReturnsTrue()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();

            Assert.True(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_NonListIntent_ReturnsFalse()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Intent = "aggregation";

            Assert.False(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_WithGroupBy_ReturnsFalse()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "region";

            Assert.False(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_WithCountDistinct_ReturnsFalse()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.CountDistinct = "sku";

            Assert.False(sut.CanHandle(intent));
        }

        // BuildQuery tests
        [Fact]
        public void BuildQuery_NoFilters_DefaultColumnsWithAliases_AndOrderBy()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT", sql);
            // Aliased columns
            Assert.Contains("OfferingName [Product]", sql);
            Assert.Contains("ProductSkuName [Product SKU]", sql);
            Assert.Contains("GeographyName [Geography]", sql);
            Assert.Contains("RegionName [Region]", sql);
            Assert.Contains("CurrentState [Current State]", sql);
            Assert.Contains("FROM dbo.products_info", sql);
            Assert.DoesNotContain("WHERE", sql); // no filters
            // ORDER BY uses original column names in the defined order
            Assert.Contains("ORDER BY OfferingName, ProductSkuName, GeographyName, RegionName, CurrentState;", sql);
            Assert.DoesNotContain("TOP", sql); // ensure no TOP since it's not implemented
        }

        [Fact]
        public void BuildQuery_WithRegionAndGeographyFilters_IncludesWhereClauses()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Filters.RegionName.AddRange(new[] { "East US", "West US" });
            intent.Filters.GeographyName.AddRange(new[] { "United States" });

            var sql = sut.BuildQuery(intent);

            Assert.Contains("WHERE", sql);
            Assert.Contains("RegionName IN ('East US','West US')", sql);
            Assert.Contains("GeographyName IN ('United States')", sql);
        }

        [Fact]
        public void BuildQuery_WithOfferings_NormalizesNamesInFilter()
        {
            var (sut, normalizer, _, _) = CreateSut();
            // Override normalization to a predictable value
            normalizer.Setup(n => n.Normalize("Azure SQL")).Returns("Azure SQL Database");

            var intent = NewIntent();
            intent.Filters.OfferingName.Add("Azure SQL");

            var sql = sut.BuildQuery(intent);

            Assert.Contains("OfferingName IN ('Azure SQL Database')", sql);
        }

        [Fact]
        public void BuildQuery_WithProductCategory_ExpandsToOfferings()
        {
            var (sut, normalizer, _, categories) = CreateSut();

            categories.Setup(c => c.ContainsCategory("Databases")).Returns(true);
            categories.Setup(c => c.GetOfferingsForCategory("Databases"))
                .Returns(new[] { "Azure SQL", "Azure Cosmos DB" });

            // Normalizer returns the name with a suffix to ensure we see normalization in SQL
            normalizer.Setup(n => n.Normalize(It.IsAny<string>()))
                .Returns<string>(s => $"{s} (Normalized)");

            var intent = NewIntent();
            intent.Filters.ProductCategoryName.Add("Databases");

            var sql = sut.BuildQuery(intent);

            // Should include both normalized offerings (order-agnostic)
            Assert.Contains("'Azure SQL (Normalized)'", sql);
            Assert.Contains("'Azure Cosmos DB (Normalized)'", sql);
            Assert.Contains("OfferingName IN (", sql);
        }

        [Fact]
        public void BuildQuery_WithOfferingAndCategory_DoesNotExpand_AndAddsClarification()
        {
            var (sut, normalizer, _, categories) = CreateSut();

            // Category exists but should not be used because offering is already specified
            categories.Setup(c => c.ContainsCategory(It.IsAny<string>())).Returns(true);
            categories.Setup(c => c.GetOfferingsForCategory(It.IsAny<string>()))
                .Returns(new[] { "Azure SQL", "Azure Cosmos DB" });

            // Normalization to predictable value
            normalizer.Setup(n => n.Normalize("Azure Storage"))
                .Returns("Azure Storage (Normalized)");

            var intent = NewIntent();
            intent.Filters.OfferingName.Add("Azure Storage");
            intent.Filters.ProductCategoryName.Add("Databases");

            var sql = sut.BuildQuery(intent);

            // Should use only the specified offering, normalized
            Assert.Contains("OfferingName IN ('Azure Storage (Normalized)')", sql);
            // Clarification added
            Assert.Contains("OfferingName specified; ProductCategoryName retained for traceability only.", intent.Clarifications);
        }

        [Fact]
        public void BuildQuery_GroupByMacro_SelectsMacroOnly()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "macro";

            var sql = sut.BuildQuery(intent);

            // Should select macro column with alias and order by macro only
            Assert.Contains("SELECT MacroGeographyName [Macro Geography]", sql);
            Assert.DoesNotContain("OfferingName [Product]", sql);
            Assert.Contains("ORDER BY MacroGeographyName;", sql);
        }

        [Fact]
        public void BuildQuery_GroupByGeo_SelectsGeographyAndRegion()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "geo";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT GeographyName [Geography], RegionName [Region]", sql);
            Assert.Contains("ORDER BY GeographyName, RegionName;", sql);
        }

        [Fact]
        public void BuildQuery_GroupByRegion_SelectsRegionAndGeography()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.GroupBy = "region";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT RegionName [Region], GeographyName [Geography]", sql);
            Assert.Contains("ORDER BY RegionName, GeographyName;", sql);
        }

        [Fact]
        public void BuildQuery_CountDistinctSku_SelectsOfferingAndSku()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.CountDistinct = "sku";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT OfferingName [Product], ProductSkuName [Product SKU]", sql);
            Assert.Contains("ORDER BY OfferingName, ProductSkuName;", sql);
        }

        [Fact]
        public void BuildQuery_CountDistinctOffering_SelectsOfferingOnly()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Parameters.CountDistinct = "offering";

            var sql = sut.BuildQuery(intent);

            Assert.Contains("SELECT OfferingName [Product]", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        [Fact]
        public void BuildQuery_WithSkuStateAndMacroFilters_IncludesAllWhereParts()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Filters.ProductSkuName.AddRange(new[] { "Standard", "Premium" });
            intent.Filters.CurrentState.AddRange(new[] { "GA" });
            intent.Filters.MacroGeographyName.AddRange(new[] { "Europe", "Asia" });

            var sql = sut.BuildQuery(intent);

            Assert.Contains("ProductSkuName IN ('Standard','Premium')", sql);
            Assert.Contains("CurrentState IN ('GA')", sql);
            Assert.Contains("MacroGeographyName IN ('Europe','Asia')", sql);
        }
    }
}
