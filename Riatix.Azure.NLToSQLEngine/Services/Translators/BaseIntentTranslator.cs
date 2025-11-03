using Riatix.Azure.NLToSQLEngine.Models;
using System.Text.Json;

namespace Riatix.Azure.NLToSQLEngine.Services.Translators
{
    public abstract class BaseIntentTranslator
    {
        protected string _systemPrompt;
        protected JsonSerializerOptions _options;

        protected BaseIntentTranslator()
        {
            _systemPrompt = Constants.Prompts.SystemPrompt;
            _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        protected void ApplyDefaults(IntentResponse intentResponse)
        {
            // difference - Default CurrentState = GA
            if (intentResponse.Intent.Equals("difference", StringComparison.OrdinalIgnoreCase)
                && (intentResponse.Filters.CurrentState == null || intentResponse.Filters.CurrentState.Count == 0))
            {
                intentResponse.Filters.CurrentState = new List<string> { "GA" };
            }

            // leaderboard - Default TopN = 5
            if (intentResponse.Intent.Equals("leaderboard", StringComparison.OrdinalIgnoreCase)
                && !intentResponse.Parameters.TopN.HasValue)
            {
                intentResponse.Parameters.TopN = 5;
            }
        }
    }
}
