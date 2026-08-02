namespace TemplateDormApi.DTO;

/// <summary>
/// 统一 API 返回格式
/// </summary>
public class ApiResponse<T>
{
    public int Code { get; set; } = 200;
    public string Message { get; set; } = "操作成功";
    public T? Data { get; set; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string message = "操作成功")
        => new() { Code = 200, Message = message, Data = data };

    public static ApiResponse<object> Error(int code, string message)
        => new() { Code = code, Message = message };

    public static ApiResponse<object> Created<T>(T data)
        => new() { Code = 201, Message = "创建成功", Data = data };
}
