using Microsoft.AspNetCore.Http;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 文件存储服务。
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// 校验并保存上传文件。
    /// </summary>
    Task<FileUploadResultDto> SaveAsync(IFormFile file, CancellationToken cancellationToken);

    /// <summary>
    /// 获取文件公开访问地址。
    /// </summary>
    Task<FileUrlDto> GetUrlAsync(
        string storageRef,
        string baseUrl,
        CancellationToken cancellationToken);

    /// <summary>
    /// 删除指定文件。
    /// </summary>
    Task<FileDeleteResultDto> DeleteAsync(
        string storageRef,
        CancellationToken cancellationToken);
}
