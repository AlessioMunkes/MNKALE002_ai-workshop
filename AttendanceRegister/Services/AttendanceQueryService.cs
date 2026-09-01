using AttendanceRegister.Data;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Repositories;
using AttendanceRegister.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Services;

public sealed class AttendanceQueryService : IAttendanceQueryService
{
    private const int MinimumReasonLength = 10;

    private readonly AttendanceDbContext _db;
    private readonly IAttendanceService _attendanceService;
    private readonly IUnitOfWork _unitOfWork;

    public AttendanceQueryService(AttendanceDbContext db, IAttendanceService attendanceService, IUnitOfWork unitOfWork)
    {
        _db = db;
        _attendanceService = attendanceService;
        _unitOfWork = unitOfWork;
    }

    // ----------------------------------------------------------------- read --

    public Task<AttendanceQuery?> GetAsync(int queryId, CancellationToken cancellationToken = default) =>
        _db.AttendanceQueries
            .Include(q => q.Lecture)
            .Include(q => q.Student)
            .FirstOrDefaultAsync(q => q.Id == queryId, cancellationToken);

    public Task<List<AttendanceQuery>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default) =>
        _db.AttendanceQueries.AsNoTracking()
            .Include(q => q.Lecture)
            .Where(q => q.StudentId == studentId)
            .OrderByDescending(q => q.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<List<AttendanceQuery>> GetForReviewAsync(QueryStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AttendanceQueries.AsNoTracking()
            .Include(q => q.Lecture)
            .Include(q => q.Student)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(q => q.Status == status.Value);
        }

        return query.OrderBy(q => q.Status)
                    .ThenByDescending(q => q.SubmittedAtUtc)
                    .ToListAsync(cancellationToken);
    }

    public Task<int> CountOpenAsync(CancellationToken cancellationToken = default) =>
        _db.AttendanceQueries.CountAsync(q => q.Status == QueryStatus.Open, cancellationToken);

    // --------------------------------------------------------------- create --

    public Task<QuerySubmissionOutcome> SubmitAsync(int studentId, int lectureId,
        AttendanceStatus requestedStatus, string reason, CancellationToken cancellationToken = default) =>
        CreateAsync(studentId, lectureId, requestedStatus, reason, null, cancellationToken);

    public Task<QuerySubmissionOutcome> RaiseOnBehalfAsync(int studentId, int lectureId,
        AttendanceStatus requestedStatus, string reason, int lecturerUserId,
        CancellationToken cancellationToken = default) =>
        CreateAsync(studentId, lectureId, requestedStatus, reason, lecturerUserId, cancellationToken);

    private async Task<QuerySubmissionOutcome> CreateAsync(int studentId, int lectureId,
        AttendanceStatus requestedStatus, string reason, int? raisedByUserId,
        CancellationToken cancellationToken)
    {
        var problem = ValidateReason(reason);
        if (problem is not null)
        {
            return new QuerySubmissionOutcome(false, problem);
        }

        if (!await _db.Students.AnyAsync(s => s.Id == studentId, cancellationToken))
        {
            return new QuerySubmissionOutcome(false, "Choose a student.");
        }

        var lecture = await _db.Lectures.FirstOrDefaultAsync(l => l.Id == lectureId, cancellationToken);
        if (lecture is null)
        {
            return new QuerySubmissionOutcome(false, "Choose a session from the list.");
        }

        var duplicate = await _db.AttendanceQueries
            .AnyAsync(q => q.StudentId == studentId && q.LectureId == lectureId && q.Status == QueryStatus.Open,
                cancellationToken);
        if (duplicate)
        {
            return new QuerySubmissionOutcome(false,
                $"There is already an open query for {lecture.SessionDate:d MMMM yyyy}. Resolve that one before adding another.");
        }

        var query = new AttendanceQuery(studentId, lectureId, requestedStatus, reason, raisedByUserId);
        _db.AttendanceQueries.Add(query);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuerySubmissionOutcome(true,
            $"Query recorded for {lecture.SessionDate:d MMMM yyyy}.", query.Id);
    }

    // --------------------------------------------------------------- update --

    public async Task<QuerySubmissionOutcome> AmendAsync(int queryId, AttendanceStatus requestedStatus,
        string reason, CancellationToken cancellationToken = default)
    {
        var problem = ValidateReason(reason);
        if (problem is not null)
        {
            return new QuerySubmissionOutcome(false, problem);
        }

        var query = await _db.AttendanceQueries.FirstOrDefaultAsync(q => q.Id == queryId, cancellationToken);
        if (query is null)
        {
            return new QuerySubmissionOutcome(false, "That query no longer exists.");
        }

        if (query.RequestedStatus == requestedStatus && query.Reason == reason.Trim())
        {
            return new QuerySubmissionOutcome(true, "Nothing changed, so nothing was saved.", query.Id);
        }

        query.Amend(requestedStatus, reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuerySubmissionOutcome(true, "Query updated.", query.Id);
    }

    public async Task<QuerySubmissionOutcome> ReopenAsync(int queryId, CancellationToken cancellationToken = default)
    {
        var query = await _db.AttendanceQueries.FirstOrDefaultAsync(q => q.Id == queryId, cancellationToken);
        if (query is null)
        {
            return new QuerySubmissionOutcome(false, "That query no longer exists.");
        }

        if (query.IsOpen)
        {
            return new QuerySubmissionOutcome(false, "That query is already open.");
        }

        query.Reopen();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuerySubmissionOutcome(true,
            "Query reopened. The register was left as it is — change it on the register screen if the outcome should be undone.",
            query.Id);
    }

    public async Task<QuerySubmissionOutcome> ResolveAsync(int queryId, bool approve, string? note,
        int lecturerUserId, CancellationToken cancellationToken = default)
    {
        var query = await _db.AttendanceQueries.FirstOrDefaultAsync(q => q.Id == queryId, cancellationToken);
        if (query is null)
        {
            return new QuerySubmissionOutcome(false, "That query no longer exists.");
        }

        if (!query.IsOpen)
        {
            return new QuerySubmissionOutcome(false, "That query has already been resolved. Reopen it first.");
        }

        if (approve)
        {
            query.Approve(lecturerUserId, note);
            await _attendanceService.SetStatusAsync(query.LectureId, query.StudentId, query.RequestedStatus,
                AttendanceSource.QueryResolution, lecturerUserId,
                string.IsNullOrWhiteSpace(note) ? "Approved attendance query" : note, cancellationToken);
        }
        else
        {
            query.Reject(lecturerUserId, note);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuerySubmissionOutcome(true,
            approve ? "Query approved and the register updated." : "Query rejected. The register was left unchanged.",
            query.Id);
    }

    // --------------------------------------------------------------- delete --

    public async Task<QuerySubmissionOutcome> DeleteAsync(int queryId, CancellationToken cancellationToken = default)
    {
        var query = await _db.AttendanceQueries.FirstOrDefaultAsync(q => q.Id == queryId, cancellationToken);
        if (query is null)
        {
            return new QuerySubmissionOutcome(false, "That query no longer exists.");
        }

        // Deleting the query never rewrites the register. An approved query has
        // already changed the record, and that change stands on its own with its
        // own audit trail.
        _db.AttendanceQueries.Remove(query);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuerySubmissionOutcome(true, "Query deleted. Any register change it caused was left in place.");
    }

    private static string? ValidateReason(string reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinimumReasonLength
            ? $"Give at least {MinimumReasonLength} characters of explanation so the query can be acted on."
            : null;
}
