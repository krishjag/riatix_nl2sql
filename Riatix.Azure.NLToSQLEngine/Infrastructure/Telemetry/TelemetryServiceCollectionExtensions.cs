using Microsoft.Extensions.DependencyInjection;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Queue;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience;

namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry
{
    public static class TelemetryServiceCollectionExtensions
    {
        public static IServiceCollection AddTelemetryPipeline(this IServiceCollection services)
        {
            services.AddScoped<IQueryLogRepository, SqlQueryLogRepository>(); // scoped (DB connection)
            services.AddScoped<IQueryLogService, QueryLogService>();           // scoped
            services.AddSingleton<IQueryLogQueue, QueryLogQueue>();            // singleton (in-memory)
            services.AddSingleton<IQueryLogPersistence, FileQueryLogPersistence>(); // singleton
            services.AddSingleton<IRetryPolicy, ExponentialBackoffRetryPolicy>();
            services.AddSingleton<ICircuitBreaker, TimeWindowCircuitBreaker>();
            services.AddHostedService<QueryLogBackgroundService>(); // singleton hosted service

            return services;
        }
    }
}
