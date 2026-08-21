using System.Net;
using System.Text.Json;

namespace DormitoryPlatform.API.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "未处理异常: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            int statusCode;
            int code;
            string message;
            if (exception is TemplateDormApi.Exceptions.BusinessException businessException)
            {
                statusCode = businessException.HttpStatus;
                code = businessException.Code;
                message = businessException.Message;
            }
            else if (exception is ArgumentException || exception is InvalidOperationException) // 可扩展自定义业务异常
            {
                statusCode = (int)HttpStatusCode.BadRequest;
                code = statusCode;
                message = exception.Message;  // 业务异常,可以改为返回具体信息
            }
            else
            {
                statusCode = (int)HttpStatusCode.InternalServerError;
                code = statusCode;
                message = "服务器内部错误，请稍后重试。";  // 通用错误
            }
            context.Response.StatusCode = statusCode;
            var response = new { code, message, data = (object?)null };
            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
