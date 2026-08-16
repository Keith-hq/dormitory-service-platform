using Microsoft.AspNetCore.Http;

namespace TemplateDormApi.Services;

public interface IImportService
{
    Task<(int ImportedCount, List<ImportError> Errors, List<int> SkippedRows)> ImportStudentsAsync(
        IFormFile file,
        CancellationToken cancellationToken);
}

public class ImportError
{
    public int Row { get; set; }
    public List<string> Messages { get; set; } = new();
}