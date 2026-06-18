using Microsoft.Extensions.Options;
using TrelloAutomation.Api.Options;

namespace TrelloAutomation.Api.Middleware;

public sealed class ApiKeyMiddleware : IMiddleware
{
    private const string HeaderName = "X-Integration-Key";
    private readonly IOptions<IntegrationOptions> _options;

    public ApiKeyMiddleware(IOptions<IntegrationOptions> options)
    {
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api/trello", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var configuredKey = _options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Integration API key is not configured." });
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            !string.Equals(providedKey.ToString(), configuredKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing or invalid {HeaderName} header." });
            return;
        }

        await next(context);
    }
}
