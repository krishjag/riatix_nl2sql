using Swashbuckle.AspNetCore.Annotations;
using System.Collections.Generic;

namespace Riatix.Azure.NLToSQLEngine.Models
{
    public class QueryResponse
    {
        [SwaggerSchema("Generated SQL query that was executed against dbo.products_info.")]
        public string? Sql { get; set; }

        [SwaggerSchema("Clarifications or disambiguations provided by the LLM.")] 
        public List<string>? Clarifications { get; set; }

        [SwaggerSchema("Natural language summary of what the SQL query does.")] 
        public string? NaturalLanguageSummary { get; set; }

        // Intent (parsed from LLM)
        [SwaggerSchema("Detected intent type, e.g. DifferencePattern, RankingPattern, SummaryPattern.")] 
        public string? Intent { get; set; }
        [SwaggerSchema("Structured JSON form of the interpreted query intent.")] 
        public string? IntentJson { get; set; }

        // Support multiple result sets
        [SwaggerSchema("Tabular result sets returned by the SQL query.")] 
        public List<List<Dictionary<string, object>>>? ResultSets { get; set; }

        // Provider info for intent translation
        [SwaggerSchema("The LLM provider for intent translation.")] 
        public string Provider { get; set; } = "Default";

        [SwaggerSchema("The LLM provider model for intent translation")] 
        public string ProviderModel { get; set; } = string.Empty;

        // Provider info for summarization
        [SwaggerSchema("The LLM provider for summarization.")] 
        public string SummaryProvider { get; set; } = "Default";

        [SwaggerSchema("The LLM provider model for summarization")]
        public string SummaryProviderModel { get; set; } = string.Empty;
    }
}
