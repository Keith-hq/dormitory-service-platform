using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class FileStorageServiceTests
{
    private static readonly byte[] ValidPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x01, 0x02, 0x03, 0x04
    };

    [Fact]
    public async Task SaveAsync_ValidPng_ReturnsRefAndWritesFile()
    {
        await WithStorageAsync(async (root, service) =>
        {
            var result = await service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/png"),
                CancellationToken.None);

            Assert.EndsWith(".png", result.StorageRef);
            Assert.Equal(ValidPng, await File.ReadAllBytesAsync(Path.Combine(root, result.StorageRef)));
        });
    }

    [Fact]
    public async Task SaveAsync_RejectsExtensionMimeMismatch()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/jpeg"),
                CancellationToken.None));

            Assert.Equal(400, exception.Code);
        });
    }

    [Fact]
    public async Task SaveAsync_RejectsFakeContentType()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(
                new FakeFormFile(new byte[16], "photo.png", "image/png"),
                CancellationToken.None));

            Assert.Equal(400, exception.Code);
        });
    }

    [Fact]
    public async Task SaveAsync_RejectsDisallowedExtension()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.exe", "image/png"),
                CancellationToken.None));

            Assert.Equal(400, exception.Code);
        });
    }

    [Fact]
    public async Task SaveAsync_AcceptsUppercaseExtension()
    {
        await WithStorageAsync(async (root, service) =>
        {
            var result = await service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.PNG", "image/png"),
                CancellationToken.None);

            Assert.EndsWith(".png", result.StorageRef);
            Assert.True(File.Exists(Path.Combine(root, result.StorageRef)));
        });
    }

    [Fact]
    public async Task SaveAsync_RejectsOversizeAndEmpty()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var oversized = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/png"),
                CancellationToken.None));
            var empty = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(
                new FakeFormFile(Array.Empty<byte>(), "photo.png", "image/png"),
                CancellationToken.None));

            Assert.Equal(400, oversized.Code);
            Assert.Equal(400, empty.Code);
        }, maxSizeBytes: ValidPng.Length - 1);
    }

    [Fact]
    public async Task SaveAsync_TwoUploadsGenerateDistinctRefs()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var first = await service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/png"),
                CancellationToken.None);
            var second = await service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/png"),
                CancellationToken.None);

            Assert.NotEqual(first.StorageRef, second.StorageRef);
        });
    }

    [Fact]
    public async Task SaveAsync_CancellationLeavesNoPartialFile()
    {
        await WithStorageAsync(async (root, service) =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/png"),
                cancellation.Token));

            Assert.False(Directory.Exists(root) &&
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any());
        });
    }

    [Fact]
    public async Task GetUrlAsync_ReturnsBaseUrlPlusUploadsPath()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var upload = await service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/png"),
                CancellationToken.None);

            var result = await service.GetUrlAsync(
                upload.StorageRef,
                "https://files.example/",
                CancellationToken.None);

            Assert.Equal($"https://files.example/uploads/{upload.StorageRef}", result.Url);
        });
    }

    [Fact]
    public async Task GetUrlAsync_MissingFile_Throws404()
    {
        await WithStorageAsync(async (_, service) =>
        {
            const string storageRef = "2026/08/11111111-1111-1111-1111-111111111111.png";

            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.GetUrlAsync(
                storageRef,
                "https://files.example",
                CancellationToken.None));

            Assert.Equal(404, exception.Code);
            Assert.Equal(StatusCodes.Status404NotFound, exception.HttpStatus);
        });
    }

    [Fact]
    public async Task GetUrlAsync_RejectsInvalidRef()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var invalidRefs = new[]
            {
                "2026/13/11111111-1111-1111-1111-111111111111.png",
                "2026/08/not-a-guid.png",
                "2026/08/11111111-1111-1111-1111-111111111111.exe"
            };

            foreach (var storageRef in invalidRefs)
            {
                var exception = await Assert.ThrowsAsync<BusinessException>(() => service.GetUrlAsync(
                    storageRef,
                    "https://files.example",
                    CancellationToken.None));
                Assert.Equal(400, exception.Code);
            }
        });
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        await WithStorageAsync(async (root, service) =>
        {
            var upload = await service.SaveAsync(
                new FakeFormFile(ValidPng, "photo.png", "image/png"),
                CancellationToken.None);

            var result = await service.DeleteAsync(upload.StorageRef, CancellationToken.None);

            Assert.True(result.Deleted);
            Assert.Equal(upload.StorageRef, result.StorageRef);
            Assert.False(File.Exists(Path.Combine(root, upload.StorageRef)));
        });
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_ReturnsDeletedFalse()
    {
        await WithStorageAsync(async (_, service) =>
        {
            const string storageRef = "2026/08/11111111-1111-1111-1111-111111111111.png";

            var result = await service.DeleteAsync(storageRef, CancellationToken.None);

            Assert.False(result.Deleted);
            Assert.Equal(storageRef, result.StorageRef);
        });
    }

    [Fact]
    public async Task DeleteAsync_RejectsPathTraversalRef()
    {
        await WithStorageAsync(async (_, service) =>
        {
            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(
                "../../etc/passwd",
                CancellationToken.None));

            Assert.Equal(400, exception.Code);
        });
    }

    private static async Task WithStorageAsync(
        Func<string, FileStorageService, Task> action,
        long? maxSizeBytes = null)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var settings = new Dictionary<string, string?>
        {
            ["Storage:RootPath"] = root
        };
        if (maxSizeBytes.HasValue)
        {
            settings["Storage:MaxSizeBytes"] = maxSizeBytes.Value.ToString();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        try
        {
            await action(root, new FileStorageService(configuration));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

internal sealed class FakeFormFile : IFormFile
{
    private readonly byte[] _content;

    public FakeFormFile(byte[] content, string fileName, string contentType)
    {
        _content = content;
        FileName = fileName;
        ContentType = contentType;
    }

    public string ContentType { get; }
    public string ContentDisposition { get; } = "form-data";
    public IHeaderDictionary Headers { get; } = new HeaderDictionary();
    public long Length => _content.LongLength;
    public string Name { get; } = "file";
    public string FileName { get; }

    public void CopyTo(Stream target)
        => target.Write(_content, 0, _content.Length);

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        => target.WriteAsync(_content.AsMemory(), cancellationToken).AsTask();

    public Stream OpenReadStream()
        => new MemoryStream(_content, writable: false);
}
