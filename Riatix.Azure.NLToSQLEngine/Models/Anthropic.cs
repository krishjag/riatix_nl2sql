namespace Riatix.Azure.NLToSQLEngine.Providers.Anthropic
{
    public class AnthropicMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    public class AnthropicChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<AnthropicSystemMessage> System { get; set; } = new();
        public List<AnthropicMessage> Messages { get; set; } = new();
        public int max_tokens { get; set; } = 1024;
    }

    public class AnthropicSystemMessage
    {
        public string Type { get; set; } = "text";
        public string Text { get; set; } = string.Empty;

        public AnthropicPromptCacheControl cache_control { get; set; } = new AnthropicPromptCacheControl();
    }

    public class AnthropicPromptCacheControl
    {
        public string Type { get; set; } = "ephemeral";
        public string Ttl { get; set; } = "1h";
    }

    public class AnthropicContentBlock
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class AnthropicMessageResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long CreatedAt { get; set; }
        public List<AnthropicContentBlock> Content { get; set; } = new();
    }
}
