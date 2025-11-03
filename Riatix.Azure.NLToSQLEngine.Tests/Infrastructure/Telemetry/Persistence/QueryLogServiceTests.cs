using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Infrastructure.Telemetry.Persistence
{
    public class QueryLogServiceTests
    {
        private sealed class TestLogger<T> : ILogger<T>
        {
            public List<(LogLevel Level, EventId EventId, string Message, Exception? Exception)> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, eventId, formatter(state, exception), exception));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }

        private sealed class FakeRepository : IQueryLogRepository
        {
            public QueryLog? LastInsert { get; private set; }
            public IReadOnlyList<QueryLog>? LastBatch { get; private set; }
            public CancellationToken LastToken { get; private set; }
            public int InsertAsyncCalls { get; private set; }
            public int InsertBatchAsyncCalls { get; private set; }
            public Exception? ThrowOnInsert { get; set; }
            public Exception? ThrowOnInsertBatch { get; set; }

            public Task InsertAsync(QueryLog log, CancellationToken cancellationToken = default)
            {
                if (ThrowOnInsert is not null) throw ThrowOnInsert;
                InsertAsyncCalls++;
                LastInsert = log;
                LastToken = cancellationToken;
                return Task.CompletedTask;
            }

            public Task InsertBatchAsync(IEnumerable<QueryLog> logs, CancellationToken cancellationToken = default)
            {
                if (ThrowOnInsertBatch is not null) throw ThrowOnInsertBatch;
                InsertBatchAsyncCalls++;
                LastBatch = logs.ToList();
                LastToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        private static QueryLog NewLog(int id = 1) => new()
        {
            Id = id,
            UserQuery = $"q{id}",
            Model = "m"
        };

        private static IEnumerable<QueryLog> AsEnumerableWithoutICollection(params QueryLog[] items)
        {
            foreach (var i in items)
                yield return i;
        }

        [Fact]
        public async Task SaveAsync_DelegatesToRepository_WithCancellationToken()
        {
            var repo = new FakeRepository();
            var logger = new TestLogger<QueryLogService>();
            var sut = new QueryLogService(repo, logger);

            var cts = new CancellationTokenSource();
            var log = NewLog(42);

            await sut.SaveAsync(log, cts.Token);

            Assert.Equal(1, repo.InsertAsyncCalls);
            Assert.Same(log, repo.LastInsert);
            Assert.Equal(cts.Token, repo.LastToken);

            // SaveAsync does not log
            Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Trace);
        }

        [Fact]
        public async Task SaveAsync_Propagates_Exception_FromRepository()
        {
            var repo = new FakeRepository { ThrowOnInsert = new InvalidOperationException("boom") };
            var logger = new TestLogger<QueryLogService>();
            var sut = new QueryLogService(repo, logger);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveAsync(NewLog()));
            Assert.Equal("boom", ex.Message);

            // No logs on failure path in SaveAsync
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task SaveBatchAsync_DelegatesToRepository_AndLogsCount_WhenICollection()
        {
            var repo = new FakeRepository();
            var logger = new TestLogger<QueryLogService>();
            var sut = new QueryLogService(repo, logger);

            var cts = new CancellationTokenSource();
            var logs = new List<QueryLog> { NewLog(1), NewLog(2) };

            await sut.SaveBatchAsync(logs, cts.Token);

            Assert.Equal(1, repo.InsertBatchAsyncCalls);
            Assert.NotNull(repo.LastBatch);
            Assert.Equal(new[] { 1, 2 }, repo.LastBatch!.Select(l => l.Id).ToArray());
            Assert.Equal(cts.Token, repo.LastToken);

            // One Debug entry with resolved message including count 2
            var debug = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Debug));
            Assert.Equal("Saved 2 logs via repository.", debug.Message);
        }

        [Fact]
        public async Task SaveBatchAsync_LogsMinusOne_WhenEnumerableIsNotICollection()
        {
            var repo = new FakeRepository();
            var logger = new TestLogger<QueryLogService>();
            var sut = new QueryLogService(repo, logger);

            var enumerable = AsEnumerableWithoutICollection(NewLog(10), NewLog(11));

            await sut.SaveBatchAsync(enumerable);

            var debug = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Debug));
            Assert.Equal("Saved -1 logs via repository.", debug.Message);

            Assert.Equal(1, repo.InsertBatchAsyncCalls);
            Assert.NotNull(repo.LastBatch);
            Assert.Equal(new[] { 10, 11 }, repo.LastBatch!.Select(l => l.Id).ToArray());
        }

        [Fact]
        public async Task SaveBatchAsync_Propagates_Exception_AndDoesNotLog()
        {
            var repo = new FakeRepository { ThrowOnInsertBatch = new ApplicationException("fail") };
            var logger = new TestLogger<QueryLogService>();
            var sut = new QueryLogService(repo, logger);

            var logs = new[] { NewLog(1) };

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => sut.SaveBatchAsync(logs));
            Assert.Equal("fail", ex.Message);

            // Log happens after repository call; on exception, there should be no debug entry.
            Assert.Empty(logger.Entries);
        }
    }
}