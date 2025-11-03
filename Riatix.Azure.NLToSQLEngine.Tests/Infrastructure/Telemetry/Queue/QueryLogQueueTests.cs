using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Queue;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Infrastructure.Telemetry.Queue
{
    public class QueryLogQueueTests
    {
        private static QueryLog NewLog(int id) => new()
        {
            Id = id,
            UserQuery = $"q{id}",
            Model = "m"
        };

        [Fact]
        public async Task EnqueueAsync_Then_DequeueAllAsync_YieldsInOrder()
        {
            var sut = new QueryLogQueue(capacity: 10);

            await sut.EnqueueAsync(NewLog(1));
            await sut.EnqueueAsync(NewLog(2));
            await sut.EnqueueAsync(NewLog(3));

            var received = new List<int>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            await foreach (var log in sut.DequeueAllAsync(cts.Token))
            {
                received.Add(log.Id);
                if (received.Count == 3) break;
            }

            Assert.Equal(new[] { 1, 2, 3 }, received);
        }

        [Fact]
        public async Task EnqueueBeyondCapacity_DropsWrites_WhenNoConsumer()
        {
            const int capacity = 5;
            var sut = new QueryLogQueue(capacity);

            // Fill to capacity
            for (int i = 1; i <= capacity; i++)
                await sut.EnqueueAsync(NewLog(i));

            // These should be dropped due to BoundedChannelFullMode.DropWrite
            for (int i = capacity + 1; i <= capacity + 10; i++)
                await sut.EnqueueAsync(NewLog(i));

            var received = new List<int>();

            // Drain what is present and stop; do not wait for more
            await foreach (var log in sut.DequeueAllAsync(CancellationToken.None))
            {
                received.Add(log.Id);
                if (received.Count == capacity) break;
            }

            Assert.Equal(capacity, received.Count);
            Assert.Equal(Enumerable.Range(1, capacity), received);
        }

        [Fact]
        public async Task DequeueAllAsync_HonorsCancellation_WhenWaiting()
        {
            var sut = new QueryLogQueue(capacity: 1);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            // No items enqueued; the reader will wait until canceled.
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in sut.DequeueAllAsync(cts.Token))
                {
                    // No-op
                }
            });
        }

        [Fact]
        public async Task EnqueueAsync_CompletesQuickly_WhenFull_DropWriteMode()
        {
            var sut = new QueryLogQueue(capacity: 2);

            // Fill channel
            await sut.EnqueueAsync(NewLog(1));
            await sut.EnqueueAsync(NewLog(2));

            // Next write should be dropped and should not block
            var extraWrite = sut.EnqueueAsync(NewLog(3)).AsTask();
            await extraWrite.WaitAsync(TimeSpan.FromSeconds(1));

            // Drain and verify only the first 2 were kept
            var received = new List<int>();
            await foreach (var log in sut.DequeueAllAsync(CancellationToken.None))
            {
                received.Add(log.Id);
                if (received.Count == 2) break;
            }

            Assert.Equal(new[] { 1, 2 }, received);
        }
    }
}