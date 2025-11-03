using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Infrastructure.Telemetry.Persistence
{
    // Ensure these file-system tests don't run in parallel (they share the same on-disk path).
    [Collection("FileQueryLogPersistence-IO")]
    public class FileQueryLogPersistenceTests : IDisposable
    {
        private static string BasePath =>
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath ?? ".")!, "logs_data", "querybuffer");

        private static string FilePath => Path.Combine(BasePath, "pending-logs.jsonl");

        public FileQueryLogPersistenceTests()
        {
            Cleanup();
        }

        public void Dispose()
        {
            Cleanup();
        }

        private static void Cleanup()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch
            {
                // ignore cleanup exceptions in tests
            }
        }

        private static QueryLog NewLog(int id, string userQuery, string model, DateTime createdAtUtc)
            => new()
            {
                Id = id,
                UserQuery = userQuery,
                Model = model,
                CorrelationId = $"corr-{id}",
                ClientIp = "127.0.0.1",
                TranslatedIntent = null, // exercise null-ignores
                IntentResponse = null,
                SqlQuery = "SELECT 1",
                ResponseSummary = "ok",
                ResponseTimeMs = 42,
                CreatedAt = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
            };

        [Fact]
        public async Task LoadUnflushedAsync_WhenFileDoesNotExist_ReturnsEmpty()
        {
            // Arrange
            var sut = new FileQueryLogPersistence();

            // Act
            var result = await sut.LoadUnflushedAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task SaveUnflushedAsync_ThenLoadUnflushedAsync_RoundTripsLogs()
        {
            // Arrange
            var sut = new FileQueryLogPersistence();
            var createdAt = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);

            var logs = new[]
            {
                NewLog(1, "first", "gpt-x", createdAt),
                NewLog(2, "second", "gpt-y", createdAt.AddMinutes(1))
            };

            // Act
            await sut.SaveUnflushedAsync(logs);
            var loaded = await sut.LoadUnflushedAsync();

            // Assert
            Assert.Equal(2, loaded.Count);

            Assert.Collection(
                loaded,
                l =>
                {
                    Assert.Equal(1, l.Id);
                    Assert.Equal("first", l.UserQuery);
                    Assert.Equal("gpt-x", l.Model);
                    Assert.Equal("corr-1", l.CorrelationId);
                    Assert.Equal("127.0.0.1", l.ClientIp);
                    Assert.Equal("SELECT 1", l.SqlQuery);
                    Assert.Equal("ok", l.ResponseSummary);
                    Assert.Equal(42, l.ResponseTimeMs);
                    Assert.Equal(createdAt, l.CreatedAt);
                },
                l =>
                {
                    Assert.Equal(2, l.Id);
                    Assert.Equal("second", l.UserQuery);
                    Assert.Equal("gpt-y", l.Model);
                    Assert.Equal("corr-2", l.CorrelationId);
                    Assert.Equal("127.0.0.1", l.ClientIp);
                    Assert.Equal("SELECT 1", l.SqlQuery);
                    Assert.Equal("ok", l.ResponseSummary);
                    Assert.Equal(42, l.ResponseTimeMs);
                    Assert.Equal(createdAt.AddMinutes(1), l.CreatedAt);
                });
        }

        [Fact]
        public async Task SaveUnflushedAsync_Appends_ToExistingFile()
        {
            // Arrange
            var sut = new FileQueryLogPersistence();
            var t0 = new DateTime(2024, 01, 01, 00, 00, 00, DateTimeKind.Utc);

            // Act
            await sut.SaveUnflushedAsync(new[] { NewLog(1, "one", "m1", t0) });
            await sut.SaveUnflushedAsync(new[] { NewLog(2, "two", "m2", t0.AddSeconds(10)) });

            var loaded = await sut.LoadUnflushedAsync();

            // Assert
            Assert.Equal(2, loaded.Count);
            Assert.Equal(new[] { 1, 2 }, loaded.Select(l => l.Id).ToArray());
        }

        [Fact]
        public async Task ClearAsync_RemovesFile_AndSubsequentLoadReturnsEmpty()
        {
            // Arrange
            var sut = new FileQueryLogPersistence();
            await sut.SaveUnflushedAsync(new[] { NewLog(1, "to-clear", "m", DateTime.UtcNow) });
            Assert.True(File.Exists(FilePath)); // sanity

            // Act
            await sut.ClearAsync();

            // Assert
            Assert.False(File.Exists(FilePath));
            var loaded = await sut.LoadUnflushedAsync();
            Assert.Empty(loaded);
        }

        [Fact]
        public async Task LoadUnflushedAsync_SkipsBlankAndMalformedLines()
        {
            // Arrange: craft a file with valid, blank, malformed, valid
            Directory.CreateDirectory(BasePath);

            var good1 = NewLog(10, "good-one", "m", new DateTime(2024, 02, 01, 00, 00, 00, DateTimeKind.Utc));
            var good2 = NewLog(11, "good-two", "m", new DateTime(2024, 02, 01, 00, 01, 00, DateTimeKind.Utc));

            var json1 = System.Text.Json.JsonSerializer.Serialize(good1, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var json2 = System.Text.Json.JsonSerializer.Serialize(good2, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = string.Join(Environment.NewLine, new[]
            {
                json1,
                "",                       // blank line
                "this is not json",       // malformed
                json2,
                "   ",                    // whitespace line
            }) + Environment.NewLine;

            await File.WriteAllTextAsync(FilePath, content);

            var sut = new FileQueryLogPersistence();

            // Act
            var loaded = await sut.LoadUnflushedAsync();

            // Assert
            Assert.Equal(2, loaded.Count);
            Assert.Equal(new[] { 10, 11 }, loaded.Select(l => l.Id).ToArray());
            Assert.Equal(new[] { "good-one", "good-two" }, loaded.Select(l => l.UserQuery).ToArray());
        }

        [Fact]
        public async Task SaveUnflushedAsync_CanBeCanceled_NoPartialWrites()
        {
            // Arrange
            var sut = new FileQueryLogPersistence();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var logs = new[] { NewLog(1, "cancel", "m", DateTime.UtcNow) };

            // Act + Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveUnflushedAsync(logs, cts.Token));

            var loaded = await sut.LoadUnflushedAsync();
            Assert.Empty(loaded); // should not have written anything
        }
    }

    // Collection definition to disable parallelization for these IO tests
    [CollectionDefinition("FileQueryLogPersistence-IO", DisableParallelization = true)]
    public class FileQueryLogPersistenceIoCollectionDefinition { }
}