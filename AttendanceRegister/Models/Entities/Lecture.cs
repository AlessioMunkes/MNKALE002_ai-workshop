namespace AttendanceRegister.Models.Entities;

/// <summary>
/// One timetabled session. Owns the rules for its own self check-in window,
/// so no page or service has to reimplement them.
/// </summary>
public sealed class Lecture
{
    public int Id { get; private set; }
    public int CourseId { get; private set; }
    public Course? Course { get; private set; }

    public DateOnly SessionDate { get; private set; }
    public TimeOnly StartTime { get; private set; } = new(8, 0);
    public TimeOnly EndTime { get; private set; } = new(8, 45);
    public string Topic { get; private set; } = string.Empty;
    public string Venue { get; private set; } = string.Empty;

    /// <summary>Code students type in to record their own attendance. Null when closed.</summary>
    public string? CheckInCode { get; private set; }
    public DateTime? CheckInClosesAtUtc { get; private set; }

    public ICollection<AttendanceRecord> AttendanceRecords { get; private set; } = new List<AttendanceRecord>();

    private Lecture() { }

    public Lecture(int courseId, DateOnly sessionDate, string topic = "", string venue = "")
    {
        CourseId = courseId;
        SessionDate = sessionDate;
        Topic = topic.Trim();
        Venue = venue.Trim();
    }

    public bool IsCheckInOpen =>
        CheckInCode is not null &&
        CheckInClosesAtUtc.HasValue &&
        CheckInClosesAtUtc.Value > DateTime.UtcNow;

    public void UpdateDetails(DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime, string topic, string venue)
    {
        SessionDate = sessionDate;
        StartTime = startTime;
        EndTime = endTime;
        Topic = topic.Trim();
        Venue = venue.Trim();
    }

    /// <summary>Opens a time-boxed check-in window and returns the generated code.</summary>
    public string OpenCheckIn(TimeSpan duration, Func<string> codeFactory)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The check-in window must be longer than zero minutes.");
        }

        CheckInCode = codeFactory();
        CheckInClosesAtUtc = DateTime.UtcNow.Add(duration);
        return CheckInCode;
    }

    public void CloseCheckIn()
    {
        CheckInCode = null;
        CheckInClosesAtUtc = null;
    }

    public bool CodeMatches(string candidate) =>
        CheckInCode is not null &&
        string.Equals(CheckInCode, candidate.Trim(), StringComparison.OrdinalIgnoreCase);
}
