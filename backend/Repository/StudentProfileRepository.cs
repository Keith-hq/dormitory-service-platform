using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;

namespace TemplateDormApi.Repository;

public sealed class StudentProfileRepository : FrameworkRepositoryBase
{
    public StudentProfileRepository(AppDbContext context) : base(context) { }

    public async Task<StudentProfileDto> UpdateProfileAsync(
        string studentId,
        UpdateStudentProfileRequest request,
        CancellationToken cancellationToken)
    {
        var student = await DbContext.Students.SingleOrDefaultAsync(
            item => item.StudentId == studentId,
            cancellationToken);
        if (student is null)
        {
            throw new BusinessException(404, "学生不存在", StatusCodes.Status404NotFound);
        }

        student.Phone = request.Phone;
        student.Email = request.Email;
        await DbContext.SaveChangesAsync(cancellationToken);

        return new StudentProfileDto
        {
            StudentId = student.StudentId,
            Phone = student.Phone,
            Email = student.Email
        };
    }

    public Task<AccommodationDto> GetCurrentAccommodationAsync(
        string studentId,
        CancellationToken cancellationToken)
        => PendingAsync<AccommodationDto>(
            "STU-02",
            "住宿数据由住宿模块统一维护，待读取边界确认",
            cancellationToken);

    public Task<IReadOnlyList<AccommodationDto>> GetAccommodationHistoryAsync(
        string studentId,
        CancellationToken cancellationToken)
        => PendingAsync<IReadOnlyList<AccommodationDto>>(
            "STU-03",
            "住宿历史读取口径待住宿模块确认",
            cancellationToken);
}
