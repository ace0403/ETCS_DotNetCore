using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ETCS.Shared.Infrastructure.Audit;

/// <summary>
/// Writes one request-level audit row for a mutating HTTP call that did not already record an entity change.
/// </summary>
public sealed class AuditMutationFilter : IAsyncActionFilter
{
    private readonly IAuditLogWriter _auditLogWriter;

    public AuditMutationFilter(IAuditLogWriter auditLogWriter)
    {
        _auditLogWriter = auditLogWriter;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (!ShouldRecord(context))
        {
            return;
        }

        var descriptor = (ControllerActionDescriptor)context.ActionDescriptor;
        var statusCode = ResolveStatusCode(context, executed);
        var failed = executed.Exception is not null && !executed.ExceptionHandled || statusCode >= 400;
        var entityId = ResolveEntityId(context);
        var outcome = failed ? AuditOutcomes.Failure : AuditOutcomes.Success;
        string? message = failed ? executed.Exception?.Message : null;
        ApplyBusinessResult(executed, ref outcome, ref message);

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry
            {
                Action = ResolveAction(descriptor.ActionName, entityId),
                EntityType = descriptor.ControllerName,
                EntityId = entityId,
                Outcome = outcome,
                Message = message,
                NewValues = SanitizeArguments(context.ActionArguments)
            },
            context.HttpContext.RequestAborted);
    }

    private static bool ShouldRecord(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return false;
        }

        if (string.Equals(descriptor.ControllerName, "AuditLog", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsPost(method)
            && !HttpMethods.IsPut(method)
            && !HttpMethods.IsPatch(method)
            && !HttpMethods.IsDelete(method))
        {
            return false;
        }

        if (IsReadAction(descriptor.ActionName))
        {
            return false;
        }

        if (context.HttpContext.Items.ContainsKey(AuditAmbientContext.RecordedItemKey)
            || AuditAmbientContext.Current?.EntityAuditRecorded == true)
        {
            return false;
        }

        return true;
    }

    private static bool IsReadAction(string actionName) =>
        actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase)
        || actionName.StartsWith("List", StringComparison.OrdinalIgnoreCase)
        || actionName.StartsWith("Search", StringComparison.OrdinalIgnoreCase);

    private static int ResolveStatusCode(ActionExecutingContext context, ActionExecutedContext executed)
    {
        if (executed.Result is StatusCodeResult statusResult)
        {
            return statusResult.StatusCode;
        }

        if (executed.Result is ObjectResult objectResult && objectResult.StatusCode is int objectCode)
        {
            return objectCode;
        }

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return StatusCodes.Status500InternalServerError;
        }

        return context.HttpContext.Response.StatusCode;
    }

    private static string ResolveAction(string actionName, string? entityId)
    {
        if (string.Equals(actionName, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            return AuditActions.Delete;
        }

        if (string.Equals(actionName, "Save", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(entityId, out var id) && id > 0
                ? AuditActions.Update
                : AuditActions.Create;
        }

        return actionName;
    }

    private static string? ResolveEntityId(ActionExecutingContext context)
    {
        if (context.RouteData.Values.TryGetValue("id", out var routeId) && routeId is not null)
        {
            var text = routeId.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            var entityId = ReadEntityId(argument);
            if (!string.IsNullOrWhiteSpace(entityId))
            {
                return entityId;
            }
        }

        return null;
    }

    private static string? ReadEntityId(object? argument)
    {
        if (argument is null)
        {
            return null;
        }

        foreach (var name in new[] { "Id", "UserId" })
        {
            var property = argument.GetType().GetProperty(name);
            if (TryFormatId(property?.GetValue(argument), out var text))
            {
                return text;
            }
        }

        return null;
    }

    private static bool TryFormatId(object? value, out string text)
    {
        text = string.Empty;
        switch (value)
        {
            case int id when id > 0:
                text = id.ToString(CultureInfo.InvariantCulture);
                return true;
            case long id when id > 0:
                text = id.ToString(CultureInfo.InvariantCulture);
                return true;
            case decimal id when id > 0:
                text = id.ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                return false;
        }
    }

    private static void ApplyBusinessResult(ActionExecutedContext executed, ref string outcome, ref string? message)
    {
        object? value = executed.Result switch
        {
            JsonResult json => json.Value,
            ObjectResult objectResult => objectResult.Value,
            _ => null
        };
        if (value is null)
        {
            return;
        }

        var type = value.GetType();
        if (type.GetProperty("Success")?.GetValue(value) is bool success)
        {
            outcome = success ? AuditOutcomes.Success : AuditOutcomes.Failure;
        }

        if (type.GetProperty("Message")?.GetValue(value) is string businessMessage && !string.IsNullOrWhiteSpace(businessMessage))
        {
            message = businessMessage;
        }
    }

    private static Dictionary<string, object?> SanitizeArguments(IDictionary<string, object?> arguments)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in arguments)
        {
            if (value is null || IsSkippedArgument(value))
            {
                continue;
            }

            sanitized[key] = value;
        }

        return sanitized;
    }

    private static bool IsSkippedArgument(object value)
    {
        var type = value.GetType();
        if (type == typeof(CancellationToken) || typeof(Stream).IsAssignableFrom(type))
        {
            return true;
        }

        if (typeof(IFormFile).IsAssignableFrom(type) || typeof(IFormFileCollection).IsAssignableFrom(type))
        {
            return true;
        }

        var fullName = type.FullName ?? string.Empty;
        return fullName.StartsWith("Microsoft.AspNetCore.Http", StringComparison.Ordinal)
            || fullName.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal);
    }
}
