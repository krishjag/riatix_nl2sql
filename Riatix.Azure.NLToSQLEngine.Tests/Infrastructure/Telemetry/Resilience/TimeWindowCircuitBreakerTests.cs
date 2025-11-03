using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Infrastructure.Telemetry.Resilience
{
    public class TimeWindowCircuitBreakerTests
    {
        [Fact]
        public void ShouldAttempt_InitiallyTrue_ClosedCircuit()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 3, openDurationSeconds: 60);

            Assert.False(breaker.IsOpen);
            Assert.True(breaker.ShouldAttempt());
            Assert.Empty(logger.Entries.Where(e => e.Level >= LogLevel.Information));
        }

        [Fact]
        public void RecordFailure_BelowThreshold_ShouldNotOpen_AndNoWarning()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 3, openDurationSeconds: 60);

            breaker.RecordFailure(); // 1st failure
            breaker.RecordFailure(); // 2nd failure (< threshold)

            Assert.False(breaker.IsOpen);
            Assert.True(breaker.ShouldAttempt());

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public void RecordFailure_ReachingThreshold_OpensAndLogsWarning()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 2, openDurationSeconds: 5);

            breaker.RecordFailure(); // count = 1
            breaker.RecordFailure(); // count = 2 -> opens

            Assert.True(breaker.IsOpen);
            Assert.False(breaker.ShouldAttempt()); // within window

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
            Assert.Single(warnings);

            // Structured logging properties
            Assert.Equal(2, Convert.ToInt32(warnings[0].Properties["Failures"]));
            Assert.Equal(5d, Convert.ToDouble(warnings[0].Properties["Seconds"]));
            Assert.Contains("Circuit opened after", warnings[0].Message);
        }

        [Fact]
        public void ShouldAttempt_OpenWithinWindow_ReturnsFalse()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 1, openDurationSeconds: 3600);

            // Trip circuit open
            breaker.RecordFailure(); // threshold == 1 -> opens

            // Immediately check, should still be within the window
            Assert.True(breaker.IsOpen);
            Assert.False(breaker.ShouldAttempt());

            // Only warning on open, no half-open info yet
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("half-opening", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ShouldAttempt_OpenAndWindowElapsed_HalfOpensLogsInfoAndReturnsTrue_WhenDurationIsZero()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 1, openDurationSeconds: 0);

            // Trip circuit open
            breaker.RecordFailure();

            // With 0s open duration, next attempt should half-open immediately
            var canAttempt = breaker.ShouldAttempt();

            Assert.True(canAttempt);
            Assert.False(breaker.IsOpen);

            var infos = logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
            Assert.Contains(infos, e => e.Message.Contains("half-opening", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void RecordSuccess_WhenOpen_ClosesAndLogsInfo()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 1, openDurationSeconds: 60);

            // Open circuit
            breaker.RecordFailure();
            Assert.True(breaker.IsOpen);

            breaker.RecordSuccess();

            Assert.False(breaker.IsOpen);
            Assert.True(breaker.ShouldAttempt());

            var infos = logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
            Assert.Contains(infos, e => e.Message.Contains("closed after recovery", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void RecordSuccess_WhenClosed_NoInfoLogAndRemainsClosed()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 3, openDurationSeconds: 60);

            // Already closed
            Assert.False(breaker.IsOpen);

            breaker.RecordSuccess(); // Should not log "closed after recovery" when already closed

            Assert.False(breaker.IsOpen);
            Assert.True(breaker.ShouldAttempt());

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("closed after recovery", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void RecordFailure_AfterOpen_LogsOnEveryFailureAtOrBeyondThreshold()
        {
            var logger = new TestLogger<TimeWindowCircuitBreaker>();
            var breaker = new TimeWindowCircuitBreaker(logger, failureThreshold: 2, openDurationSeconds: 5);

            // Failures: 1 (no log), 2 (log, opens), 3 (log), 4 (log)
            breaker.RecordFailure(); // 1
            breaker.RecordFailure(); // 2 -> opens, log #1
            breaker.RecordFailure(); // 3 -> still open, log #2
            breaker.RecordFailure(); // 4 -> still open, log #3

            Assert.True(breaker.IsOpen);

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
            Assert.Equal(3, warnings.Count);

            // Verify Failures property increments as 2, 3, 4 and seconds are constant
            Assert.Equal(new[] { 2, 3, 4 }, warnings.Select(w => Convert.ToInt32(w.Properties["Failures"])));
            Assert.All(warnings, w => Assert.Equal(5d, Convert.ToDouble(w.Properties["Seconds"])));
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
                        props[kv.Key] = kv.Value;
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