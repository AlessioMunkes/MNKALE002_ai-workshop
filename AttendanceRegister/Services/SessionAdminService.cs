using AttendanceRegister.Data;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Repositories;
using AttendanceRegister.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Services;

public sealed class SessionAdminService : ISessionAdminService
{
    private readonly AttendanceDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SessionAdminService> _logger;

    public SessionAdminService(AttendanceDbContext db, IUnitOfWork unitOfWork, ILogger<SessionAdminService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SessionDeletionPreview?> PreviewDeleteAsync(int lectureId,
        CancellationToken cancellationToken = default)
    {
        var lecture = await _db.Lectures.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lectureId, cancellationToken);

        if (lecture is null)
        {
            return null;
        }

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.LectureId == lectureId)
            .Select(r => r.Status)
            .ToListAsync(cancellationToken);

        var queries = await _db.AttendanceQueries.AsNoTracking()
            .CountAsync(q => q.LectureId == lectureId, cancellationToken);

        return new SessionDeletionPreview(
            lecture.Id,
            lecture.SessionDate,
            lecture.Topic,
            records.Count,
            records.Count(s => s.Counts()),
            queries,
            lecture.IsCheckInOpen);
    }

    public async Task<SessionAdminOutcome> DeleteAsync(int lectureId,
        CancellationToken cancellationToken = default)
    {
        var lecture = await _db.Lectures.FirstOrDefaultAsync(l => l.Id == lectureId, cancellationToken);
        if (lecture is null)
        {
            return new SessionAdminOutcome(false, "That session no longer exists.");
        }

        // The dependents are removed explicitly rather than left to the database
        // cascade. The cascade would do it, but only while SQLite's foreign key
        // enforcement is on — which is a connection setting, not a property of
        // the schema. Doing it here means the behaviour cannot quietly change
        // underneath the application.
        var records = await _db.AttendanceRecords
            .Where(r => r.LectureId == lectureId)
            .ToListAsync(cancellationToken);

        var queries = await _db.AttendanceQueries
            .Where(q => q.LectureId == lectureId)
            .ToListAsync(cancellationToken);

        _db.AttendanceRecords.RemoveRange(records);
        _db.AttendanceQueries.RemoveRange(queries);
        _db.Lectures.Remove(lecture);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted session {Date} with {Records} attendance records and {Queries} queries.",
            lecture.SessionDate, records.Count, queries.Count);

        var carried = records.Count == 0
            ? "It had no attendance captured against it."
            : $"{records.Count} attendance {(records.Count == 1 ? "record" : "records")}" +
              (queries.Count > 0 ? $" and {queries.Count} {(queries.Count == 1 ? "query" : "queries")}" : "") +
              " went with it.";

        return new SessionAdminOutcome(true, $"Deleted the session on {lecture.SessionDate:d MMMM yyyy}. {carried}");
    }
}
