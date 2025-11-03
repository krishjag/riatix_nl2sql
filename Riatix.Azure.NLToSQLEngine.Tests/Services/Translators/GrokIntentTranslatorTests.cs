using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services.Clients;
using Riatix.Azure.NLToSQLEngine.Services.Translators;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Services.Translators
{
    public class GrokIntentTranslatorTests
    {
        public GrokIntentTranslatorTests()
        {
            // Ensure the system prompt file exists for BaseIntentTranslator initialization.
            var promptsDir = Path.Combine(Environment.CurrentDirectory, "Prompts");
            Directory.CreateDirectory(promptsDir);
            var promptPath = Path.Combine(promptsDir, "system_prompt.md");
            if (!File.Exists(promptPath))
            {
                File.WriteAllText(promptPath, "test prompt");
            }
        }

        [Fact]
        public async Task TranslateAsync_ParsesResponse_WithoutApplyingDefaults()
        {
            // Arrange
            var intentJson = JsonSerializer.Serialize(new
            {
                intent = "list",
                filters = new
                {
                    RegionName = new[] { "East US" }
                },
                parameters = new
                {
                    TopN = 10
                },
                clarifications = Array.Empty<string>(),
                rawUserQuery = "list east us",
                correlationId = "cid-1"
            });

            var translator = CreateTranslatorWithChatResponse(intentJson);

            // Act
            var result = await translator.TranslateAsync("list east us");

            // Assert
            Assert.Equal("list", result.Intent);
            Assert.Contains("East US", result.Filters.RegionName);
            Assert.Equal(10, result.Parameters.TopN);
        }

        [Fact]
        public async Task TranslateAsync_AppliesDefaultTopN_ForLeaderboardWhenMissing()
        {
            // Arrange
            var intentJson = JsonSerializer.Serialize(new
            {
                intent = "leaderboard",
                filters = new { },
                parameters = new { },
                clarifications = Array.Empty<string>(),
                rawUserQuery = "top regions",
                correlationId = "cid-2"
            });

            var translator = CreateTranslatorWithChatResponse(intentJson);

            // Act
            var result = await translator.TranslateAsync("top regions");

            // Assert
            Assert.Equal("leaderboard", result.Intent);
            Assert.Equal(5, result.Parameters.TopN);
        }

        [Fact]
        public async Task TranslateAsync_AppliesDefaultCurrentState_ForDifferenceWhenMissing()
        {
            // Arrange
            var intentJson = JsonSerializer.Serialize(new
            {
                intent = "difference",
                filters = new
                {
                    CurrentState = Array.Empty<string>() // explicitly empty to trigger default
                },
                parameters = new { },
                clarifications = Array.Empty<string>(),
                rawUserQuery = "diff",
                correlationId = "cid-3"
            });

            var translator = CreateTranslatorWithChatResponse(intentJson);

            // Act
            var result = await translator.TranslateAsync("diff");

            // Assert
            Assert.Equal("difference", result.Intent);
            Assert.Contains("GA", result.Filters.CurrentState);
        }

        [Fact]
        public async Task TranslateAsync_ParsesResponse_WithCodeFences()
        {
            // Arrange
            var fenced = "```json\n" + JsonSerializer.Serialize(new
            {
                intent = "list",
                filters = new
                {
                    OfferingName = new[] { "Azure AI Studio" }
                },
                parameters = new { },
                clarifications = Array.Empty<string>(),
                rawUserQuery = "list offerings",
                correlationId = "cid-4"
            }) + "\n```";

            var translator = CreateTranslatorWithChatResponse(fenced);

            // Act
            var result = await translator.TranslateAsync("list offerings");

            // Assert
            Assert.Equal("list", result.Intent);
            Assert.Contains("Azure AI Studio", result.Filters.OfferingName);
        }

        [Fact]
        public async Task TranslateAsync_Throws_OnInvalidJson()
        {
            // Arrange
            var translator = CreateTranslatorWithChatResponse("not json");

            // Act + Assert
            await Assert.ThrowsAsync<JsonException>(() => translator.TranslateAsync("any"));
        }

        private static GrokIntentTranslator CreateTranslatorWithChatResponse(string content)
        {
            // Build a handler that returns a valid Grok-like chat completion JSON payload
            var handler = new StubHttpMessageHandler(_ =>
            {
                var responsePayload = JsonSerializer.Serialize(new
                {
                    id = "id-1",
                    @object = "chat.completion",
                    created = 0,
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new
                            {
                                role = "assistant",
                                content = content
                            },
                            finish_reason = "stop"
                        }
                    }
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responsePayload, Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost/"),
                Timeout = TimeSpan.FromSeconds(60)
            };

            // Instantiate GrokClient and replace its private _httpClient via reflection
            var grokClient = new GrokClient("test-api-key", "grok-2", "http://localhost/");

            var httpClientField = typeof(GrokClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(httpClientField);
            httpClientField!.SetValue(grokClient, httpClient);

            return new GrokIntentTranslator(grokClient, "grok-2");
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_responder(request));
        }
    }
}