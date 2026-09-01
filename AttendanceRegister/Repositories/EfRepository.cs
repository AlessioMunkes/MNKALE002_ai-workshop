using AttendanceRegister.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Repositories;

public class EfRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly AttendanceDbContext Db;
    protected readonly DbSet<TEntity> Set;

    public EfRepository(AttendanceDbContext db)
    {
        Db = db;
        Set = db.Set<TEntity>();
    }

    public IQueryable<TEntity> Query() => Set;

    public IQueryable<TEntity> QueryReadOnly() => Set.AsNoTracking();

    public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FindAsync(new object?[] { id }, cancellationToken).AsTask();

    public void Add(TEntity entity) => Set.Add(entity);

    public void AddRange(IEnumerable<TEntity> entities) => Set.AddRange(entities);

    public void Remove(TEntity entity) => Set.Remove(entity);
}
