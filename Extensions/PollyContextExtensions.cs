using Polly;

namespace MinimalApiPolly.Extensions;

public static class PollyContextExtensions
{
    public static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue("logger", out var logger) && logger is ILogger typedLogger)
        {
            return typedLogger;
        }
        return null;
    }
    
    public static void SetLogger(this Context context, ILogger logger)
    {
        context["logger"] = logger;
    }
}
