using AttendanceRegister.Data;

namespace AttendanceRegister.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AttendanceDbContext _db;

    public UnitOfWork(AttendanceDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
