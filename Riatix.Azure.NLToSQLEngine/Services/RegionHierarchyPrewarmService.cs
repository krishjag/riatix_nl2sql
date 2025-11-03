namespace Riatix.Azure.NLToSQLEngine.Services
{
    public class RegionHierarchyPrewarmService : IHostedService
    {
        private readonly IRegionHierarchyCache _cache;

        public RegionHierarchyPrewarmService(IRegionHierarchyCache cache)
        {
            _cache = cache;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine("[Prewarm] Initializing region hierarchy cache...");
                await _cache.PreWarmAsync(cancellationToken);
                Console.WriteLine("[Prewarm] Region hierarchy cache ready.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Prewarm] Failed to pre-warm cache: {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
