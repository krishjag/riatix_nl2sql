using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;
using Riatix.Azure.NLToSQLEngine.Services.Clients;
using System.Reflection;

namespace Riatix.Azure.NLToSQLEngine
{
    public class LLMProviderFactory : ILLMProviderFactory
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LLMProviderFactory> _logger;
        private readonly Dictionary<string, ProviderBundle> _providerCache = new();
        private readonly object _lock = new();

        public LLMProviderFactory(IConfiguration config, ILogger<LLMProviderFactory> logger)
        {
            _config = config;
            _logger = logger;
        }

        public LLMProviderResult<IIntentTranslator> GetIntentTranslator(string? provider = null)
        {
            var bundle = GetBundleByModel(provider!);
            return new LLMProviderResult<IIntentTranslator>(bundle.Translator, bundle.Name, bundle.Model);
        }

        public LLMProviderResult<ISummaryGenerator> GetSummaryGenerator(string? provider = null)
        {
            var bundle = GetBundleByModel(provider!);
            return new LLMProviderResult<ISummaryGenerator>(bundle.Summarizer, bundle.Name, bundle.Model);
        }

        public IAIClient GetClient(string? provider = null)
        {
            var bundle = GetBundleByModel(provider!);
            return bundle.Client;
        }

        private ProviderBundle GetBundleByModel(string modelSelection)
        {
            // Expect format "ProviderName: Model"
            var parts = modelSelection.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid model selection format: {modelSelection}");

            var provider = parts[0];
            var model = parts[1];

            // Ensure provider+model is initialized
            var bundle = GetOrCreateBundle(provider, model);
            if (!bundle.Model.Equals(model, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Requested model '{model}' not configured for provider {provider}");

            return bundle;
        }

        // ------------------------
        // Core bundle logic
        // ------------------------
        private ProviderBundle GetOrCreateBundle(string? provider, string? model)
        {
            var providerKey = ResolveProviderKey(provider);
            var modelKey = model ?? string.Empty;
            var cacheKey = $"{providerKey}:{modelKey}".ToLowerInvariant();

            lock (_lock)
            {
                if (_providerCache.TryGetValue(cacheKey, out var cached))
                    return cached;

                // Determine provider section (e.g., LLMProviders:Grok)
                var sectionName = Capitalize(providerKey);
                var providerSection = _config.GetSection($"LLMProviders:{sectionName}");
                if (!providerSection.Exists())
                    throw new InvalidOperationException($"Provider configuration not found for '{sectionName}'.");

                // Each provider now has multiple model entries
                var modelSections = providerSection.GetChildren().ToList();
                if (modelSections.Count == 0)
                    throw new InvalidOperationException($"No model configurations found under provider '{sectionName}'.");

                // Resolve the correct model entry (by explicit name or default to first)
                var modelSection = string.IsNullOrWhiteSpace(model)
                    ? modelSections.First()
                    : modelSections.FirstOrDefault(s =>
                          s.GetValue<string>("Model")?.Equals(model, StringComparison.OrdinalIgnoreCase) == true)
                      ?? throw new InvalidOperationException($"Model '{model}' not found for provider '{sectionName}'.");

                // Resolve config values
                var apiKey = _config[$"{sectionName}_ApiKey"]
                    ?? throw new InvalidOperationException($"{sectionName} ApiKey missing");
                var modelName = modelSection["Model"]
                    ?? throw new InvalidOperationException($"{sectionName} Model missing");
                var modelID = modelSection["ModelID"] 
                    ?? throw new InvalidOperationException($"{sectionName} ModelID missing");
                var domain = modelSection["Domain"];

                var clientType = ResolveType(modelSection["ClientType"]);
                var translatorType = ResolveType(modelSection["TranslatorType"]);
                var summaryType = ResolveType(modelSection["SummaryGeneratorType"]);

                _logger.LogInformation("Dynamically loading {Provider} with model {Model}", sectionName, modelName);

                // Create the components
                var client = (IAIClient)Activator.CreateInstance(clientType, apiKey, modelID, domain)!;
                var translator = (IIntentTranslator)Activator.CreateInstance(translatorType, client, modelID)!;
                var summarizer = (ISummaryGenerator)Activator.CreateInstance(summaryType, client, modelID)!;

                var bundle = new ProviderBundle(sectionName, modelName, client, translator, summarizer);
                _providerCache[cacheKey] = bundle;

                return bundle;
            }
        }


        private string ResolveProviderKey(string? provider) =>
            (provider ?? _config["LLMProviders:Active:Name"] ?? "openai").ToLower();

        private static string Capitalize(string value) =>
            char.ToUpper(value[0]) + value.Substring(1);

        private static Type ResolveType(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName));
            var type = Type.GetType(typeName);
            if (type == null) throw new InvalidOperationException($"Could not load type {typeName}");
            return type;
        }

        private record ProviderBundle(
            string Name,
            string Model,
            IAIClient Client,
            IIntentTranslator Translator,
            ISummaryGenerator Summarizer
        );

        public record ProviderInfo(string Name, string Model, string ModelID);

        public IEnumerable<ProviderInfo> GetAvailableProviders()
        {
            var providersSection = _config.GetSection("LLMProviders");

            foreach (var providerSection in providersSection.GetChildren())
            {
                // Skip the "Active" sub-section
                if (providerSection.Key.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Each provider (e.g., "OpenAI") contains an array of model objects
                foreach (var modelSection in providerSection.GetChildren())
                {
                    var modelName = modelSection["Model"] ?? string.Empty;
                    var modelID = modelSection["ModelID"] ?? string.Empty;
                    yield return new ProviderInfo(providerSection.Key, modelName, modelID);
                }
            }
        }

    }
}
