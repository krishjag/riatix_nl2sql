using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Persistence
{
    public class SqlQueryLogRepository : IQueryLogRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlQueryLogRepository> _logger;

        public SqlQueryLogRepository(IConfiguration config, ILogger<SqlQueryLogRepository> logger)
        {
            _connectionString = config["ConnectionStrings_SqlServer"]
                ?? throw new InvalidOperationException("Missing connection string: ConnectionStrings_SqlServer");
            _logger = logger;
        }

        public async Task InsertAsync(QueryLog log, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT INTO [dbo].[query_logs]
                    ([UserId], [UserQuery], [Model], [TranslatedIntent],
                     [SqlQuery], [ResponseSummary], [ResponseTimeMs], [CreatedAt], 
                     [CorrelationId], [ClientIp], [IntentResponse])
                VALUES
                    (@UserId, @UserQuery, @Model, @TranslatedIntent,
                     @SqlQuery, @ResponseSummary, @ResponseTimeMs, SYSUTCDATETIME(), 
                     @CorrelationId, @ClientIp, @IntentResponse);";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", (object?)log.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserQuery", log.UserQuery);
            cmd.Parameters.AddWithValue("@Model", log.Model);
            cmd.Parameters.AddWithValue("@TranslatedIntent", (object?)log.TranslatedIntent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SqlQuery", (object?)log.SqlQuery ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ResponseSummary", (object?)log.ResponseSummary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ResponseTimeMs", log.ResponseTimeMs);
            cmd.Parameters.AddWithValue("@CorrelationId", (object?)log.CorrelationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientIp", (object?)log.ClientIp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IntentResponse", (object?)log.IntentResponse ?? DBNull.Value);            

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task InsertBatchAsync(IEnumerable<QueryLog> logs, CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();
            var parameters = new List<SqlParameter>();
            int i = 0;

            foreach (var log in logs)
            {
                sb.AppendLine($@"
                    INSERT INTO [dbo].[query_logs]
                        ([UserId], [UserQuery], [Model], [TranslatedIntent],
                         [SqlQuery], [ResponseSummary], [ResponseTimeMs], [CreatedAt],
                         [CorrelationId], [ClientIp], [IntentResponse])
                    VALUES
                        (@UserId{i}, @UserQuery{i}, @Model{i}, @TranslatedIntent{i},
                         @SqlQuery{i}, @ResponseSummary{i}, @ResponseTimeMs{i}, SYSUTCDATETIME(),
                        @CorrelationId{i}, @ClientIp{i}, @IntentResponse{i});");

                parameters.Add(new SqlParameter($"@UserId{i}", (object?)log.UserId ?? DBNull.Value));
                parameters.Add(new SqlParameter($"@UserQuery{i}", log.UserQuery));
                parameters.Add(new SqlParameter($"@Model{i}", log.Model));
                parameters.Add(new SqlParameter($"@TranslatedIntent{i}", (object?)log.TranslatedIntent ?? DBNull.Value));
                parameters.Add(new SqlParameter($"@SqlQuery{i}", (object?)log.SqlQuery ?? DBNull.Value));
                parameters.Add(new SqlParameter($"@ResponseSummary{i}", (object?)log.ResponseSummary ?? DBNull.Value));
                parameters.Add(new SqlParameter($"@ResponseTimeMs{i}", log.ResponseTimeMs));
                parameters.Add(new SqlParameter($"@CorrelationId{i}", (object?)log.CorrelationId ?? DBNull.Value));
                parameters.Add(new SqlParameter($"@ClientIp{i}", (object?)log.ClientIp ?? DBNull.Value));                
                parameters.Add(new SqlParameter($"@IntentResponse{i}", (object?)log.IntentResponse ?? DBNull.Value));
                i++;
            }

            if (i == 0) return;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(sb.ToString(), conn);
            cmd.Parameters.AddRange(parameters.ToArray());

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Inserted {Count} logs into SQL ({Rows} rows affected).", i, affected);
        }
    }
}
