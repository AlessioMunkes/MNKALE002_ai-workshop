using AttendanceRegister.Data;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Services;

public sealed class CourseContext : ICourseContext
{
    private readonly AttendanceDbContext _db;
    private readonly CourseOptions _options;
    private Course? _cached;

    public CourseContext(AttendanceDbContext db, IOptions<CourseOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<Course> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var code = _options.Code.ToUpperInvariant();
        _cached = await _db.Courses.FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
                  ?? await _db.Courses.FirstOrDefaultAsync(cancellationToken)
                  ?? throw new InvalidOperationException("No course has been configured. Restart the application so the seeder can create one.");

        return _cached;
    }
}
