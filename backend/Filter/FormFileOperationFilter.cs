using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TemplateDormApi.Filters;

public class FormFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // 检查是否有 IFormFile 参数
        var fileParameters = context.ApiDescription.ParameterDescriptions
            .Where(p => p.Type == typeof(IFormFile) || p.Type == typeof(IFormFileCollection))
            .ToList();

        if (!fileParameters.Any())
            return;

        // 设置请求体为 multipart/form-data
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = fileParameters.ToDictionary(
                            p => p.Name,
                            p => new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            }
                        ),
                        Required = fileParameters.Where(p => p.IsRequired)
                                                  .Select(p => p.Name)
                                                  .ToHashSet()
                    }
                }
            }
        };

        // 移除已处理的参数（避免在 Parameters 中重复）
        foreach (var param in fileParameters)
        {
            var paramToRemove = operation.Parameters
                .FirstOrDefault(p => p.Name == param.Name);
            if (paramToRemove != null)
                operation.Parameters.Remove(paramToRemove);
        }
    }
}