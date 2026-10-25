using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Amlak.Config.Extensions.Swagger;

public sealed class ErrorResponseOperationFilter : IOperationFilter
{
    private static readonly Dictionary<int, string> ErrorDescriptions = new()
    {
        { 400, "درخواست نامعتبر — خطای اعتبارسنجی یا قاعده کسب‌وکار" },
        { 401, "احراز هویت نشده — توکن ارسال نشده یا منقضی شده" },
        { 403, "دسترسی ممنوع — نقش کافی ندارید" },
        { 404, "یافت نشد" },
        { 422, "خطای منطق تجاری" },
        { 500, "خطای داخلی سرور" },
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var problemDetailsRef = context.SchemaGenerator.GenerateSchema(
            typeof(ProblemDetails), context.SchemaRepository);

        foreach (var (statusCode, description) in ErrorDescriptions)
        {
            var key = statusCode.ToString();

            if (!operation.Responses.TryGetValue(key, out var response))
            {
                response = new OpenApiResponse { Description = description };
                operation.Responses[key] = response;
            }
            else
            {
                response.Description = description;
            }

            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType { Schema = problemDetailsRef }
            };
        }
    }
}
