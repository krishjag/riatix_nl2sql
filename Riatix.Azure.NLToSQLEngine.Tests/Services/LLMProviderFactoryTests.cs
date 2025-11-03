using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Riatix.Azure.NLToSQLEngine;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Services
{
    public class LLMProviderFactoryTests
    {
        private static IConfiguration BuildConfig(params (string Key, string Value)[] pairs)
        {
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in pairs)
                dict[k] = v;

            return new ConfigurationBuilder()
                .AddInMemoryCollection(dict!)
                .Build();
        }

        [Fact]
        public void GetAvailableProviders_SkipsActive_AndEnumeratesModels()
        {
            // Arrange: Provider sections must match Capitalize(providerKey) -> "Openai", "Grok"
            var config = BuildConfig(
                ("LLMProviders:Active:Name", "openai"),
                ("LLMProviders:Openai:0:Model", "Small"),
                ("LLMProviders:Openai:0:ModelID", "small-1"),
                ("LLMProviders:Openai:1:Model", "Large"),
                ("LLMProviders:Openai:1:ModelID", "large-1"),
                ("LLMProviders:Grok:0:Model", "Mixtral"),
                ("LLMProviders:Grok:0:ModelID", "mixtral-1")
            );

            var factory = new LLMProviderFactory(config, new NullLogger<LLMProviderFactory>());

            // Act
            var providers = factory.GetAvailableProviders().ToList();

            // Assert
            Assert.Equal(3, providers.Count);
            Assert.Contains(providers, p => p.Name == "Openai" && p.Model == "Small" && p.ModelID == "small-1");
            Assert.Contains(providers, p => p.Name == "Openai" && p.Model == "Large" && p.ModelID == "large-1");
            Assert.Contains(providers, p => p.Name == "Grok" && p.Model == "Mixtral" && p.ModelID == "mixtral-1");
        }

        [Fact]
        public void GetIntentTranslator_Throws_OnInvalidSelectionFormat()
        {
            // Arrange
            var config = BuildConfig();
            var factory = new LLMProviderFactory(config, new NullLogger<LLMProviderFactory>());

            // Act + Assert
            var ex = Assert.Throws<ArgumentException>(() => factory.GetIntentTranslator("OpenAI")); // Missing ": Model"
            Assert.Contains("Invalid model selection format", ex.Message);
        }

        [Fact]
        public void GetIntentTranslator_Throws_WhenProviderConfigMissing()
        {
            // Arrange: No "LLMProviders:Openai" section
            var config = BuildConfig(
                ("LLMProviders:Active:Name", "openai")
            );
            var factory = new LLMProviderFactory(config, new NullLogger<LLMProviderFactory>());

            // Act + Assert
            var ex = Assert.Throws<InvalidOperationException>(() => factory.GetIntentTranslator("OpenAI: TestModel"));
            Assert.Contains("Provider configuration not found for 'Openai'", ex.Message);
        }

        [Fact]
        public void GetIntentTranslator_Throws_WhenModelNotFound_ForProvider()
        {
            // Arrange: Provider exists, but requested model doesn't
            var config = BuildConfig(
                ("Openai_ApiKey", "dummy"),
                ("LLMProviders:Openai:0:Model", "Small"),
                ("LLMProviders:Openai:0:ModelID", "small-1"),
                ("LLMProviders:Openai:0:ClientType", typeof(string).AssemblyQualifiedName), // never reached
                ("LLMProviders:Openai:0:TranslatorType", typeof(string).AssemblyQualifiedName),
                ("LLMProviders:Openai:0:SummaryGeneratorType", typeof(string).AssemblyQualifiedName)
            );
            var factory = new LLMProviderFactory(config, new NullLogger<LLMProviderFactory>());

            // Act + Assert
            var ex = Assert.Throws<InvalidOperationException>(() => factory.GetIntentTranslator("OpenAI: Large"));
            Assert.Contains("Model 'Large' not found for provider 'Openai'", ex.Message);
        }
    }
}