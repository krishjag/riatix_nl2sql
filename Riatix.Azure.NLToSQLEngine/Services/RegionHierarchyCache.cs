using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    public class RegionHierarchyCache : IRegionHierarchyCache
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly object _sync = new();
        private bool _isLoaded;

        private readonly Dictionary<string, HashSet<string>> _macroToRegions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _geoToRegions = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, (string? Geo, string? Macro)> _regionParents = new(StringComparer.OrdinalIgnoreCase);

        public RegionHierarchyCache(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        private void EnsureLoaded()
        {
            if (_isLoaded) return;
            lock (_sync)
            {
                if (_isLoaded) return;
                LoadHierarchy();
            }
        }

        private void LoadHierarchy()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();

            var data = db.Query<(string Macro, string Geography, string Region)>(
                "SELECT DISTINCT MacroGeographyName, GeographyName, RegionName FROM dbo.products_info"
            ).ToList();

            foreach (var (macro, geo, region) in data)
            {
                if (!string.IsNullOrWhiteSpace(macro))
                {
                    _macroToRegions.TryAdd(macro, new(StringComparer.OrdinalIgnoreCase));
                    _macroToRegions[macro].Add(region);
                }

                if (!string.IsNullOrWhiteSpace(geo))
                {
                    _geoToRegions.TryAdd(geo, new(StringComparer.OrdinalIgnoreCase));
                    _geoToRegions[geo].Add(region);
                }

                _regionParents[region] = (geo, macro);
            }

            _isLoaded = true;
            Console.WriteLine($"[RegionHierarchyCache] Loaded {data.Count:N0} hierarchy rows.");
        }

        public async Task PreWarmAsync(CancellationToken cancellationToken = default)
        {
            if (_isLoaded) return;
            await Task.Run(() =>
            {
                lock (_sync)
                {
                    if (_isLoaded) return;
                    LoadHierarchy();
                }
            }, cancellationToken);
        }

        public IReadOnlyCollection<string> GetRegionsForGeographies(IEnumerable<string> geographies)
        {
            EnsureLoaded();
            var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in geographies)
                if (_geoToRegions.TryGetValue(g, out var r))
                    regions.UnionWith(r);
            return regions;
        }

        public IReadOnlyCollection<string> GetRegionsForMacros(IEnumerable<string> macros)
        {
            EnsureLoaded();
            var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in macros)
                if (_macroToRegions.TryGetValue(m, out var r))
                    regions.UnionWith(r);
            return regions;
        }

        public bool TryGetParentGeography(string region, out string? geography, out string? macro)
        {
            EnsureLoaded();
            if (_regionParents.TryGetValue(region, out var p))
            {
                geography = p.Geo;
                macro = p.Macro;
                return true;
            }

            geography = null;
            macro = null;
            return false;
        }

        /// <summary>
        /// Returns all known region names loaded in the hierarchy cache.
        /// This is primarily used for global expansion (non-US Government regions).
        /// </summary>
        public IReadOnlyCollection<string> GetAllRegions()
        {
            EnsureLoaded();
            return _regionParents.Keys.ToList();
        }
    }
}
