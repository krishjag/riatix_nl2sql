using OpenAI;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.Services.Clients
{
    public class OpenAIClientInternal : OpenAIClient, IAIClient
    {
        public OpenAIClientInternal(string apiKey, string? apiBaseUrl = null, string? apiVersion = null) : base(apiKey)
        {
        }
    }
}
