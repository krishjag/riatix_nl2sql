using System.Text.Json;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services.Clients;

namespace Riatix.Azure.NLToSQLEngine.Services.Translators
{
    public class AnthropicIntentTranslator : BaseIntentTranslator, IIntentTranslator
    {
        private readonly AnthropicClient _client;
        private readonly string _model;        

        public AnthropicIntentTranslator(AnthropicClient client, string model)
        {
            _client = client;
            _model = model;            
        }

        public async Task<IntentResponse> TranslateAsync(string userQuery)
        {
            var response = await _client.ChatAsync(_systemPrompt, userQuery);

            var intentResponse = JsonSerializer.Deserialize<IntentResponse>(response, _options)
                                 ?? new IntentResponse();

            ApplyDefaults(intentResponse);
            return intentResponse;
        }
    }
}
