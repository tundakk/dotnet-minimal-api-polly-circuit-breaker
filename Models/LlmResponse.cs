namespace MinimalApiPolly.Models;

public record LlmResponse(
    string Id,
    string Object,
    long Created,
    string Model,
    List<Choice> Choices,
    Usage Usage)
{
    public string GetCompletionText() => Choices?.FirstOrDefault()?.Text ?? string.Empty;
}

public record Choice(string Text, int Index, object? LogProbs, string FinishReason);

public record Usage(int PromptTokens, int CompletionTokens, int TotalTokens);
