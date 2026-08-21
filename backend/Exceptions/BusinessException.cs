using Microsoft.AspNetCore.Http;

namespace TemplateDormApi.Exceptions;

/// <summary>
/// 可预期业务错误，支持 API 业务码与 HTTP 状态码分离。
/// </summary>
public sealed class BusinessException : Exception
{
    public BusinessException(int code, string message, int httpStatus = StatusCodes.Status400BadRequest)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
    }

    public int Code { get; }

    public int HttpStatus { get; }
}
