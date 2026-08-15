using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using TemplateDormApi.Data;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

public class ImportService : IImportService
{
    private readonly AppDbContext _context;

    public ImportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(int ImportedCount, List<ImportError> Errors, List<int> SkippedRows)> ImportStudentsAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var errors = new List<ImportError>();
        var skippedRows = new List<int>();
        var studentsToAdd = new List<Student>();
        var accountsToAdd = new List<UserAccount>();

        // 1. 读取已有学生ID和账号关联的学生ID
        var existingStudentIds = await _context.Students
            .Select(s => s.StudentId)
            .ToListAsync(cancellationToken);
        var existingAccountStudentIds = await _context.UserAccounts
            .Select(u => u.StudentId)
            .Where(id => id != null)
            .ToListAsync(cancellationToken);
        var allExistingStudentIds = existingStudentIds
            .Concat(existingAccountStudentIds.Select(id => id!))
            .ToHashSet();

        // 2. 解析 Excel
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets[0];
        if (worksheet == null)
            throw new BusinessException(400, "Excel 文件格式不正确");

        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var studentId = worksheet.Cells[row, 1]?.Text?.Trim();
            var name = worksheet.Cells[row, 2]?.Text?.Trim();
            var gender = worksheet.Cells[row, 3]?.Text?.Trim();
            var majorName = worksheet.Cells[row, 4]?.Text?.Trim();
            var phone = worksheet.Cells[row, 5]?.Text?.Trim();
            var email = worksheet.Cells[row, 6]?.Text?.Trim();

            var rowErrors = new List<string>();

            if (string.IsNullOrEmpty(studentId))
                rowErrors.Add("学号不能为空");
            if (string.IsNullOrEmpty(name))
                rowErrors.Add("姓名不能为空");

            if (rowErrors.Any())
            {
                errors.Add(new ImportError { Row = row, Messages = rowErrors });
                continue;
            }

            // 检查学号重复（数据库中）
            if (allExistingStudentIds.Contains(studentId!) || studentsToAdd.Any(s => s.StudentId == studentId))
            {
                skippedRows.Add(row);
                continue;
            }

            // 专业解析
            int? majorId = null;
            if (!string.IsNullOrEmpty(majorName))
            {
                var major = await _context.Majors
                    .FirstOrDefaultAsync(m => m.MajorName == majorName, cancellationToken);
                if (major == null)
                {
                    errors.Add(new ImportError { Row = row, Messages = new List<string> { $"专业 '{majorName}' 不存在" } });
                    continue;
                }
                majorId = major.MajorId;
            }

            // 构建学生
            var student = new Student
            {
                StudentId = studentId!,
                Name = name!,
                Gender = gender ?? string.Empty,
                MajorId = majorId,
                Phone = phone ?? string.Empty,
                Email = email ?? string.Empty
            };
            studentsToAdd.Add(student);

            // 自动创建账号（默认密码为学号）
            var account = new UserAccount
            {
                LoginName = studentId!,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(studentId!),
                AccountStatus = "正常",
                StudentId = studentId!
            };
            accountsToAdd.Add(account);
        }

        // 3. 错误处理（超过10条直接返回，不执行插入）
        if (errors.Count > 10)
        {
            return (0, errors, skippedRows);
        }
        if (errors.Any())
        {
            return (0, errors, skippedRows);
        }

        // 4. 批量插入
        await _context.Students.AddRangeAsync(studentsToAdd, cancellationToken);
        await _context.UserAccounts.AddRangeAsync(accountsToAdd, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return (studentsToAdd.Count, errors, skippedRows);
    }
}