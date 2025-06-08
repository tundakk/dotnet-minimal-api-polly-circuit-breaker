namespace MinimalApiPolly.Models;

public record LlmSimpleRequest(string Prompt);

public record LlmSimpleResponse(string Response, bool Success, string? Error = null);
