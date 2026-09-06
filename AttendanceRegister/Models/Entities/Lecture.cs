namespace AttendanceRegister.Models.Entities;

/// <summary>
/// One timetabled session. Owns the rules for its own self check-in window.
///
/// Since codes rotate, this holds the <em>secret</em> the codes are derived
/// from, not a code. It is stored in the column that used to hold the literal
/// code, so an existing database needs no migration — the meaning of the column
/// changed, its shape did not.
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

    /// <summary>
    /// Seed for the rotating code. Never shown to anyone: the projector shows a
    /// code derived from this and the clock, which is a different string every
    /// thirty seconds.
    /// </summary>
    public string? CheckInSecret { get; private set; }

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
        CheckInSecret is not null &&
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

    /// <summary>Opens a time-boxed check-in window seeded with the given secret.</summary>
    public void OpenCheckIn(TimeSpan duration, string secret)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The check-in window must be longer than zero minutes.");
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("A check-in window needs a secret to derive codes from.", nameof(secret));
        }

        CheckInSecret = secret;
        CheckInClosesAtUtc = DateTime.UtcNow.Add(duration);
    }

    public void CloseCheckIn()
    {
        CheckInSecret = null;
        CheckInClosesAtUtc = null;
    }
}
