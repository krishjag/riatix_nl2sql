using OpenAI;
using OpenAI.Chat;
using Riatix.Azure.NLToSQLEngine.Models;
using System.Text.Json;
using Riatix.Azure.NLToSQLEngine.Services.Clients;

namespace Riatix.Azure.NLToSQLEngine.Services.Translators
{
    public class OpenAIIntentTranslator : BaseIntentTranslator, IIntentTranslator
    {
        private readonly OpenAIClientInternal _client;
        private readonly string _model;                

        public OpenAIIntentTranslator(OpenAIClientInternal client, string model)
        {
            _client = client;
            _model = model;                        
        }

        // Seam for testability: allow tests to override how the chat is completed and text is extracted
        protected virtual async Task<string> CompleteChatAndGetTextAsync(string userQuery)
        {
            var chat = _client.GetChatClient(_model);

            var response = await chat.CompleteChatAsync(new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(_systemPrompt),
                ChatMessage.CreateUserMessage(userQuery)
            });

            return response.Value.Content[0].Text;
        }

        public async Task<IntentResponse> TranslateAsync(string userQuery)
        {
            var contentText = await CompleteChatAndGetTextAsync(userQuery);

            var intentResponse = JsonSerializer.Deserialize<IntentResponse>(contentText, _options)
                                 ?? new IntentResponse();

            ApplyDefaults(intentResponse);

            return intentResponse;
        }                
    }
}
