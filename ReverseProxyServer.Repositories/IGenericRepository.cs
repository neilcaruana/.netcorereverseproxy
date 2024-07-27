
namespace ReverseProxyServer.Repositories
{
    public interface IGenericRepository<T> where T : new()
    {
        Task<T> InsertAsync(T entity);
        Task<T> UpsertAsync(T entity);
        Task<int> UpdateAsync(T entity);
        Task<int> DeleteAsync(T entity);
        Task<bool> AnyAsync();
        Task<long> CountAsync();
        Task<T?> GetByFieldValueAsync(string fieldName, object fieldValue);
        Task<T?> GetByPrimaryKeyAsync(object pkValue);
        Task<List<T>> GetListDataByFieldValueAsync(string fieldName = "", object? fieldValue = null);
    }
}
