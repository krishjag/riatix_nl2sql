using Swashbuckle.AspNetCore.Annotations;

namespace Riatix.Azure.NLToSQLEngine.Models
{
    public class QueryRequest
    {
        [SwaggerSchema("Natural language query, e.g. 'Show GA services in East US'")]
        public string UserQuery { get; set; } = string.Empty;

        [SwaggerSchema("Selected LLM model provider and model name, e.g. 'OpenAI: gpt-5-mini'")]
        public string? Model { get; set; }

        public bool GenerateSummary { get; set; } = false;
    }
}
