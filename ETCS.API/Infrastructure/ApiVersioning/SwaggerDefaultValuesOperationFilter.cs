using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ETCS.API.Infrastructure.ApiVersioning;

/// <summary>
/// Applies version-aware Swagger tweaks compatible with Swashbuckle 10 / OpenAPI 2.x.
/// </summary>
public sealed class SwaggerDefaultValuesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var parameter in operation.Parameters ?? [])
        {
            var description = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

            if (description is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(parameter.Description))
            {
                parameter.Description = description.ModelMetadata?.Description;
            }
        }
    }
}
