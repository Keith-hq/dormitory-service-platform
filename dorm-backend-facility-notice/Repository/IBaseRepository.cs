namespace DormBackendFacilityNotice.Repository;

/// <summary>
/// 通用 Repository 基类接口
/// </summary>
public interface IBaseRepository<T> where T : class
{
    Task<List<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
    Task<(List<T> Items, int Total)> GetPagedAsync(int page, int pageSize);
}
