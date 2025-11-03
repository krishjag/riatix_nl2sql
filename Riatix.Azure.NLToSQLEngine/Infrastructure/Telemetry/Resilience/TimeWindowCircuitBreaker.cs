namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience
{

    public class TimeWindowCircuitBreaker : ICircuitBreaker
    {
        private readonly ILogger<TimeWindowCircuitBreaker> _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;

        private int _failureCount;
        private DateTime _lastFailure = DateTime.MinValue;
        private bool _open;

        public bool IsOpen => _open;

        public TimeWindowCircuitBreaker(
            ILogger<TimeWindowCircuitBreaker> logger,
            int failureThreshold = 3,
            int openDurationSeconds = 60)
        {
            _logger = logger;
            _failureThreshold = failureThreshold;
            _openDuration = TimeSpan.FromSeconds(openDurationSeconds);
        }

        public bool ShouldAttempt()
        {
            if (_open && DateTime.UtcNow - _lastFailure > _openDuration)
            {
                _logger.LogInformation("Circuit breaker half-opening for a retry.");
                _open = false;
            }

            return !_open;
        }

        public void RecordFailure()
        {
            _failureCount++;
            _lastFailure = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _open = true;
                _logger.LogWarning("Circuit opened after {Failures} failures. Cooling for {Seconds}s.",
                    _failureCount, _openDuration.TotalSeconds);
            }
        }

        public void RecordSuccess()
        {
            if (_open)
                _logger.LogInformation("Circuit closed after recovery.");

            _failureCount = 0;
            _open = false;
        }
    }
}
