using System.IO;
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

        // 2. 解析（CSV 或 Excel）。样例列序：学号,姓名,性别,学院,专业,手机号,邮箱（7 列，首行表头）。
        //    CSV 用简单解析；Excel（.xlsx）用 EPPlus；两路径统一读取第 0/1/2/4/5/6 列。
        var rows = await ParseRowsAsync(file, cancellationToken);

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex];
            var row = rowIndex + 2;  // 文件行号（1-based，含表头）
            var studentId = Cell(cells, 0);  // 学号
            var name = Cell(cells, 1);       // 姓名
            var gender = Cell(cells, 2);     // 性别
            var majorName = Cell(cells, 4);  // 专业（列5）
            var phone = Cell(cells, 5);      // 手机号（列6）
            var email = Cell(cells, 6);      // 邮箱（列7）

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

    /// <summary>读取表格行（跳过表头）。CSV 按逗号切分（去引号/BOM）；Excel 用 EPPlus。统一返回 7 列单元格。</summary>
    private static async Task<List<List<string>>> ParseRowsAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            var rows = new List<List<string>>();
            var headerSkipped = false;
            while (reader.ReadLine() is { } line)
            {
                if (!headerSkipped) { headerSkipped = true; continue; }  // 首行为表头
                if (string.IsNullOrWhiteSpace(line)) continue;
                rows.Add(line.Split(',').Select(c => c.Trim().Trim('"').Trim('﻿')).ToList());
            }
            return rows;
        }

        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets[0];
        if (worksheet == null)
            throw new BusinessException(400, "Excel 文件格式不正确");
        if (worksheet.Dimension is null)
            return new List<List<string>>();
        var excelRows = new List<List<string>>();
        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var cells = new List<string>();
            for (int col = 1; col <= 7; col++)
                cells.Add(worksheet.Cells[row, col]?.Text?.Trim() ?? string.Empty);
            excelRows.Add(cells);
        }
        return excelRows;
    }

    private static string Cell(IReadOnlyList<string> cells, int index) =>
        cells.Count > index ? cells[index] : string.Empty;
}