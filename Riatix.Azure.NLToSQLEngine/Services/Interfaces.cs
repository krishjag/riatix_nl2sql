using Microsoft.AspNetCore.Mvc;
using Riatix.Azure.NLToSQLEngine.Models;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using static Riatix.Azure.NLToSQLEngine.LLMProviderFactory;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    public interface IIntentTranslator
    {
        Task<IntentResponse> TranslateAsync(string userQuery);
    }


    public interface ISqlGenerator
    {
        string Generate(IntentResponse intentJson);
    }

    public interface ISqlExecutor
    {
        List<List<Dictionary<string, object>>> Execute(string sql);
    }

    public interface ISqlHelper
    {
        List<Dictionary<string, object>> GetAzureRegionMatrix();
        Dictionary<string, object> GetAzureRegionMatrixDataAction();

        Dictionary<string, string> GetAzureRegionMatrixDataCurrency();
    }

    public interface ISummaryGenerator
    {
        Task<string> SummarizeAsync(
            string userQuery,
            List<List<Dictionary<string, object>>> resultSets,
            IntentResponse intent);
    }

    public interface IQueryBuilder
    {
        string BuildQuery(IntentResponse response);
        bool CanHandle(IntentResponse intent);
    }
    public interface ILLMProviderFactory
    {
        LLMProviderResult<IIntentTranslator> GetIntentTranslator(string? provider = null);
        LLMProviderResult<ISummaryGenerator> GetSummaryGenerator(string? provider = null);
        IEnumerable<ProviderInfo> GetAvailableProviders();
    }

    public interface IServiceNameNormalizer
    {
        /// <summary>
        /// Normalize a user-provided service name (handles typos, reordering, etc.).
        /// Returns the canonical Azure service name or the original if no match passes threshold.
        /// </summary>
        string Normalize(string input);

        /// <summary>
        /// Configurable consensus threshold (0-1).
        /// </summary>
        double ConfidenceThreshold { get; set; }
    }

    /// <summary>
    /// Loads the canonical service name map from a prepacked binary file.
    /// </summary>
    public interface ICanonicalMapLoader
    {
        Dictionary<string, List<string>> Load();
    }


    public interface IRegionHierarchyCache
    {
        IReadOnlyCollection<string> GetRegionsForGeographies(IEnumerable<string> geographies);
        IReadOnlyCollection<string> GetRegionsForMacros(IEnumerable<string> macroGeographies);
        IReadOnlyCollection<string> GetAllRegions();
        bool TryGetParentGeography(string region, out string? geography, out string? macroGeography);

        Task PreWarmAsync(CancellationToken cancellationToken = default);
    }

    public interface IAIClient
    {
    }

    public interface IProductCategoryMap
    {
        IReadOnlyList<string> GetOfferingsForCategory(string categoryName);
        bool ContainsCategory(string categoryName);
        IReadOnlyDictionary<string, List<string>> GetAllCategories();
    }
}
