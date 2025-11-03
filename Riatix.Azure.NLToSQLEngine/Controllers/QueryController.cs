using Microsoft.AspNetCore.Mvc;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Queue;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Models.Examples;
using Riatix.Azure.NLToSQLEngine.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Diagnostics;
using System.Text.Json;
using static Riatix.Azure.NLToSQLEngine.LLMProviderFactory;


namespace Riatix.Azure.NLToSQLEngine.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {
        private readonly ILLMProviderFactory _factory;
        private readonly ISqlGenerator _sqlGenerator;
        private readonly ISqlExecutor _executor;
        private readonly ISqlHelper _sqlHelper;
        private readonly IQueryLogQueue _logQueue;
        private readonly ILogger<QueryController> _logger;

        public QueryController(
            ILLMProviderFactory factory,
            ISqlGenerator sqlGenerator,
            ISqlExecutor executor,
            ISqlHelper sqlHelper,
            IQueryLogQueue logQueue,
            ILogger<QueryController> logger)
        {
            _factory = factory;
            _sqlGenerator = sqlGenerator;
            _executor = executor;
            _sqlHelper = sqlHelper;
            _logQueue = logQueue;
            _logger = logger;
        }

        [HttpPost("ask")]
        [ProducesResponseType(typeof(QueryResponse), 200)]
        [SwaggerRequestExample(typeof(QueryRequest), typeof(QueryRequestExample))]
        [SwaggerResponseExample(200, typeof(QueryResponseMultipleExamples))]
        public async Task<ActionResult<QueryResponse>> Ask([FromBody] QueryRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = GetCorrelationId(HttpContext) ?? HttpContext.TraceIdentifier;
            var clientIp = GetClientIpAddress(HttpContext);

            try
            {
                // Step 1: Resolve translator & summarizer from factory
                var translatorResult = _factory.GetIntentTranslator(request.Model);
                var summarizerResult = _factory.GetSummaryGenerator(request.Model);

                // Step 2: Intent -> SQL -> Execution
                var intentResponse = await translatorResult.Instance.TranslateAsync(request.UserQuery);
                intentResponse.NonLLMMetaData.RawUserQuery = request.UserQuery;
                intentResponse.NonLLMMetaData.CorrelationId = correlationId;

                var sql = _sqlGenerator.Generate(intentResponse);
                var resultSets = _executor.Execute(sql);

                string summary = "No summary generated.";

                if (request.GenerateSummary)
                {
                    // Step 3: Summarization (disabled for now)
                    summary = await summarizerResult.Instance.SummarizeAsync(
                        request.UserQuery,
                        resultSets,
                        intentResponse
                    );
                }

                stopwatch.Stop();

                // Step 4: Build response
                var response = new QueryResponse
                {
                    Sql = sql,
                    Clarifications = intentResponse.Clarifications.ToList(),
                    NaturalLanguageSummary = summary,
                    ResultSets = resultSets,
                    Intent = intentResponse.Intent,
                    IntentJson = JsonSerializer.Serialize(intentResponse, new JsonSerializerOptions { WriteIndented = true }),
                    Provider = translatorResult.ProviderName,
                    ProviderModel = translatorResult.Model,
                    SummaryProvider = summarizerResult.ProviderName,
                    SummaryProviderModel = summarizerResult.Model
                };

                // Step 5: Asynchronously enqueue telemetry log (non-blocking)
                var log = new QueryLog
                {
                    CorrelationId = correlationId,
                    ClientIp = clientIp,
                    UserId = HttpContext.User?.Identity?.Name ?? "anonymous",
                    UserQuery = request.UserQuery!,
                    Model = request.Model!,
                    TranslatedIntent = intentResponse.Intent,
                    IntentResponse = JsonSerializer.Serialize(intentResponse, new JsonSerializerOptions { WriteIndented = true }),
                    SqlQuery = sql,
                    ResponseSummary = summary,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    CreatedAt = DateTime.UtcNow
                };

                _ = _logQueue.EnqueueAsync(log);
                _logger.LogDebug("Telemetry log enqueued. CorrelationId={CorrelationId}, ClientIp={ClientIp}", correlationId, clientIp);

                return Ok(response);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error processing query request. CorrelationId={CorrelationId}", correlationId);

                var failLog = new QueryLog
                {
                    CorrelationId = correlationId,
                    ClientIp = clientIp,
                    UserId = HttpContext.User?.Identity?.Name ?? "anonymous",
                    UserQuery = request.UserQuery!,
                    Model = request.Model!,
                    TranslatedIntent = null,
                    IntentResponse = null,
                    SqlQuery = null,
                    ResponseSummary = $"Error: {ex.Message}",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    CreatedAt = DateTime.UtcNow
                };

                _ = _logQueue.EnqueueAsync(failLog);

                return StatusCode(500, new { error = ex.Message, correlationId });
            }
        }

        [HttpGet("providers")]
        public ActionResult<IEnumerable<ProviderInfo>> GetProviders()
        {
            var factory = HttpContext.RequestServices.GetRequiredService<ILLMProviderFactory>();
            var providers = factory.GetAvailableProviders();
            return Ok(providers);
        }

        [HttpGet("productmatrix")]
        public ActionResult<string> GetAzureRegionMatrixAsJson()
        {
            return JsonSerializer.Serialize(_sqlHelper.GetAzureRegionMatrix(), new JsonSerializerOptions { WriteIndented = false });
        }

        [HttpGet("productmatrix/action")]
        public ActionResult<string> GetAzureRegionMatrixDataActionAsJson()
        {
            return JsonSerializer.Serialize(_sqlHelper.GetAzureRegionMatrixDataAction(), new JsonSerializerOptions { WriteIndented = false });
        }

        [HttpGet("productmatrix/datacurrency")]
        public ActionResult<string> GetAzureRegionMatrixDataCurrencyAsJson()
        {
            return JsonSerializer.Serialize(_sqlHelper.GetAzureRegionMatrixDataCurrency(), new JsonSerializerOptions { WriteIndented = false });
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            // Check proxy headers first (common in Azure App Gateway / Front Door)
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                // Could be a comma-separated list if there are multiple hops
                return forwarded.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static string GetCorrelationId(HttpContext context)
        {
            return context.Request.Headers["X-NL2SQL-Request-ID"].FirstOrDefault()!;                
        }
    }
}
