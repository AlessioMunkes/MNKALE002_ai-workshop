using AttendanceRegister.Data;
using AttendanceRegister.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Repositories;

public interface IStudentRepository : IRepository<Student>
{
    Task<Student?> FindByStudentNumberAsync(string studentNumber, CancellationToken cancellationToken = default);

    /// <summary>Student number to id, in one round trip. Used by the bulk importer.</summary>
    Task<Dictionary<string, int>> GetIdsByStudentNumberAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

public sealed class StudentRepository : EfRepository<Student>, IStudentRepository
{
    public StudentRepository(AttendanceDbContext db) : base(db) { }

    public Task<Student?> FindByStudentNumberAsync(string studentNumber, CancellationToken cancellationToken = default)
    {
        var normalised = Student.NormaliseStudentNumber(studentNumber);
        return Set.FirstOrDefaultAsync(s => s.StudentNumber == normalised, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetIdsByStudentNumberAsync(CancellationToken cancellationToken = default)
    {
        var pairs = await Set.AsNoTracking()
            .Select(s => new { s.StudentNumber, s.Id })
            .ToListAsync(cancellationToken);

        return pairs.ToDictionary(p => p.StudentNumber, p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Set.CountAsync(cancellationToken);
}

public interface ILectureRepository : IRepository<Lecture>
{
    Task<List<Lecture>> GetForCourseAsync(int courseId, CancellationToken cancellationToken = default);
    Task<Dictionary<DateOnly, int>> GetIdsByDateAsync(int courseId, CancellationToken cancellationToken = default);
    Task<Lecture?> GetOpenCheckInAsync(int courseId, CancellationToken cancellationToken = default);
}

public sealed class LectureRepository : EfRepository<Lecture>, ILectureRepository
{
    public LectureRepository(AttendanceDbContext db) : base(db) { }

    public Task<List<Lecture>> GetForCourseAsync(int courseId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
           .Where(l => l.CourseId == courseId)
           .OrderBy(l => l.SessionDate)
           .ToListAsync(cancellationToken);

    public async Task<Dictionary<DateOnly, int>> GetIdsByDateAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var pairs = await Set.AsNoTracking()
            .Where(l => l.CourseId == courseId)
            .Select(l => new { l.SessionDate, l.Id })
            .ToListAsync(cancellationToken);

        return pairs.ToDictionary(p => p.SessionDate, p => p.Id);
    }

    public async Task<Lecture?> GetOpenCheckInAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await Set.Where(l => l.CourseId == courseId
                                 && l.CheckInSecret != null
                                 && l.CheckInClosesAtUtc != null
                                 && l.CheckInClosesAtUtc > now)
                        .OrderByDescending(l => l.SessionDate)
                        .FirstOrDefaultAsync(cancellationToken);
    }
}

public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    Task<AttendanceRecord?> FindAsync(int lectureId, int studentId, CancellationToken cancellationToken = default);
    Task<List<AttendanceRecord>> GetForLectureAsync(int lectureId, CancellationToken cancellationToken = default);
    Task<List<AttendanceRecord>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Every record for the given lectures, keyed for O(1) upsert lookups.</summary>
    Task<Dictionary<(int LectureId, int StudentId), AttendanceRecord>> GetKeyedAsync(
        IReadOnlyCollection<int> lectureIds, CancellationToken cancellationToken = default);

    /// <summary>Ids of lectures that have at least one captured record.</summary>
    Task<List<int>> GetCapturedLectureIdsAsync(CancellationToken cancellationToken = default);
}

public sealed class AttendanceRepository : EfRepository<AttendanceRecord>, IAttendanceRepository
{
    public AttendanceRepository(AttendanceDbContext db) : base(db) { }

    public Task<AttendanceRecord?> FindAsync(int lectureId, int studentId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(r => r.LectureId == lectureId && r.StudentId == studentId, cancellationToken);

    public Task<List<AttendanceRecord>> GetForLectureAsync(int lectureId, CancellationToken cancellationToken = default) =>
        Set.Where(r => r.LectureId == lectureId).ToListAsync(cancellationToken);

    public Task<List<AttendanceRecord>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().Where(r => r.StudentId == studentId).ToListAsync(cancellationToken);

    public async Task<Dictionary<(int LectureId, int StudentId), AttendanceRecord>> GetKeyedAsync(
        IReadOnlyCollection<int> lectureIds, CancellationToken cancellationToken = default)
    {
        if (lectureIds.Count == 0)
        {
            return new Dictionary<(int, int), AttendanceRecord>();
        }

        var records = await Set.Where(r => lectureIds.Contains(r.LectureId)).ToListAsync(cancellationToken);
        return records.ToDictionary(r => (r.LectureId, r.StudentId));
    }

    public Task<List<int>> GetCapturedLectureIdsAsync(CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().Select(r => r.LectureId).Distinct().ToListAsync(cancellationToken);
}
