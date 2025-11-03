using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Infrastructure.Telemetry.Resilience
{
    public class ExponentialBackoffRetryPolicyTests
    {
        [Fact]
        public async Task ExecuteAsync_SucceedsFirstTry_NoRetries_NoLogs()
        {
            var logger = new TestLogger<ExponentialBackoffRetryPolicy>();
            var policy = new ExponentialBackoffRetryPolicy(logger, maxRetries: 3, multiplier: 2.0, initialDelayMs: 10);

            var attempts = 0;
            Task Op()
            {
                attempts++;
                return Task.CompletedTask;
            }

            await policy.ExecuteAsync(Op);

            Assert.Equal(1, attempts);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task ExecuteAsync_RetriesWithExponentialBackoff_AndEventuallySucceeds()
        {
            // Fail 3 times, succeed on 4th; ensure 3 warnings with delays 10, 20, 40
            var logger = new TestLogger<ExponentialBackoffRetryPolicy>();
            var policy = new ExponentialBackoffRetryPolicy(logger, maxRetries: 5, multiplier: 2.0, initialDelayMs: 10);

            var attempts = 0;
            Task Op()
            {
                attempts++;
                if (attempts <= 3)
                    return Task.FromException(new InvalidOperationException("fail"));
                return Task.CompletedTask;
            }

            await policy.ExecuteAsync(Op);

            Assert.Equal(4, attempts);

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
            Assert.Equal(3, warnings.Count);

            // Validate structured log properties
            Assert.Equal(new[] { 1, 2, 3 }, warnings.Select(w => Convert.ToInt32(w.Properties["Attempt"])));
            Assert.All(warnings, w => Assert.Equal(5, Convert.ToInt32(w.Properties["Max"])));
            Assert.Equal(new[] { 10d, 20d, 40d }, warnings.Select(w => Convert.ToDouble(w.Properties["Delay"])));
        }

        [Fact]
        public async Task ExecuteAsync_AlwaysFails_ThrowsAfterMaxRetries_LogsDelaysForEachRetry()
        {
            var logger = new TestLogger<ExponentialBackoffRetryPolicy>();
            var policy = new ExponentialBackoffRetryPolicy(logger, maxRetries: 3, multiplier: 3.0, initialDelayMs: 5);

            var attempts = 0;
            Task Op()
            {
                attempts++;
                return Task.FromException(new ApplicationException("always fail"));
            }

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => policy.ExecuteAsync(Op));
            Assert.Equal("always fail", ex.Message);

            // Attempts equal to maxRetries; last failure is not caught (no extra call after loop)
            Assert.Equal(3, attempts);

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
            // Warnings only for attempts < maxRetries => 2 warnings
            Assert.Equal(2, warnings.Count);

            Assert.Equal(new[] { 1, 2 }, warnings.Select(w => Convert.ToInt32(w.Properties["Attempt"])));
            Assert.All(warnings, w => Assert.Equal(3, Convert.ToInt32(w.Properties["Max"])));
            // Delays: 5, then 15 (multiplier 3.0)
            Assert.Equal(new[] { 5d, 15d }, warnings.Select(w => Convert.ToDouble(w.Properties["Delay"])));
        }

        [Fact]
        public async Task ExecuteAsync_CanceledDuringDelay_ThrowsTaskCancelledException_StopsFurtherAttempts()
        {
            var logger = new TestLogger<ExponentialBackoffRetryPolicy>();
            var policy = new ExponentialBackoffRetryPolicy(logger, maxRetries: 5, multiplier: 2.0, initialDelayMs: 100);

            var attempts = 0;
            Task Op()
            {
                attempts++;
                return Task.FromException(new InvalidOperationException("fail fast"));
            }

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(20); // Cancel during first delay (100 ms)

            await Assert.ThrowsAsync<TaskCanceledException>(() => policy.ExecuteAsync(Op, cts.Token));

            // Only the initial attempt is executed; delay is canceled before next attempt
            Assert.Equal(1, attempts);

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
            Assert.Single(warnings);
            Assert.Equal(1, Convert.ToInt32(warnings[0].Properties["Attempt"]));
            Assert.Equal(100d, Convert.ToDouble(warnings[0].Properties["Delay"]));
        }

        [Fact]
        public async Task ExecuteAsync_WithZeroMaxRetries_CallsOperationOnce_NoWarnings()
        {
            var logger = new TestLogger<ExponentialBackoffRetryPolicy>();
            var policy = new ExponentialBackoffRetryPolicy(logger, maxRetries: 0, multiplier: 2.0, initialDelayMs: 10);

            var attempts = 0;
            Task Op()
            {
                attempts++;
                return Task.CompletedTask;
            }

            await policy.ExecuteAsync(Op);

            // Loop doesn't run; the post-loop call executes once
            Assert.Equal(1, attempts);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        private sealed class TestLogger<T> : ILogger<T>
        {
            public readonly List<LogEntry> Entries = new();

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var message = formatter(state, exception);
                var props = new Dictionary<string, object?>(StringComparer.Ordinal);

                if (state is IEnumerable<KeyValuePair<string, object>> kvps)
                {
                    foreach (var kv in kvps)
                    {
                        props[kv.Key] = kv.Value;
                    }
                }

                Entries.Add(new LogEntry
                {
                    Level = logLevel,
                    Message = message,
                    Exception = exception,
                    Properties = props
                });
            }
        }

        private sealed class LogEntry
        {
            public LogLevel Level { get; set; }
            public string Message { get; set; } = string.Empty;
            public Exception? Exception { get; set; }
            public Dictionary<string, object?> Properties { get; set; } = new();
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}