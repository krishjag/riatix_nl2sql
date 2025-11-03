namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience
{
    public class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly double _multiplier;
        private readonly ILogger<ExponentialBackoffRetryPolicy> _logger;

        public ExponentialBackoffRetryPolicy(
            ILogger<ExponentialBackoffRetryPolicy> logger,
            int maxRetries = 3,
            double multiplier = 2.0,
            int initialDelayMs = 500)
        {
            _maxRetries = maxRetries;
            _initialDelay = TimeSpan.FromMilliseconds(initialDelayMs);
            _multiplier = multiplier;
            _logger = logger;
        }

        public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            var delay = _initialDelay;

            for (var attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (Exception ex) when (attempt < _maxRetries)
                {
                    _logger.LogWarning(ex,
                        "Retry {Attempt}/{Max} failed. Retrying in {Delay} ms...",
                        attempt, _maxRetries, delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * _multiplier);
                }
            }

            await operation();
        }
    }
}
