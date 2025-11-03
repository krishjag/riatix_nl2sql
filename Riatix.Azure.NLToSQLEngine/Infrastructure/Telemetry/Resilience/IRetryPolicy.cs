namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience
{
    public interface IRetryPolicy
    {
        Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    }
}
