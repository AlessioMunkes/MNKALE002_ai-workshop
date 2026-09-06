namespace AttendanceRegister.Services.Abstractions;

public sealed record SessionAdminOutcome(bool Success, string Message);

/// <summary>
/// What a lecturer can do to a session record itself, as opposed to the
/// attendance captured against it. Separate from IAttendanceService for the
/// same reason IStudentAdminService is separate from IAccountService: one is
/// about running the course, the other about maintaining its records.
/// </summary>
public interface ISessionAdminService
{
    /// <summary>What deleting this session would take with it.</summary>
    Task<SessionDeletionPreview?> PreviewDeleteAsync(int lectureId, CancellationToken cancellationToken = default);

    Task<SessionAdminOutcome> DeleteAsync(int lectureId, CancellationToken cancellationToken = default);
}

public sealed record SessionDeletionPreview(
    int LectureId,
    DateOnly SessionDate,
    string Topic,
    int AttendanceRecords,
    int CountedPresent,
    int Queries,
    bool CheckInOpen);
