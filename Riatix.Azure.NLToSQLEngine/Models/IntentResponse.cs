using Microsoft.Extensions.FileProviders.Physical;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Riatix.Azure.NLToSQLEngine.Models
{
    public class IntentResponse
    {
        /// <summary>
        /// The intent type (list, aggregation, difference, ranking, leaderboard).
        /// </summary>
        [SwaggerSchema("The detected intent type (list, aggregation, difference, ranking, leaderboard).")]
        public string Intent { get; set; } = string.Empty;

        /// <summary>
        /// Standardized filters (all arrays of strings).
        /// </summary>
        [SwaggerSchema("Structured filters applied to the SQL query.")] 
        public Filters Filters { get; set; } = new();

        /// <summary>
        /// Additional parameters (typed).
        /// </summary>
        [SwaggerSchema("Optional parameters such as TopN, GroupBy, and DifferenceMode.")] 
        public Parameters Parameters { get; set; } = new();

        /// <summary>
        /// Clarifications suggested by the LLM if ambiguity exists.
        /// </summary>
        [SwaggerSchema("Clarifications suggested by the LLM if ambiguity exists.")] 
        public List<string> Clarifications { get; set; } = new();   
        
        public NonLLMMetaData NonLLMMetaData { get; set; } = new();
    }

    /// <summary>
    /// Additional metadata added during processing pipelines.
    /// </summary>
    public class NonLLMMetaData
    {
        /// <summary>
        /// The raw user query that led to this intent.
        /// </summary>    
        [SwaggerSchema("The original user query that led to this intent.")]
        public string RawUserQuery { get; set; } = string.Empty;

        /// <summary>
        /// The correlation ID for tracing the request.
        /// </summary>  
        [SwaggerSchema("Unique correlation ID for request tracing.")]
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class Filters
    {
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> RegionName { get; set; } = new();

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> GeographyName { get; set; } = new();

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> MacroGeographyName { get; set; } = new();

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> OfferingName { get; set; } = new();

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> ProductSkuName { get; set; } = new();

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> CurrentState { get; set; } = new();

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> ProductCategoryName { get; set; } = new();

        public ExclusionFilter? Exclusions { get; set; }
    }

    public class ExclusionFilter
    {
        public string Scope { get; set; } = string.Empty; // "RegionName" | "GeographyName" | "MacroGeographyName"
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> ScopeValue { get; set; } = new();
    }

    public class Parameters
    {
        public int? TopN { get; set; }
        public string? GroupBy { get; set; }
        public string? CountDistinct { get; set; }

        public List<HavingCondition>? HavingCondition  { get; set; }
        public string SortOrder { get; set; } = "Descending";

        /// <summary>
        /// Indicates the type of set difference: "symmetric" or "directional".
        /// </summary>
        public string? DifferenceMode { get; set; }

        /// <summary>
        /// When directional, identifies the "source" comparison scope (Region, Geography, or Macro).
        /// </summary>
        public ComparisonScope? DifferenceSource { get; set; }

        /// <summary>
        /// When directional, identifies the "target" comparison scope (Region, Geography, or Macro).
        /// </summary>
        public ComparisonScope? DifferenceTarget { get; set; }
    }

    public class HavingCondition
    {
        public string Operator { get; set; } = string.Empty;
           
        public int Threshold { get; set; } = 0;
    }


    /// <summary>
    /// Defines a typed scope used for difference comparisons.
    /// Supports RegionName, GeographyName, and MacroGeographyName.
    /// </summary>
    public class ComparisonScope
    {
        /// <summary>
        /// The scope dimension name (e.g., "RegionName", "GeographyName", "MacroGeographyName").
        /// </summary>
        public string ScopeType { get; set; } = string.Empty;

        /// <summary>
        /// The value of the scope (e.g., "East US", "France", "Europe").
        /// </summary>
        public string ScopeValue { get; set; } = string.Empty;
    }

    public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = JsonSerializer.Deserialize<List<T>>(ref reader, options);
                return list ?? new List<T>();
            }
            else
            {
                var single = JsonSerializer.Deserialize<T>(ref reader, options);
                return new List<T> { single! };
            }
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
