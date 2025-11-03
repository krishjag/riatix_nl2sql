namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence
{
    public interface IQueryLogService
    {
        Task SaveAsync(QueryLog log, CancellationToken cancellationToken = default);
        Task SaveBatchAsync(IEnumerable<QueryLog> logs, CancellationToken cancellationToken = default);
    }

    public interface IQueryLogRepository
    {
        Task InsertAsync(QueryLog log, CancellationToken cancellationToken = default);
        Task InsertBatchAsync(IEnumerable<QueryLog> logs, CancellationToken cancellationToken = default);
    }
}
