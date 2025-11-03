using System.Text.Json;

namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence
{
    public class FileQueryLogPersistence : IQueryLogPersistence
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public FileQueryLogPersistence()
        {            
            var basePath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath?? ".")!, "logs_data", "querybuffer");
            Directory.CreateDirectory(basePath);
            _filePath = Path.Combine(basePath, "pending-logs.jsonl");
        }

        public async Task SaveUnflushedAsync(IEnumerable<QueryLog> logs, CancellationToken cancellationToken = default)
        {
            await using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            foreach (var log in logs)
            {
                var json = JsonSerializer.Serialize(log, _options);
                await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json + Environment.NewLine), cancellationToken);
            }
        }

        public async Task<IReadOnlyList<QueryLog>> LoadUnflushedAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_filePath))
                return Array.Empty<QueryLog>();

            var lines = await File.ReadAllLinesAsync(_filePath, cancellationToken);
            var result = new List<QueryLog>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var log = JsonSerializer.Deserialize<QueryLog>(line, _options);
                    if (log != null) result.Add(log);
                }
                catch { /* skip malformed */ }
            }
            return result;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
            return Task.CompletedTask;
        }
    }
}
