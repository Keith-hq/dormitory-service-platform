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

    public async Task<AccommodationDto> GetCurrentAccommodationAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        var accommodation = await DbContext.BedAllocations
            .AsNoTracking()
            .Where(item =>
                item.StudentId == studentId &&
                item.CheckOutDate == null &&
                item.RoomId != null)
            .OrderByDescending(item => item.CheckInDate)
            .ThenByDescending(item => item.AllocationId)
            .Select(item => new AccommodationDto
            {
                AllocationId = item.AllocationId,
                StudentId = item.StudentId!,
                RoomId = item.RoomId!.Value,
                BedNo = item.BedNo,
                CheckInDate = item.CheckInDate,
                CheckOutDate = item.CheckOutDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        return accommodation ?? throw new BusinessException(
            404,
            "未找到当前住宿信息",
            StatusCodes.Status404NotFound);
    }

    public async Task<IReadOnlyList<AccommodationDto>> GetAccommodationHistoryAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        return await DbContext.BedAllocations
            .AsNoTracking()
            .Where(item =>
                item.StudentId == studentId &&
                item.CheckOutDate != null &&
                item.RoomId != null)
            .OrderByDescending(item => item.CheckOutDate)
            .ThenByDescending(item => item.CheckInDate)
            .Select(item => new AccommodationDto
            {
                AllocationId = item.AllocationId,
                StudentId = item.StudentId!,
                RoomId = item.RoomId!.Value,
                BedNo = item.BedNo,
                CheckInDate = item.CheckInDate,
                CheckOutDate = item.CheckOutDate
            })
            .ToListAsync(cancellationToken);
    }
}
