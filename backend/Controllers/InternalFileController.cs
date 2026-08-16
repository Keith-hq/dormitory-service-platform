using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 供内部服务调用的文件管理接口。
/// </summary>
[ApiController]
[Route("api/internal/files")]
[ServiceKeyAuth]
public class InternalFileController : ControllerBase
{
    /// <summary>
    /// 契约 SVC-FILE-01 的业务模块标识白名单（module，可选）。
    /// </summary>
    private static readonly HashSet<string> AllowedModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "repair",
        "appeal",
        "visitor"
    };

    private readonly IFileStorageService _fileStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalFileController> _logger;

    public InternalFileController(
        IFileStorageService fileStorage,
        IConfiguration configuration,
        ILogger<InternalFileController> logger)
    {
        _fileStorage = fileStorage;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 上传图片文件。
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        string? module, // 注意：module 参数未加 [FromForm]，按 C-040 保留，Swagger 展示由 OperationFilter 处理
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(module) && !AllowedModules.Contains(module))
        {
            throw new BusinessException(400, "module 非法");
        }

        var result = await _fileStorage.SaveAsync(file, cancellationToken);
        _logger.LogInformation(
            "内部文件上传：StorageRef={StorageRef}，Size={Size}，Module={Module}",
            result.StorageRef,
            file.Length,
            module);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// 获取文件公开访问地址。
    /// </summary>
    [HttpGet("{**storageRef}")]
    public async Task<IActionResult> GetUrl(
        string storageRef,
        CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["Storage:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        }

        var result = await _fileStorage.GetUrlAsync(storageRef, baseUrl, cancellationToken);
        _logger.LogInformation("内部文件地址查询：StorageRef={StorageRef}", storageRef);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// 删除文件。
    /// </summary>
    [HttpDelete("{**storageRef}")]
    public async Task<IActionResult> Delete(
        string storageRef,
        CancellationToken cancellationToken)
    {
        var result = await _fileStorage.DeleteAsync(storageRef, cancellationToken);
        _logger.LogInformation(
            "内部文件删除：StorageRef={StorageRef}，Deleted={Deleted}",
            storageRef,
            result.Deleted);
        return Ok(ApiResponse.Ok(result));
    }
}
