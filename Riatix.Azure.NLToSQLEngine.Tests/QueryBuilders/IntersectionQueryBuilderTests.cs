using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.QueryBuilders;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.QueryBuilders
{
    public class IntersectionQueryBuilderTests
    {
        private static IntersectionQueryBuilder CreateBuilder(
            IRegionHierarchyCache? cache = null,
            IServiceNameNormalizer? normalizer = null,
            IProductCategoryMap? categoryMap = null)
        {
            cache ??= new FakeRegionCache();
            normalizer ??= new FakeNormalizer();
            categoryMap ??= new FakeProductCategoryMap();
            return new IntersectionQueryBuilder(normalizer, cache, categoryMap);
        }

        private static IntentResponse CreateIntent(
            IEnumerable<string>? regions = null,
            IEnumerable<string>? geographies = null,
            IEnumerable<string>? macros = null,
            IEnumerable<string>? states = null,
            IEnumerable<string>? offerings = null,
            IEnumerable<string>? categories = null,
            IEnumerable<string>? skus = null)
        {
            return new IntentResponse
            {
                Intent = "intersection",
                Filters = new Filters
                {
                    RegionName = regions?.ToList() ?? new List<string>(),
                    GeographyName = geographies?.ToList() ?? new List<string>(),
                    MacroGeographyName = macros?.ToList() ?? new List<string>(),
                    CurrentState = states?.ToList() ?? new List<string>(),
                    OfferingName = offerings?.ToList() ?? new List<string>(),
                    ProductCategoryName = categories?.ToList() ?? new List<string>(),
                    ProductSkuName = skus?.ToList() ?? new List<string>()
                },
                Parameters = new Parameters()
            };
        }

        [Fact]
        public void BuildQuery_UsesIntersectMode_WhenMultipleScopes()
        {
            // Arrange
            var builder = CreateBuilder();
            var intent = CreateIntent(
                regions: new[] { "East US" },
                geographies: new[] { "EMEA" });

            // Act
            var sql = builder.BuildQuery(intent);

            // Assert
            Assert.Contains("-- Intersection Query (INTERSECT Mode)", sql);
            Assert.Contains("INTERSECT", sql);
            Assert.Contains("RegionName IN ('East US')", sql);
            Assert.Contains("GeographyName IN ('EMEA')", sql);
            // Default GA enforced when no states specified
            Assert.Contains("CurrentState IN ('GA')", sql);
            // Ordering by OfferingName per latest changes
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        [Fact]
        public void BuildQuery_SingleScope_NoHaving_WhenEffectiveRegionCountIsOne()
        {
            // Arrange
            var cache = new FakeRegionCache()
                .WithGeo("GeoSingle", new[] { "R1" }); // 1 effective region
            var builder = CreateBuilder(cache);
            var intent = CreateIntent(
                geographies: new[] { "GeoSingle" });

            // Act
            var sql = builder.BuildQuery(intent);

            // Assert
            Assert.Contains("-- Intersection Query (HAVING COUNT Mode, Cache Optimized)", sql);
            Assert.Contains("GeographyName IN ('GeoSingle')", sql);
            Assert.DoesNotContain("HAVING COUNT(DISTINCT RegionName)", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        [Fact]
        public void BuildQuery_ExpandsGlobalMacrosAndExcludesGov()
        {
            // Arrange
            var cache = new FakeRegionCache()
                // Returned values are treated as macros by current implementation
                .WithMacro("All", new[] { "Americas", "Europe", "Gov-US", "DoD-Secret" });
            var builder = CreateBuilder(cache);
            var intent = CreateIntent(
                macros: new[] { "Global" });

            // Act
            var sql = builder.BuildQuery(intent);

            // Assert
            Assert.Contains("-- Intersection Query (INTERSECT Mode)", sql);
            Assert.Contains("MacroGeographyName IN ('Americas')", sql);
            Assert.Contains("MacroGeographyName IN ('Europe')", sql);
            Assert.DoesNotContain("Gov-US", sql);
            Assert.DoesNotContain("DoD-Secret", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        [Fact]
        public void BuildQuery_AppliesOfferingCategoryExpansion_AndSkuFilters()
        {
            // Arrange
            var categoryMap = new FakeProductCategoryMap()
                .WithCategory("Compute", new[] { "Virtual Machines", "Container Instances" });

            var normalizer = new FakeNormalizer();
            var builder = CreateBuilder(categoryMap: categoryMap, normalizer: normalizer);

            var intent = CreateIntent(
                regions: new[] { "East US" },
                categories: new[] { "Compute" },
                skus: new[] { "D4s_v5" });

            // Act
            var sql = builder.BuildQuery(intent);

            // Assert
            Assert.Contains("RegionName IN ('East US')", sql);
            // Category expansion -> OfferingName IN (...)
            Assert.Contains("OfferingName IN ('Virtual Machines', 'Container Instances')", sql);
            Assert.Contains("ProductSkuName IN ('D4s_v5')", sql);
            Assert.Contains("ORDER BY OfferingName;", sql);
        }

        // --------- Test Fakes ---------

        private sealed class FakeNormalizer : IServiceNameNormalizer
        {
            public double ConfidenceThreshold { get; set; } = 0.8;
            public string Normalize(string input) => input?.Trim() ?? string.Empty;
        }

        private sealed class FakeRegionCache : IRegionHierarchyCache
        {
            private readonly Dictionary<string, IReadOnlyCollection<string>> _geoToRegions =
                new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, IReadOnlyCollection<string>> _macroToRegions =
                new(StringComparer.OrdinalIgnoreCase);

            public FakeRegionCache WithGeo(string geo, IEnumerable<string> regions)
            {
                _geoToRegions[geo] = regions.ToArray();
                return this;
            }

            public FakeRegionCache WithMacro(string macro, IEnumerable<string> regionsOrMacros)
            {
                _macroToRegions[macro] = regionsOrMacros.ToArray();
                return this;
            }

            public IReadOnlyCollection<string> GetRegionsForGeographies(IEnumerable<string> geographies)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in geographies ?? Array.Empty<string>())
                {
                    if (_geoToRegions.TryGetValue(g, out var rs))
                        set.UnionWith(rs);
                }
                return set.ToArray();
            }

            public IReadOnlyCollection<string> GetRegionsForMacros(IEnumerable<string> macroGeographies)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in macroGeographies ?? Array.Empty<string>())
                {
                    if (_macroToRegions.TryGetValue(m, out var rs))
                        set.UnionWith(rs);
                }
                return set.ToArray();
            }

            public IReadOnlyCollection<string> GetAllRegions()
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in _geoToRegions.Values.SelectMany(v => v))
                    set.Add(r);
                foreach (var r in _macroToRegions.Values.SelectMany(v => v))
                    set.Add(r);
                return set.ToArray();
            }

            public bool TryGetParentGeography(string region, out string? geography, out string? macroGeography)
            {
                geography = _geoToRegions.FirstOrDefault(kv => kv.Value.Contains(region)).Key;
                macroGeography = _macroToRegions.FirstOrDefault(kv => kv.Value.Contains(region)).Key;
                return geography != null || macroGeography != null;
            }

            public Task PreWarmAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class FakeProductCategoryMap : IProductCategoryMap
        {
            private readonly Dictionary<string, List<string>> _map = new(StringComparer.OrdinalIgnoreCase);

            public FakeProductCategoryMap WithCategory(string category, IEnumerable<string> offerings)
            {
                _map[category] = offerings.ToList();
                return this;
            }

            public IReadOnlyList<string> GetOfferingsForCategory(string categoryName) =>
                _map.TryGetValue(categoryName, out var list) ? list : Array.Empty<string>();

            public bool ContainsCategory(string categoryName) => _map.ContainsKey(categoryName);

            public IReadOnlyDictionary<string, List<string>> GetAllCategories() => _map;
        }
    }
}
