using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ETCS.API.Infrastructure.Auth;

public sealed class AuthApiKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.ApiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller) ||
            !string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", context.Document)] = []
        });
    }
}
