using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;

namespace TemplateDormApi.Services;

/// <summary>
/// 基于本地文件系统的图片存储服务。
/// </summary>
public class FileStorageService : IFileStorageService
{
    private const long DefaultMaxSizeBytes = 5L * 1024 * 1024;
    private const int HeaderLength = 12;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    };

    private readonly string _rootPath;
    private readonly long _maxSizeBytes;

    public FileStorageService(IConfiguration configuration)
    {
        var configuredRoot = configuration["Storage:RootPath"];
        _rootPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(AppContext.BaseDirectory, "uploads")
                : configuredRoot);
        _maxSizeBytes = configuration.GetValue<long?>("Storage:MaxSizeBytes")
            ?? DefaultMaxSizeBytes;
    }

    /// <inheritdoc />
    public async Task<FileUploadResultDto> SaveAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length <= 0 || file.Length > _maxSizeBytes)
        {
            throw new BusinessException(400, "文件大小不合法");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var extensionType = GetExtensionType(extension);
        var mimeType = GetMimeType(file.ContentType);
        if (extensionType == ImageType.Unknown || mimeType == ImageType.Unknown || extensionType != mimeType)
        {
            throw new BusinessException(400, "文件类型校验失败");
        }

        var now = DateTime.Now;
        var storageRef = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():D}{extension}";
        var directory = Path.Combine(_rootPath, now.ToString("yyyy"), now.ToString("MM"));
        var finalPath = ResolvePath(storageRef);
        string? temporaryPath = null;

        try
        {
            await using var source = file.OpenReadStream();
            var header = new byte[HeaderLength];
            var headerBytesRead = 0;
            while (headerBytesRead < header.Length)
            {
                var read = await source.ReadAsync(
                    header.AsMemory(headerBytesRead, header.Length - headerBytesRead),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                headerBytesRead += read;
            }

            var signatureType = GetSignatureType(header.AsSpan(0, headerBytesRead));
            if (signatureType == ImageType.Unknown || signatureType != extensionType)
            {
                throw new BusinessException(400, "文件类型校验失败");
            }

            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():D}.tmp");
            var totalBytesRead = (long)headerBytesRead;
            EnsureWithinSizeLimit(totalBytesRead);

            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await destination.WriteAsync(
                    header.AsMemory(0, headerBytesRead),
                    cancellationToken);

                var buffer = new byte[81920];
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    totalBytesRead += read;
                    EnsureWithinSizeLimit(totalBytesRead);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath, overwrite: false);
            temporaryPath = null;

            return new FileUploadResultDto { StorageRef = storageRef };
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<FileUrlDto> GetUrlAsync(
        string storageRef,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = ValidateAndResolvePath(storageRef);
        if (!File.Exists(filePath))
        {
            throw new BusinessException(404, "文件不存在", StatusCodes.Status404NotFound);
        }

        return Task.FromResult(new FileUrlDto
        {
            Url = $"{baseUrl.TrimEnd('/')}/uploads/{storageRef}"
        });
    }

    /// <inheritdoc />
    public Task<FileDeleteResultDto> DeleteAsync(
        string storageRef,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = ValidateAndResolvePath(storageRef);
        var deleted = File.Exists(filePath);
        if (deleted)
        {
            File.Delete(filePath);
        }

        return Task.FromResult(new FileDeleteResultDto
        {
            StorageRef = storageRef,
            Deleted = deleted
        });
    }

    private string ValidateAndResolvePath(string storageRef)
    {
        if (string.IsNullOrWhiteSpace(storageRef))
        {
            throw InvalidStorageRef();
        }

        var segments = storageRef.Split('/');
        if (segments.Length != 3 ||
            segments[0].Length != 4 ||
            !segments[0].All(char.IsAsciiDigit) ||
            segments[1].Length != 2 ||
            !int.TryParse(
                segments[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var month) ||
            month is < 1 or > 12)
        {
            throw InvalidStorageRef();
        }

        var extension = Path.GetExtension(segments[2]);
        var guidText = segments[2][..^extension.Length];
        if (!AllowedExtensions.Contains(extension) ||
            !Guid.TryParseExact(guidText, "D", out _))
        {
            throw InvalidStorageRef();
        }

        return ResolvePath(storageRef);
    }

    private string ResolvePath(string storageRef)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageRef));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(rootPrefix, comparison))
        {
            throw InvalidStorageRef();
        }

        return fullPath;
    }

    private void EnsureWithinSizeLimit(long byteCount)
    {
        if (byteCount > _maxSizeBytes)
        {
            throw new BusinessException(400, "文件大小不合法");
        }
    }

    private static ImageType GetExtensionType(string extension)
        => extension switch
        {
            ".jpg" or ".jpeg" => ImageType.Jpeg,
            ".png" => ImageType.Png,
            ".gif" => ImageType.Gif,
            ".webp" => ImageType.WebP,
            _ => ImageType.Unknown
        };

    private static ImageType GetMimeType(string contentType)
        => contentType switch
        {
            "image/jpeg" => ImageType.Jpeg,
            "image/png" => ImageType.Png,
            "image/gif" => ImageType.Gif,
            "image/webp" => ImageType.WebP,
            _ => ImageType.Unknown
        };

    private static ImageType GetSignatureType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 &&
            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ImageType.Jpeg;
        }

        if (header.Length >= 8 &&
            header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ImageType.Png;
        }

        if (header.Length >= 4 &&
            header[..4].SequenceEqual(new byte[] { 0x47, 0x49, 0x46, 0x38 }))
        {
            return ImageType.Gif;
        }

        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ImageType.WebP;
        }

        return ImageType.Unknown;
    }

    private static BusinessException InvalidStorageRef()
        => new(400, "Storage_Ref 格式非法");

    private static void TryDeleteTemporaryFile(string? temporaryPath)
    {
        if (temporaryPath is null)
        {
            return;
        }

        try
        {
            File.Delete(temporaryPath);
        }
        catch
        {
            // 保留原始异常，临时文件清理由后续运维处理。
        }
    }

    private enum ImageType
    {
        Unknown,
        Jpeg,
        Png,
        Gif,
        WebP
    }
}
