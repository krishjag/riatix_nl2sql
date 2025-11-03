using Riatix.Azure.NLToSQLEngine.Models.Providers.Grok;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Riatix.Azure.NLToSQLEngine.Services.Clients
{
    public class GrokClient : IAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly JsonSerializerOptions _jsonOptions;

        public GrokClient(string apiKey, string model, string baseUrl = "https://api.x.ai/")
        {
            _model = model;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(60) // configurable
            };

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

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
            var request = new GrokChatRequest
            {
                Model = _model,
                Messages = new List<GrokMessage>
                {
                    new GrokMessage { Role = "system", Content = systemPrompt },
                    new GrokMessage { Role = "user", Content = userPrompt }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            // Retry with exponential backoff
            int maxRetries = 3;
            int delayMs = 1000;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using var response = await _httpClient.PostAsync(
                    "v1/chat/completions", content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var parsed = JsonSerializer.Deserialize<GrokChatResponse>(json, _jsonOptions);


                    string responseText = parsed?.Choices.FirstOrDefault()?.Message.Content!;

                    if(responseText is null)
                        throw new Exception("No response content from Grok API.");

                    responseText = responseText.Replace(@"```json", string.Empty);
                    responseText = responseText.Replace(@"```", string.Empty).Trim();

                    return responseText;
                        
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt == maxRetries - 1)
                        throw new HttpRequestException(
                            $"Grok API unavailable after {maxRetries} attempts. Status: {response.StatusCode}");

                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2; // exponential backoff
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException(
                        $"Grok API error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {error}");
                }
            }

            throw new Exception("Unexpected retry loop exit in GrokClient.ChatAsync.");
        }
    }
}
