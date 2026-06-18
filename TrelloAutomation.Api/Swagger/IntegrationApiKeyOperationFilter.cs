using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TrelloAutomation.Api.Swagger;

public sealed class IntegrationApiKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var relativePath = context.ApiDescription.RelativePath;
        if (relativePath is null || !relativePath.StartsWith("api/trello", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Integration-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Internal API key used to protect Trello automation endpoints.",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
