using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IStudentProfileService
{
    Task<StudentProfileDto> UpdateProfileAsync(string studentId, UpdateStudentProfileRequest request, CancellationToken cancellationToken);
    Task<AccommodationDto> GetCurrentAccommodationAsync(string studentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccommodationDto>> GetAccommodationHistoryAsync(string studentId, CancellationToken cancellationToken);
}

public sealed class StudentProfileService : IStudentProfileService
{
    private readonly StudentProfileRepository _repository;

    public StudentProfileService(StudentProfileRepository repository)
    {
        _repository = repository;
    }

    public Task<StudentProfileDto> UpdateProfileAsync(
        string studentId,
        UpdateStudentProfileRequest request,
        CancellationToken cancellationToken)
        => _repository.UpdateProfileAsync(studentId, request, cancellationToken);

    public Task<AccommodationDto> GetCurrentAccommodationAsync(
        string studentId,
        CancellationToken cancellationToken)
        => _repository.GetCurrentAccommodationAsync(studentId, cancellationToken);

    public Task<IReadOnlyList<AccommodationDto>> GetAccommodationHistoryAsync(
        string studentId,
        CancellationToken cancellationToken)
        => _repository.GetAccommodationHistoryAsync(studentId, cancellationToken);
}
