using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Services.Abstractions;

public sealed record QuerySubmissionOutcome(bool Success, string Message, int? QueryId = null);

public interface IAttendanceQueryService
{
    // ---- read ---------------------------------------------------------------
    Task<AttendanceQuery?> GetAsync(int queryId, CancellationToken cancellationToken = default);
    Task<List<AttendanceQuery>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default);
    Task<List<AttendanceQuery>> GetForReviewAsync(QueryStatus? status = null, CancellationToken cancellationToken = default);
    Task<int> CountOpenAsync(CancellationToken cancellationToken = default);

    // ---- create -------------------------------------------------------------
    Task<QuerySubmissionOutcome> SubmitAsync(int studentId, int lectureId, AttendanceStatus requestedStatus,
        string reason, CancellationToken cancellationToken = default);

    /// <summary>Captures a query a student raised by email or in person.</summary>
    Task<QuerySubmissionOutcome> RaiseOnBehalfAsync(int studentId, int lectureId, AttendanceStatus requestedStatus,
        string reason, int lecturerUserId, CancellationToken cancellationToken = default);

    // ---- update -------------------------------------------------------------
    Task<QuerySubmissionOutcome> AmendAsync(int queryId, AttendanceStatus requestedStatus, string reason,
        CancellationToken cancellationToken = default);

    Task<QuerySubmissionOutcome> ReopenAsync(int queryId, CancellationToken cancellationToken = default);

    Task<QuerySubmissionOutcome> ResolveAsync(int queryId, bool approve, string? note, int lecturerUserId,
        CancellationToken cancellationToken = default);

    // ---- delete -------------------------------------------------------------
    Task<QuerySubmissionOutcome> DeleteAsync(int queryId, CancellationToken cancellationToken = default);
}
