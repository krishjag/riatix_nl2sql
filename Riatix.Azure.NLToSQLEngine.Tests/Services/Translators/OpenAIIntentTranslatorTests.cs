using System.IO;
using System.Threading.Tasks;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services.Clients;
using Riatix.Azure.NLToSQLEngine.Services.Translators;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Services.Translators
{
    public class OpenAIIntentTranslatorTests
    {
        private static void EnsureSystemPromptFile()
        {
            // BaseIntentTranslator reads Prompts/system_prompt.md at construction time via Constants.Prompts.SystemPrompt
            // Ensure the relative file exists for tests.
            const string dir = "Prompts";
            const string path = "Prompts/system_prompt.md";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "Test system prompt");
            }
        }

        private sealed class TestableOpenAIIntentTranslator : OpenAIIntentTranslator
        {
            private string _nextJson = "{}";
            public string? CapturedUserQuery { get; private set; }

            public TestableOpenAIIntentTranslator(OpenAIClientInternal client, string model)
                : base(client, model)
            {
                // Avoid any test brittleness from the prompt content if needed; base already set it.
                // _systemPrompt is protected in BaseIntentTranslator; no need to change for behavior.
            }

            public void SetResponseJson(string json) => _nextJson = json;

            protected override Task<string> CompleteChatAndGetTextAsync(string userQuery)
            {
                CapturedUserQuery = userQuery;
                return Task.FromResult(_nextJson);
            }
        }

        private static TestableOpenAIIntentTranslator CreateSut(string responseJson)
        {
            EnsureSystemPromptFile();
            // We never hit the network due to override, so the apiKey can be anything.
            var client = new OpenAIClientInternal("sk-test");
            var sut = new TestableOpenAIIntentTranslator(client, "gpt-test");
            sut.SetResponseJson(responseJson);
            return sut;
        }

        [Fact]
        public async Task TranslateAsync_DeserializesResponse_AndReturnsIntentResponse()
        {
            var json = """
            {
              "intent": "list",
              "filters": {
                "RegionName": ["East US"],
                "CurrentState": ["GA"]
              },
              "parameters": {
                "topN": 3
              },
              "clarifications": ["none"],
              "nonLLMMetaData": {
                "rawUserQuery": "show me GA services in East US",
                "correlationId": "abc-123"
              }
            }
            """;

            var sut = CreateSut(json);

            var result = await sut.TranslateAsync("show me GA services in East US");

            Assert.NotNull(result);
            Assert.Equal("list", result.Intent);
            Assert.Equal(new[] { "East US" }, result.Filters.RegionName);
            Assert.Equal(new[] { "GA" }, result.Filters.CurrentState);
            Assert.Equal(3, result.Parameters.TopN);
            Assert.Equal("show me GA services in East US", result.NonLLMMetaData.RawUserQuery);
            Assert.Equal("abc-123", result.NonLLMMetaData.CorrelationId);

            // Ensure the user query passed into the chat call is the same
            Assert.Equal("show me GA services in East US", sut.CapturedUserQuery);
        }

        [Fact]
        public async Task TranslateAsync_AppliesDefault_CurrentState_ForDifferenceIntent()
        {
            var json = """
            {
              "intent": "difference",
              "filters": { },
              "parameters": { }
            }
            """;

            var sut = CreateSut(json);

            var result = await sut.TranslateAsync("difference query");

            Assert.Equal("difference", result.Intent);
            // Default CurrentState should be ["GA"]
            Assert.Equal(new[] { "GA" }, result.Filters.CurrentState);
        }

        [Fact]
        public async Task TranslateAsync_AppliesDefault_TopN_ForLeaderboardIntent()
        {
            var json = """
            {
              "intent": "leaderboard",
              "filters": { },
              "parameters": { }
            }
            """;

            var sut = CreateSut(json);

            var result = await sut.TranslateAsync("leaderboard query");

            Assert.Equal("leaderboard", result.Intent);
            Assert.Equal(5, result.Parameters.TopN);
        }

        [Fact]
        public async Task TranslateAsync_DoesNotOverride_TopN_WhenProvided_ForLeaderboardIntent()
        {
            var json = """
            {
              "intent": "leaderboard",
              "filters": { },
              "parameters": { "topN": 10 }
            }
            """;

            var sut = CreateSut(json);

            var result = await sut.TranslateAsync("leaderboard top 10");

            Assert.Equal("leaderboard", result.Intent);
            Assert.Equal(10, result.Parameters.TopN);
        }

        [Fact]
        public async Task TranslateAsync_WhenResponseIsJsonNull_ReturnsNewIntentResponseWithDefaults()
        {
            var json = "null"; // Deserializer returns null - translator constructs new IntentResponse()

            var sut = CreateSut(json);

            var result = await sut.TranslateAsync("any");

            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.Intent);
            Assert.Empty(result.Filters.RegionName);
            Assert.Null(result.Parameters.TopN);
            Assert.Equal(string.Empty, result.NonLLMMetaData.RawUserQuery);
            Assert.Equal(string.Empty, result.NonLLMMetaData.CorrelationId);
        }

        [Fact]
        public async Task TranslateAsync_Deserialization_IsCaseInsensitive()
        {
            var json = """
            {
              "InTeNt": "list",
              "FiLtErS": { "reGiOnNaMe": "West US" },
              "PaRaMeTeRs": { "ToPn": 2 }
            }
            """;

            var sut = CreateSut(json);

            var result = await sut.TranslateAsync("case-insensitive");

            Assert.Equal("list", result.Intent);
            Assert.Equal(new[] { "West US" }, result.Filters.RegionName);
            Assert.Equal(2, result.Parameters.TopN);
        }
    }
}
