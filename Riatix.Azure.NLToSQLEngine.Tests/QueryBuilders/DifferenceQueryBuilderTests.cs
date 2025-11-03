using Moq;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.QueryBuilders;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.QueryBuilders
{
    public class DifferenceQueryBuilderTests
    {
        private static (DifferenceQueryBuilder sut, Mock<IServiceNameNormalizer> norm, Mock<IRegionHierarchyCache> region, Mock<IProductCategoryMap> cat) CreateSut()
        {
            var normalizer = new Mock<IServiceNameNormalizer>(MockBehavior.Loose);
            normalizer.Setup(n => n.Normalize(It.IsAny<string>()))
            .Returns<string>(s => s); // identity by default

            var regionCache = new Mock<IRegionHierarchyCache>(MockBehavior.Strict);
            // Default: no expansions unless a test sets up
            regionCache.Setup(r => r.GetRegionsForGeographies(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<string>());
            regionCache.Setup(r => r.GetRegionsForMacros(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<string>());

            var categories = new Mock<IProductCategoryMap>(MockBehavior.Loose);
            categories.Setup(c => c.ContainsCategory(It.IsAny<string>())).Returns(false);
            categories.Setup(c => c.GetOfferingsForCategory(It.IsAny<string>()))
            .Returns(Array.Empty<string>());

            var sut = new DifferenceQueryBuilder(normalizer.Object, regionCache.Object, categories.Object);
            return (sut, normalizer, regionCache, categories);
        }

        private static IntentResponse NewIntent()
        {
            return new IntentResponse
            {
                Intent = "difference",
                Filters = new Filters(),
                Parameters = new Parameters()
            };
        }

        // CanHandle
        [Fact]
        public void CanHandle_DifferenceIntent_ReturnsTrue()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();

            Assert.True(sut.CanHandle(intent));
        }

        [Fact]
        public void CanHandle_OtherIntent_ReturnsFalse()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Intent = "list";

            Assert.False(sut.CanHandle(intent));
        }

        // Symmetric mode validations
        [Fact]
        public void BuildQuery_Symmetric_Throws_WhenLessThanTwoScopes()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent(); // no regions/geo/macro

            var ex = Assert.Throws<ArgumentException>(() => sut.BuildQuery(intent));
            Assert.Contains("at least two comparison values", ex.Message);
        }

        [Fact]
        public void BuildQuery_Symmetric_WithTwoRegions_PivotsByRegion_IncludesDefaultGA()
        {
            var (sut, _, _, _) = CreateSut();
            var intent = NewIntent();
            intent.Filters.RegionName.AddRange(new[] { "East US", "West US" });

            var sql = sut.BuildQuery(intent);

            Assert.Contains("-- Symmetric Comparison Query (Pivot View)", sql);
            Assert.Contains("SELECT OfferingName AS [Product], ProductSkuName AS [SKU]", sql);
            Assert.Contains("FROM dbo.products_info", sql);
            Assert.Contains("WHERE CurrentState IN ('GA')", sql); // default GA
            Assert.Contains("RegionName IN ('East US', 'West US')".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            Assert.Contains("FOR RegionName IN ([East US], [West US])".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            Assert.Contains("COUNT(OfferingExists)", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        [Fact]
        public void BuildQuery_Symmetric_WithGeographies_NoExpansion_PivotsByGeography()
        {
            var (sut, _, region, _) = CreateSut();
            // Ensure no regions are added for geographies
            region.Setup(r => r.GetRegionsForGeographies(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<string>());

            var intent = NewIntent();
            intent.Filters.GeographyName.AddRange(new[] { "United States", "France" });

            var sql = sut.BuildQuery(intent);

            Assert.Contains("FOR GeographyName IN ([United States], [France])".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            Assert.Contains("GeographyName IN ('United States', 'France')".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
        }

        [Fact]
        public void BuildQuery_Symmetric_WithGeographies_ExpansionToRegions_UsesScopeWithRegionColumns()
        {
            var (sut, _, region, _) = CreateSut();
            region.Setup(r => r.GetRegionsForGeographies(It.Is<IEnumerable<string>>(g => g.Contains("United States") && g.Contains("France"))))
            .Returns(new[] { "East US", "West US", "France Central" });

            var intent = NewIntent();
            intent.Filters.GeographyName.AddRange(new[] { "United States", "France" });

            var sql = sut.BuildQuery(intent);

            // Compare dimension becomes Scope and columns are region values
            Assert.Contains("FOR Scope IN ([East US], [West US], [France Central])".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            // Inner CASE should map RegionName -> same value
            Assert.Contains("WHEN RegionName = 'East US' THEN 'East US'", sql);
            Assert.Contains("WHEN RegionName = 'West US' THEN 'West US'", sql);
            Assert.Contains("WHEN RegionName = 'France Central' THEN 'France Central'", sql);
            // WHERE contains OR of Region/Geo
            Assert.Contains("RegionName IN ('East US', 'West US', 'France Central')".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            Assert.Contains("GeographyName IN ('United States', 'France')".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
        }

        [Fact]
        public void BuildQuery_Symmetric_WithOnlyGeoAndMacro_NoRegions_UsesScopeWithPrefixedValues()
        {
            var (sut, _, region, _) = CreateSut();
            region.Setup(r => r.GetRegionsForGeographies(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<string>());
            region.Setup(r => r.GetRegionsForMacros(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<string>());

            var intent = NewIntent();
            intent.Filters.GeographyName.Add("France");
            intent.Filters.MacroGeographyName.Add("Europe");

            var sql = sut.BuildQuery(intent);

            Assert.Contains("FOR Scope IN ([Country:France], [MacroGeography:Europe])".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            Assert.Contains("WHEN GeographyName = 'France' THEN 'Country:France'", sql);
            Assert.Contains("WHEN MacroGeographyName = 'Europe' THEN 'MacroGeography:Europe'", sql);
            Assert.Contains("GeographyName IN ('France')", sql);
            Assert.Contains("MacroGeographyName IN ('Europe')", sql);
        }

        [Fact]
        public void BuildQuery_Symmetric_WithOfferingsAndSkus_IncludesFiltersAndNormalization()
        {
            var (sut, norm, _, _) = CreateSut();
            norm.Setup(n => n.Normalize("Azure SQL")).Returns("Azure SQL Database");
            norm.Setup(n => n.Normalize("Azure Storage")).Returns("Azure Storage");

            var intent = NewIntent();
            intent.Filters.RegionName.AddRange(new[] { "East US", "West US" }); // ensure >=2 scopes
            intent.Filters.OfferingName.AddRange(new[] { "Azure SQL", "Azure Storage" });
            intent.Filters.ProductSkuName.AddRange(new[] { "Standard", "Premium" });

            var sql = sut.BuildQuery(intent);

            Assert.Contains("OfferingName IN ('Azure SQL Database', 'Azure Storage')".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
            Assert.Contains("ProductSkuName IN ('Standard', 'Premium')".Replace(" ", string.Empty), sql.Replace(" ", string.Empty));
        }

        [Fact]
        public void BuildQuery_Symmetric_WithCategoryOnly_ExpandsToOfferings()
        {
            var (sut, norm, _, cat) = CreateSut();
            cat.Setup(c => c.ContainsCategory("Databases")).Returns(true);
            cat.Setup(c => c.GetOfferingsForCategory("Databases")).Returns(new[] { "Azure SQL", "Azure Cosmos DB" });
            norm.Setup(n => n.Normalize(It.IsAny<string>())).Returns<string>(s => s + " (N)");

            var intent = NewIntent();
            intent.Filters.RegionName.AddRange(new[] { "East US", "West US" }); // avoid exception
            intent.Filters.ProductCategoryName.Add("Databases");

            var sql = sut.BuildQuery(intent);

            Assert.Contains("OfferingName IN (", sql);
            Assert.Contains("'Azure SQL (N)'", sql);
            Assert.Contains("'Azure Cosmos DB (N)'", sql);
        }

        // Directional mode
        [Fact]
        public void BuildQuery_Directional_RegionSourceTarget_IncludesExceptAndFiltersAndDefaultGA()
        {
            var (sut, norm, _, _) = CreateSut();
            norm.Setup(n => n.Normalize("Azure SQL")).Returns("Azure SQL Database");

            var intent = NewIntent();
            intent.Parameters.DifferenceMode = "directional";
            intent.Parameters.DifferenceSource = new ComparisonScope { ScopeType = "RegionName", ScopeValue = "East US" };
            intent.Parameters.DifferenceTarget = new ComparisonScope { ScopeType = "RegionName", ScopeValue = "West US" };
            intent.Filters.OfferingName.Add("Azure SQL");
            intent.Filters.ProductSkuName.Add("Premium");

            var sql = sut.BuildQuery(intent);

            Assert.Contains("-- Directional Difference Query (A - B)", sql);
            Assert.Contains("SELECT DISTINCT OfferingName AS [Product], ProductSkuName AS [SKU]", sql);
            Assert.Contains("EXCEPT", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
            Assert.Contains("WHERE RegionName = 'East US'", sql);
            Assert.Contains("WHERE RegionName = 'West US'", sql);
            Assert.Contains("CurrentState IN ('GA')", sql); // default GA
            Assert.Contains("OfferingName IN ('Azure SQL Database')", sql);
            Assert.Contains("ProductSkuName IN ('Premium')", sql);
        }

        [Fact]
        public void BuildQuery_Directional_MacroScope_BuildsMacroFilter()
        {
            var (sut, _, _, _) = CreateSut();

            var intent = NewIntent();
            intent.Parameters.DifferenceMode = "directional";
            intent.Parameters.DifferenceSource = new ComparisonScope { ScopeType = "MacroGeographyName", ScopeValue = "Europe" };
            intent.Parameters.DifferenceTarget = new ComparisonScope { ScopeType = "MacroGeographyName", ScopeValue = "Asia" };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("WHERE MacroGeographyName = 'Europe'", sql);
            Assert.Contains("WHERE MacroGeographyName = 'Asia'", sql);
        }

        [Fact]
        public void BuildQuery_Directional_ScopeValueWithQuote_IsEscaped()
        {
            var (sut, _, _, _) = CreateSut();

            var intent = NewIntent();
            intent.Parameters.DifferenceMode = "directional";
            intent.Parameters.DifferenceSource = new ComparisonScope { ScopeType = "RegionName", ScopeValue = "O'Brien Region" };
            intent.Parameters.DifferenceTarget = new ComparisonScope { ScopeType = "RegionName", ScopeValue = "Other" };

            var sql = sut.BuildQuery(intent);

            Assert.Contains("WHERE RegionName = 'O''Brien Region'", sql);
        }

        [Fact]
        public void BuildQuery_Directional_InvalidScopeType_Throws()
        {
            var (sut, _, _, _) = CreateSut();

            var intent = NewIntent();
            intent.Parameters.DifferenceMode = "directional";
            intent.Parameters.DifferenceSource = new ComparisonScope { ScopeType = "Foo", ScopeValue = "X" };
            intent.Parameters.DifferenceTarget = new ComparisonScope { ScopeType = "RegionName", ScopeValue = "Y" };

            var ex = Assert.Throws<ArgumentException>(() => sut.BuildQuery(intent));
            Assert.Contains("Unknown scope type", ex.Message);
        }

        [Fact]
        public void BuildQuery_Symmetric_WithOfferingAndCategory_DoesNotExpand_AddsClarification()
        {
            var (sut, norm, _, cat) = CreateSut();
            cat.Setup(c => c.ContainsCategory(It.IsAny<string>())).Returns(true);
            cat.Setup(c => c.GetOfferingsForCategory(It.IsAny<string>())).Returns(new[] { "A", "B" });
            norm.Setup(n => n.Normalize("Azure Storage")).Returns("Azure Storage (N)");

            var intent = NewIntent();
            intent.Filters.RegionName.AddRange(new[] { "East US", "West US" });
            intent.Filters.OfferingName.Add("Azure Storage");
            intent.Filters.ProductCategoryName.Add("Databases");

            var sql = sut.BuildQuery(intent);

            Assert.Contains("OfferingName IN ('Azure Storage (N)')", sql);
            Assert.Contains("OfferingName specified; ProductCategoryName retained for traceability only.", intent.Clarifications);
        }
    }
}
