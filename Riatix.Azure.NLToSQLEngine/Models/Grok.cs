namespace Riatix.Azure.NLToSQLEngine.Models.Providers.Grok
{
    public class GrokChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<GrokMessage> Messages { get; set; } = new();
        public double Temperature { get; set; } = 0.2;
    }

    public class GrokMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    public class GrokChatResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public List<GrokChoice> Choices { get; set; } = new();
    }

    public class GrokChoice
    {
        public int Index { get; set; }
        public GrokMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = string.Empty;
    }
}
