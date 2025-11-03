using Riatix.Azure.NLToSQLEngine.Providers.Anthropic;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Riatix.Azure.NLToSQLEngine.Services.Clients
{
    public class AnthropicClient : IAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly JsonSerializerOptions _jsonOptions;

        public AnthropicClient(string apiKey, string model, string baseUrl = "https://api.anthropic.com/")
        {
            _model = model;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(60)
            };            

            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            var request = new AnthropicChatRequest
            {
                Model = _model,
                System = new List<AnthropicSystemMessage> { 
                    new AnthropicSystemMessage { Text = systemPrompt }
                } ,
                Messages = new List<AnthropicMessage>
                {
                    new AnthropicMessage { Role = "user", Content = userPrompt }
                },
                max_tokens = 1024 * 3
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            int maxRetries = 3;
            int delayMs = 1000;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var parsed = JsonSerializer.Deserialize<AnthropicMessageResponse>(json, _jsonOptions);

                    string responseText = parsed?.Content.FirstOrDefault()?.Text!;

                    var pattern = @"```json\s*([\s\S]*?)```";
                    var match = Regex.Match(responseText, pattern);
                    if (match.Success)
                    {
                        responseText = match.Groups[1].Value.Trim();                        
                    }

                    return responseText ?? throw new Exception("No text content returned by Anthropic.");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt == maxRetries - 1)
                        throw new HttpRequestException($"Anthropic API unavailable after {maxRetries} retries.");

                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException(
                        $"Anthropic API error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {error}");
                }
            }

            throw new Exception("Unexpected retry loop exit in AnthropicClient.ChatAsync.");
        }
    }
}
