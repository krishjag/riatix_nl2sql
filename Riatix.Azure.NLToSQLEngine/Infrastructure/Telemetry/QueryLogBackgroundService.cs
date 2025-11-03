using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Queue;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry
{
    public class QueryLogBackgroundService : BackgroundService
    {
        private readonly IQueryLogQueue _queue;
        private readonly IQueryLogPersistence _persistence;
        private readonly IRetryPolicy _retryPolicy;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QueryLogBackgroundService> _logger;

        private readonly List<QueryLog> _buffer = new();
        private readonly int _maxBatchSize = 500;
        private readonly TimeSpan _maxInterval = TimeSpan.FromSeconds(30);
        private DateTime _lastFlushTime = DateTime.UtcNow;

        public QueryLogBackgroundService(
            IQueryLogQueue queue,
            IQueryLogPersistence persistence,
            IRetryPolicy retryPolicy,
            ICircuitBreaker circuitBreaker,
            IServiceScopeFactory scopeFactory,
            ILogger<QueryLogBackgroundService> logger)
        {
            _queue = queue;
            _persistence = persistence;
            _retryPolicy = retryPolicy;
            _circuitBreaker = circuitBreaker;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("QueryLog background service running with scoped persistence.");

            var recovered = await _persistence.LoadUnflushedAsync(stoppingToken);
            if (recovered.Count > 0)
            {
                _logger.LogInformation("Recovered {Count} unflushed logs from disk.", recovered.Count);
                _buffer.AddRange(recovered);
            }

            await foreach (var log in _queue.DequeueAllAsync(stoppingToken))
            {
                _buffer.Add(log);
                var elapsed = DateTime.UtcNow - _lastFlushTime;

                if (_buffer.Count >= _maxBatchSize || elapsed >= _maxInterval)
                    await FlushAsync(stoppingToken);
            }

            if (_buffer.Count > 0)
                await FlushAsync(stoppingToken);
        }

        private async Task FlushAsync(CancellationToken ct)
        {
            if (_buffer.Count == 0) return;

            var batch = _buffer.ToList();
            _buffer.Clear();

            if (!_circuitBreaker.ShouldAttempt())
            {
                _logger.LogWarning("Circuit open - skipping SQL flush; persisting {Count} logs to disk.", batch.Count);
                await _persistence.SaveUnflushedAsync(batch, ct);
                return;
            }

            try
            {                
                using var scope = _scopeFactory.CreateScope();
                var logService = scope.ServiceProvider.GetRequiredService<IQueryLogService>();

                await _retryPolicy.ExecuteAsync(() => logService.SaveBatchAsync(batch, ct), ct);
                _circuitBreaker.RecordSuccess();
                await _persistence.ClearAsync(ct);

                _lastFlushTime = DateTime.UtcNow;
                _logger.LogInformation("Flushed {Count} logs to SQL successfully.", batch.Count);
            }
            catch (Exception ex)
            {
                _circuitBreaker.RecordFailure();
                _logger.LogError(ex, "SQL flush failed; persisting {Count} logs to disk.", batch.Count);
                await _persistence.SaveUnflushedAsync(batch, ct);
            }
        }
    }
}
