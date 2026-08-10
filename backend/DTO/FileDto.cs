namespace TemplateDormApi.DTO;

/// <summary>
/// 文件上传结果。
/// </summary>
public class FileUploadResultDto
{
    public string StorageRef { get; set; } = "";
}

/// <summary>
/// 文件公开访问地址。
/// </summary>
public class FileUrlDto
{
    public string Url { get; set; } = "";
}

/// <summary>
/// 文件删除结果。
/// </summary>
public class FileDeleteResultDto
{
    public string StorageRef { get; set; } = "";
    public bool Deleted { get; set; }
}
