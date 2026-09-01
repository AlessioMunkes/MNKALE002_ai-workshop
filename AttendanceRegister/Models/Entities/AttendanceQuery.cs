namespace AttendanceRegister.Models.Entities;

/// <summary>A student's dispute about one attendance record, and its outcome.</summary>
public sealed class AttendanceQuery
{
    public int Id { get; private set; }

    public int StudentId { get; private set; }
    public Student? Student { get; private set; }

    public int LectureId { get; private set; }
    public Lecture? Lecture { get; private set; }

    public AttendanceStatus RequestedStatus { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; private set; } = DateTime.UtcNow;

    public QueryStatus Status { get; private set; } = QueryStatus.Open;
    public string? ResolutionNote { get; private set; }
    public int? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Set when a lecturer captured the query on the student's behalf.</summary>
    public int? RaisedByUserId { get; private set; }

    public DateTime? AmendedAtUtc { get; private set; }

    private AttendanceQuery() { }

    public AttendanceQuery(int studentId, int lectureId, AttendanceStatus requestedStatus, string reason,
        int? raisedByUserId = null)
    {
        StudentId = studentId;
        LectureId = lectureId;
        RequestedStatus = requestedStatus;
        Reason = reason.Trim();
        RaisedByUserId = raisedByUserId;
    }

    public bool IsOpen => Status == QueryStatus.Open;
    public bool WasRaisedByLecturer => RaisedByUserId.HasValue;
    public bool WasAmended => AmendedAtUtc.HasValue;

    /// <summary>Corrects what the query says. Allowed whatever state it is in.</summary>
    public void Amend(AttendanceStatus requestedStatus, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A query needs a reason.", nameof(reason));
        }

        RequestedStatus = requestedStatus;
        Reason = reason.Trim();
        AmendedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Puts a resolved query back in the queue. The register is deliberately
    /// left as it is: undoing an approval is a separate decision, made on the
    /// register screen where the change is visible.
    /// </summary>
    public void Reopen()
    {
        if (IsOpen)
        {
            return;
        }

        Status = QueryStatus.Open;
        ResolutionNote = null;
        ResolvedByUserId = null;
        ResolvedAtUtc = null;
        AmendedAtUtc = DateTime.UtcNow;
    }

    public void Approve(int lecturerUserId, string? note)
    {
        EnsureOpen();
        Status = QueryStatus.Approved;
        Close(lecturerUserId, note);
    }

    public void Reject(int lecturerUserId, string? note)
    {
        EnsureOpen();
        Status = QueryStatus.Rejected;
        Close(lecturerUserId, note);
    }

    private void Close(int lecturerUserId, string? note)
    {
        ResolvedByUserId = lecturerUserId;
        ResolvedAtUtc = DateTime.UtcNow;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException(
                "This query has already been resolved. Reopen it first if the outcome needs to change.");
        }
    }
}
