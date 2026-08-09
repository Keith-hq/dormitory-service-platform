using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TemplateDormApi.Controllers;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class InternalFileControllerTests
{
    [Fact]
    public async Task Upload_ReturnsOkWithStorageRef()
    {
        var expected = new FileUploadResultDto
        {
            StorageRef = "2026/08/11111111-1111-1111-1111-111111111111.png"
        };
        var service = new FakeFileStorageService { UploadResult = expected };
        var controller = CreateController(service);

        var actionResult = await controller.Upload(
            new FakeFormFile(new byte[] { 1 }, "photo.png", "image/png"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<FileUploadResultDto>>(okResult.Value);
        Assert.Same(expected, response.Data);
    }

    [Fact]
    public async Task GetUrl_ReturnsOkWithUrl()
    {
        var expected = new FileUrlDto { Url = "https://files.example/uploads/test.png" };
        var service = new FakeFileStorageService { UrlResult = expected };
        var controller = CreateController(service);

        var actionResult = await controller.GetUrl(
            "2026/08/11111111-1111-1111-1111-111111111111.png",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<FileUrlDto>>(okResult.Value);
        Assert.Same(expected, response.Data);
        Assert.Equal("https://files.example", service.BaseUrl);
    }

    [Fact]
    public async Task Delete_ReturnsOkWithDeleted()
    {
        var expected = new FileDeleteResultDto
        {
            StorageRef = "2026/08/11111111-1111-1111-1111-111111111111.png",
            Deleted = true
        };
        var service = new FakeFileStorageService { DeleteResult = expected };
        var controller = CreateController(service);

        var actionResult = await controller.Delete(expected.StorageRef, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<FileDeleteResultDto>>(okResult.Value);
        Assert.Same(expected, response.Data);
    }

    [Fact]
    public void Controller_HasServiceKeyAuthAttribute()
    {
        Assert.NotNull(
            typeof(InternalFileController)
                .GetCustomAttributes(typeof(ServiceKeyAuthAttribute), true)
                .SingleOrDefault());
    }

    [Fact]
    public void RouteTemplate_UsesCatchAllStorageRef()
    {
        var getRoute = typeof(InternalFileController)
            .GetMethod(nameof(InternalFileController.GetUrl))!
            .GetCustomAttributes(typeof(HttpMethodAttribute), true)
            .Cast<HttpMethodAttribute>()
            .Single();
        var deleteRoute = typeof(InternalFileController)
            .GetMethod(nameof(InternalFileController.Delete))!
            .GetCustomAttributes(typeof(HttpMethodAttribute), true)
            .Cast<HttpMethodAttribute>()
            .Single();

        Assert.Contains("**storageRef", getRoute.Template);
        Assert.Contains("**storageRef", deleteRoute.Template);
    }

    private static InternalFileController CreateController(IFileStorageService service)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:PublicBaseUrl"] = "https://files.example"
            })
            .Build();

        return new InternalFileController(
            service,
            configuration,
            NullLogger<InternalFileController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public FileUploadResultDto UploadResult { get; set; } = new();
        public FileUrlDto UrlResult { get; set; } = new();
        public FileDeleteResultDto DeleteResult { get; set; } = new();
        public string? BaseUrl { get; private set; }

        public Task<FileUploadResultDto> SaveAsync(
            IFormFile file,
            CancellationToken cancellationToken)
            => Task.FromResult(UploadResult);

        public Task<FileUrlDto> GetUrlAsync(
            string storageRef,
            string baseUrl,
            CancellationToken cancellationToken)
        {
            BaseUrl = baseUrl;
            return Task.FromResult(UrlResult);
        }

        public Task<FileDeleteResultDto> DeleteAsync(
            string storageRef,
            CancellationToken cancellationToken)
            => Task.FromResult(DeleteResult);
    }
}
