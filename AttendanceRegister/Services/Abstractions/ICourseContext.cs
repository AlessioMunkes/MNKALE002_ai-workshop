using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Services.Abstractions;

/// <summary>
/// Resolves the course the application is running for. Isolated behind an
/// interface so a future multi-course version only replaces this one class.
/// </summary>
public interface ICourseContext
{
    Task<Course> GetAsync(CancellationToken cancellationToken = default);
}
