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
    public class AnthropicIntentTranslatorTests
    {
        private sealed class StubMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public string? LastRequestBody { get; private set; }

            public StubMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = async (req, ct) =>
                {
                    if (req.Content != null)
                    {
                        LastRequestBody = await req.Content.ReadAsStringAsync(ct);
                    }
                    return await handler(req, ct);
                };
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => _handler(request, cancellationToken);
        }

        private static string AnthropicEnvelope(string contentText)
        {
            // Wraps the assistant "text" (which is our JSON payload) into Anthropic's message response shape.
            return $$"""
            {
              "id": "msg_1",
              "type": "message",
              "createdAt": 0,
              "content": [
                { "type": "text", "text": {{JsonSerializer.Serialize(contentText)}} }
              ]
            }
            """;
        }

        private static AnthropicClient CreateClientWithHandler(StubMessageHandler handler)
        {
            // Use any strings; we will swap the HttpClient under the hood via reflection.
            var client = new AnthropicClient("test-key", "test-model", "http://localhost/");
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost/")
            };

            // Replace the private _httpClient field to avoid real HTTP.
            var httpClientField = typeof(AnthropicClient)
                .GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(httpClientField);
            httpClientField!.SetValue(client, httpClient);

            return client;
        }

        private static AnthropicIntentTranslator CreateTranslator(AnthropicClient client)
        {
            EnsureSystemPromptFile();
            return new AnthropicIntentTranslator(client, "test-model");
        }

        private static void EnsureSystemPromptFile()
        {
            // BaseIntentTranslator pulls Constants.Prompts.SystemPrompt which reads Prompts/system_prompt.md.
            // Ensure it exists in the current working directory to avoid FileNotFoundException in tests.
            var dir = Path.Combine(Environment.CurrentDirectory, "Prompts");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, "system_prompt.md");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "Test System Prompt");
            }
        }

        [Fact]
        public async Task TranslateAsync_Deserializes_And_Applies_Defaults_For_Difference()
        {
            var handler = new StubMessageHandler((_, __) =>
            {
                var payload = "{\"intent\":\"difference\",\"filters\":{},\"parameters\":{}}";
                var envelope = AnthropicEnvelope(payload);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(envelope, Encoding.UTF8, "application/json")
                });
            });

            var client = CreateClientWithHandler(handler);
            var sut = CreateTranslator(client);

            var result = await sut.TranslateAsync("show difference");

            Assert.Equal("difference", result.Intent);
            Assert.NotNull(result.Filters);
            Assert.Contains("GA", result.Filters.CurrentState); // default applied
            Assert.Null(result.Parameters.TopN);
        }

        [Fact]
        public async Task TranslateAsync_Applies_Default_TopN_For_Leaderboard()
        {
            var handler = new StubMessageHandler((_, __) =>
            {
                var payload = "{\"intent\":\"leaderboard\",\"filters\":{},\"parameters\":{}}";
                var envelope = AnthropicEnvelope(payload);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(envelope, Encoding.UTF8, "application/json")
                });
            });

            var client = CreateClientWithHandler(handler);
            var sut = CreateTranslator(client);

            var result = await sut.TranslateAsync("leaderboard top regions");

            Assert.Equal("leaderboard", result.Intent);
            Assert.Equal(5, result.Parameters.TopN); // default applied
        }

        [Fact]
        public async Task TranslateAsync_DoesNot_Override_Provided_Values()
        {
            var handler = new StubMessageHandler((_, __) =>
            {
                // CurrentState provided as a single string (tests SingleOrArrayConverter) and TopN provided.
                var payload = """
                {
                  "intent": "leaderboard",
                  "filters": { "currentState": "Preview" },
                  "parameters": { "topN": 10 }
                }
                """;
                var envelope = AnthropicEnvelope(payload);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(envelope, Encoding.UTF8, "application/json")
                });
            });

            var client = CreateClientWithHandler(handler);
            var sut = CreateTranslator(client);

            var result = await sut.TranslateAsync("leaderboard with explicit values");

            Assert.Equal("leaderboard", result.Intent);
            Assert.Equal(10, result.Parameters.TopN);                        // should not be reset to 5
            Assert.Contains("Preview", result.Filters.CurrentState);         // should not be overridden to GA
        }

        [Fact]
        public async Task TranslateAsync_When_Response_Text_Is_Null_Returns_Default_Instance()
        {
            var handler = new StubMessageHandler((_, __) =>
            {
                // Anthropic returns content[0].text = "null"
                var envelope = AnthropicEnvelope("null");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(envelope, Encoding.UTF8, "application/json")
                });
            });

            var client = CreateClientWithHandler(handler);
            var sut = CreateTranslator(client);

            var result = await sut.TranslateAsync("any query");

            // Deserialize returns null -> fallback to new IntentResponse()
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.Intent);
            Assert.NotNull(result.Filters);
            Assert.NotNull(result.Parameters);
            Assert.NotNull(result.Clarifications);
            Assert.Empty(result.Clarifications);
        }

        [Fact]
        public async Task TranslateAsync_Sends_SystemPrompt_And_UserQuery_To_Client()
        {
            string? capturedRequest = null;

            var handler = new StubMessageHandler(async (req, ct) =>
            {
                capturedRequest = await req.Content!.ReadAsStringAsync(ct);

                var payload = "{\"intent\":\"list\",\"filters\":{},\"parameters\":{}}";
                var envelope = AnthropicEnvelope(payload);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(envelope, Encoding.UTF8, "application/json")
                };
            });

            var client = CreateClientWithHandler(handler);
            var sut = CreateTranslator(client);

            var userQuery = "list services in East US";
            var result = await sut.TranslateAsync(userQuery);
            Assert.Equal("list", result.Intent);

            // Validate request captured by our handler
            Assert.False(string.IsNullOrWhiteSpace(capturedRequest));
            using var doc = JsonDocument.Parse(capturedRequest!);

            // system[0].text should contain our test prompt
            var systemText = doc.RootElement.GetProperty("system")[0].GetProperty("text").GetString();
            Assert.Contains("You are a Natural Language", systemText);

            // messages[0].content should contain the user query
            var messageContent = doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
            Assert.Equal(userQuery, messageContent);
        }
    }
}