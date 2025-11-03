using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    /// <summary>
    /// Hosted background service that preloads and caches the product category map
    /// during application startup, similar to RegionHierarchyPrewarmService.
    /// </summary>
    public class ProductCategoryMapWarmupService : IHostedService
    {
        private readonly ILogger<ProductCategoryMapWarmupService> _logger;
        private readonly ProductCategoryMapLoader _loader;
        private readonly IServiceProvider _serviceProvider;
        private static IProductCategoryMap? _cachedMap;
        private static readonly object _lock = new();

        public ProductCategoryMapWarmupService(
            ILogger<ProductCategoryMapWarmupService> logger,
            ProductCategoryMapLoader loader,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _loader = loader;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting ProductCategoryMap warm-up...");

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var categoryData = await _loader.LoadAsync();
                var map = new ProductCategoryMap(categoryData);

                lock (_lock)
                {
                    _cachedMap = map;
                }

                sw.Stop();
                _logger.LogInformation(
                    "ProductCategoryMap loaded successfully with {Count} categories in {ElapsedMs}ms.",
                    map.GetAllCategories().Count, sw.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warm up ProductCategoryMap on startup.");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProductCategoryMapWarmupService stopped.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves the prewarmed, cached ProductCategoryMap.
        /// </summary>
        public static IProductCategoryMap? GetCachedMap()
        {
            lock (_lock)
            {
                return _cachedMap;
            }
        }
    }
}
