namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Queue
{
    public interface IQueryLogQueue
    {
        ValueTask EnqueueAsync(QueryLog log);
        IAsyncEnumerable<QueryLog> DequeueAllAsync(CancellationToken cancellationToken);
    }
}
