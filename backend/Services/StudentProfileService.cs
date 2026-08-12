using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IStudentProfileService
{
    Task<StudentProfileDto> UpdateProfileAsync(string studentId, int accountId, UpdateStudentProfileRequest request, CancellationToken cancellationToken);
    Task<AccommodationDto> GetCurrentAccommodationAsync(string studentId, int accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccommodationDto>> GetAccommodationHistoryAsync(string studentId, int accountId, CancellationToken cancellationToken);
}

public sealed class StudentProfileService : IStudentProfileService
{
    private readonly StudentProfileRepository _repository;
    private readonly IStudentIdentityService _identityService;

    public StudentProfileService(
        StudentProfileRepository repository,
        IStudentIdentityService identityService)
    {
        _repository = repository;
        _identityService = identityService;
    }

    public async Task<StudentProfileDto> UpdateProfileAsync(
        string studentId,
        int accountId,
        UpdateStudentProfileRequest request,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.UpdateProfileAsync(studentId, request, cancellationToken);
    }

    public async Task<AccommodationDto> GetCurrentAccommodationAsync(
        string studentId,
        int accountId,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetCurrentAccommodationAsync(studentId, cancellationToken);
    }

    public async Task<IReadOnlyList<AccommodationDto>> GetAccommodationHistoryAsync(
        string studentId,
        int accountId,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetAccommodationHistoryAsync(studentId, cancellationToken);
    }
}
