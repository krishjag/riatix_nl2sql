namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence
{
    public interface IQueryLogPersistence
    {
        Task SaveUnflushedAsync(IEnumerable<QueryLog> logs, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<QueryLog>> LoadUnflushedAsync(CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
