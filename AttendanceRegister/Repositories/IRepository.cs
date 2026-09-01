namespace AttendanceRegister.Repositories;

/// <summary>
/// Generic persistence contract. Services depend on this rather than on
/// DbContext, which keeps them testable and swaps the provider out cheaply.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    IQueryable<TEntity> QueryReadOnly();
    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void Remove(TEntity entity);
}
