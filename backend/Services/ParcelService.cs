using Microsoft.AspNetCore.Http;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 快递业务逻辑接口
/// </summary>
public interface IParcelService
{
    Task<PagedResult<ParcelRecord>> GetMyParcelsAsync(string studentId, int page, int pageSize);
    Task<ParcelRecord> PickupAsync(int parcelId, string currentStudentId);
}

/// <summary>
/// 快递业务逻辑实现
/// </summary>
public class ParcelService : IParcelService
{
    private readonly ParcelRepository _repository;

    public ParcelService(ParcelRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ParcelRecord>> GetMyParcelsAsync(
        string studentId, int page, int pageSize)
    {
        var (items, total) = await _repository.GetPagedByStudentAsync(studentId, page, pageSize);
        return new PagedResult<ParcelRecord>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ParcelRecord> PickupAsync(int parcelId, string currentStudentId)
    {
        var parcel = await _repository.GetByIdAsync(parcelId)
            ?? throw new BusinessException(404, "快递记录不存在", StatusCodes.Status404NotFound);

        if (parcel.StudentId != currentStudentId)
            throw new BusinessException(403, "无权操作他人的快递", StatusCodes.Status403Forbidden);

        if (parcel.PickupTime.HasValue)
            throw new BusinessException(400, "该快递已取件");

        parcel.PickupTime = DateTime.Now;
        return await _repository.UpdateAsync(parcel);
    }
}
