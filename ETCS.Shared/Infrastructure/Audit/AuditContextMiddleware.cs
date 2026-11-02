using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ETCS.Shared.Infrastructure.Audit;

public sealed class AuditContextMiddleware
{
    public const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly string _sourceApp;
    private readonly string _authenticatedActorType;

    public AuditContextMiddleware(RequestDelegate next, string sourceApp, string authenticatedActorType)
    {
        _next = next;
        _sourceApp = sourceApp;
        _authenticatedActorType = authenticatedActorType;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var scope = CreateScope(context);
        using (AuditAmbientContext.Begin(scope))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationHeaderName] = scope.CorrelationId.ToString("D");
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    private AuditRequestScope CreateScope(HttpContext context)
    {
        var correlationId = Guid.NewGuid();
        if (context.Request.Headers.TryGetValue(CorrelationHeaderName, out var header)
            && Guid.TryParse(header.ToString(), out var parsed))
        {
            correlationId = parsed;
        }

        var scope = new AuditRequestScope
        {
            CorrelationId = correlationId,
            SourceApp = _sourceApp,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            HttpMethod = context.Request.Method,
            Path = context.Request.Path.Value
        };

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            scope.ActorType = _authenticatedActorType;
            var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("nameid");
            if (int.TryParse(raw, out var actorId) && actorId > 0)
            {
                scope.ActorId = actorId;
            }

            scope.ActorName = user.FindFirstValue(ClaimTypes.GivenName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
        }

        return scope;
    }
}

public static class AuditApplicationBuilderExtensions
{
    public static IApplicationBuilder UseAuditContext(
        this IApplicationBuilder app,
        string sourceApp,
        string authenticatedActorType)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<AuditContextMiddleware>(sourceApp, authenticatedActorType);
    }
}
