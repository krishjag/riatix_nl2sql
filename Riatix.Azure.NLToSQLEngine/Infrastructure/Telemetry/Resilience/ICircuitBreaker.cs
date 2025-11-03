namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Resilience
{
    public interface ICircuitBreaker
    {
        bool IsOpen { get; }
        void RecordSuccess();
        void RecordFailure();
        bool ShouldAttempt();
    }
}
