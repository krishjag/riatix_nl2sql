using OpenAI;
using OpenAI.Chat;
using Riatix.Azure.NLToSQLEngine.Models;
using System.Text.Json;

namespace Riatix.Azure.NLToSQLEngine.Services.Summarizers
{
    public class OpenAISummaryGenerator : ISummaryGenerator
    {
        private readonly OpenAIClient _client;
        private readonly string _model;

        public OpenAISummaryGenerator(OpenAIClient client, string model)
        {
            _client = client;
            _model = model;
        }

        /// <summary>
        /// Summarizes query results into plain natural language using the configured LLM provider.
        /// </summary>
        public async Task<string> SummarizeAsync(
            string userQuery,
            List<List<Dictionary<string, object>>> resultSets,
            IntentResponse intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (resultSets == null) throw new ArgumentNullException(nameof(resultSets));

            string summarySystemPrompt = @"
                                            You are a data summarizer for Microsoft Azure product availability.

                                            Your task is to explain SQL query results in clear, concise natural language 
                                            that a non-technical business stakeholder can understand.

                                            ### Rules
                                            1. Focus on clarity: describe what the results mean, not how SQL works.  
                                            2. Summaries should be short, accurate, and natural sounding.  
                                            3. If results show differences across regions (intent = 'difference'), 
                                               highlight which regions have or do not have the services.  
                                            4. If results show rankings (intent = 'ranking'), state which region/geography 
                                               has more/fewer services.  
                                            5. If results show leaderboards (intent = 'leaderboard'), list the top groups 
                                               with counts.  
                                            6. If aggregation, report the counts in plain language.  
                                            7. If listing rows, summarize the scope (e.g., '5 services found in East US').  
                                            8. Handle empty results by saying 'No services found for the given filters.'  

                                            ### Output
                                            Return only natural language text. Do not return JSON, SQL, or code.
                                            ";

            var chat = _client.GetChatClient(_model);

            var response = await chat.CompleteChatAsync(new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(summarySystemPrompt),
                ChatMessage.CreateUserMessage(JsonSerializer.Serialize(new
                {
                    intent = intent.Intent,
                    filters = intent.Filters,
                    parameters = intent.Parameters,
                    results = resultSets.Take(100)
                }))
            });

            return response.Value.Content.FirstOrDefault()?.Text?.Trim() ?? string.Empty;
        }
    }
}
