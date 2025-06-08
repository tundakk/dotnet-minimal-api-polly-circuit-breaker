namespace MinimalApiPolly.Models;

public record LlmRequest(string Prompt, int MaxTokens = 100, double Temperature = 0.7);
