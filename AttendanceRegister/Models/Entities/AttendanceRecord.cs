namespace AttendanceRegister.Models.Entities;

/// <summary>
/// One student's state at one lecture. A unique index on
/// (LectureId, StudentId) guarantees a single row per pairing.
/// </summary>
public sealed class AttendanceRecord
{
    public int Id { get; private set; }

    public int LectureId { get; private set; }
    public Lecture? Lecture { get; private set; }

    public int StudentId { get; private set; }
    public Student? Student { get; private set; }

    public AttendanceStatus Status { get; private set; }
    public AttendanceSource Source { get; private set; }
    public DateTime RecordedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>The account that last wrote this row. Null for automated imports.</summary>
    public int? RecordedByUserId { get; private set; }

    public string? Note { get; private set; }

    private AttendanceRecord() { }

    public AttendanceRecord(int lectureId, int studentId, AttendanceStatus status, AttendanceSource source,
        int? recordedByUserId = null, string? note = null)
    {
        LectureId = lectureId;
        StudentId = studentId;
        Status = status;
        Source = source;
        RecordedByUserId = recordedByUserId;
        Note = Trim(note);
    }

    public bool IsCounted => Status.Counts();

    /// <summary>Rewrites the row and returns true only when something actually changed.</summary>
    public bool Revise(AttendanceStatus status, AttendanceSource source, int? recordedByUserId, string? note = null)
    {
        var trimmedNote = Trim(note);
        if (Status == status && Note == trimmedNote)
        {
            return false;
        }

        Status = status;
        Source = source;
        RecordedByUserId = recordedByUserId;
        Note = trimmedNote;
        RecordedAtUtc = DateTime.UtcNow;
        return true;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
