using System;

namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry
{
    public class QueryLog
    {
        public string? CorrelationId { get; set; }
        public string? ClientIp { get; set; }
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string UserQuery { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? TranslatedIntent { get; set; }

        public string? IntentResponse { get; set; }
        public string? SqlQuery { get; set; }
        public string? ResponseSummary { get; set; }
        public int ResponseTimeMs { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
