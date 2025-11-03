using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence
{
    public class QueryLogService : IQueryLogService
    {
        private readonly IQueryLogRepository _repository;
        private readonly ILogger<QueryLogService> _logger;

        public QueryLogService(IQueryLogRepository repository, ILogger<QueryLogService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task SaveAsync(QueryLog log, CancellationToken cancellationToken = default)
        {
            await _repository.InsertAsync(log, cancellationToken);
        }

        public async Task SaveBatchAsync(IEnumerable<QueryLog> logs, CancellationToken cancellationToken = default)
        {
            await _repository.InsertBatchAsync(logs, cancellationToken);
            _logger.LogDebug("Saved {Count} logs via repository.", logs is ICollection<QueryLog> c ? c.Count : -1);
        }
    }
}
