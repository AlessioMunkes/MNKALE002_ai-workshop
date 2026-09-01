namespace AttendanceRegister.Repositories;

/// <summary>One transactional boundary shared by every repository on a request.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
