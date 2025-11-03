using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Models.Providers.Grok;
using Riatix.Azure.NLToSQLEngine.Services.Clients;
using System.Text.Json;
using static System.Collections.Specialized.BitVector32;

namespace Riatix.Azure.NLToSQLEngine.Services.Translators
{
    public class GrokIntentTranslator : BaseIntentTranslator, IIntentTranslator
    {
        private readonly GrokClient _client;
        private readonly string _model;

        public GrokIntentTranslator(GrokClient client, string model)
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
