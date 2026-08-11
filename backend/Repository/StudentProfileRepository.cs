using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class StudentProfileRepository : FrameworkRepositoryBase
{
    public StudentProfileRepository(AppDbContext context) : base(context) { }

    public Task<StudentProfileDto> UpdateProfileAsync(
        string studentId,
        UpdateStudentProfileRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<StudentProfileDto>(
            "STU-01",
            "D_STUDENT 尚无 EMAIL 字段，待确认资料扩展方案",
            cancellationToken);

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
