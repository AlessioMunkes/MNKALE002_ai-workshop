using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Models.ViewModels;

/// <summary>One student's line on the session overview.</summary>
public sealed class SessionAttendee
{
    public int StudentId { get; init; }
    public string StudentNumber { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;

    public AttendanceStatus? Status { get; init; }
    public AttendanceSource? Source { get; init; }
    public DateTime? RecordedAtUtc { get; init; }
    public string? Note { get; init; }

    /// <summary>The student's rate across the whole course, for context.</summary>
    public double OverallPercent { get; init; }

    public bool WasCounted => Status.Counts();
    public string StatusLabel => Status.ToLabel();
    public string StatusModifier => Status.ToModifier();
    public string SourceLabel => Source.ToLabel();

    public DateTime? RecordedLocal => RecordedAtUtc?.ToLocalTime();
    public string RecordedSortKey => RecordedLocal?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
}

/// <summary>How one session's attendance was captured.</summary>
public sealed record CaptureBreakdown(AttendanceSource Source, int Count)
{
    public string Label => ((AttendanceSource?)Source).ToLabel();
}

/// <summary>
/// Everything about one lecture on one screen.
///
/// The register screen answers "let me change this". This one answers "what
/// happened here?" — which is a different question, asked at a different
/// moment, and mixing the two would make the editing screen slower to load and
/// harder to scan.
/// </summary>
public sealed class SessionOverview
{
    public int LectureId { get; init; }
    public DateOnly SessionDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string Venue { get; init; } = string.Empty;
    public bool IsCheckInOpen { get; init; }

    public int Enrolled { get; init; }
    public int MinimumPercent { get; init; } = 80;

    public List<SessionAttendee> Attendees { get; init; } = new();
    public List<CaptureBreakdown> Captured { get; init; } = new();
    public List<AttendanceQuery> Queries { get; init; } = new();

    /// <summary>Mean attendance across every captured session, for comparison.</summary>
    public double CourseAverage { get; init; }

    /// <summary>This session's position when sessions are ranked best first.</summary>
    public int Rank { get; init; }

    public int SessionsCaptured { get; init; }

    public int PresentCount => Attendees.Count(a => a.Status == AttendanceStatus.Present);
    public int LateCount => Attendees.Count(a => a.Status == AttendanceStatus.Late);
    public int ExcusedCount => Attendees.Count(a => a.Status == AttendanceStatus.Excused);
    public int AbsentCount => Attendees.Count(a => a.Status == AttendanceStatus.Absent);
    public int NotCapturedCount => Attendees.Count(a => a.Status is null);

    public int CountedCount => Attendees.Count(a => a.WasCounted);
    public bool HasRecords => Attendees.Any(a => a.Status is not null);

    public double Percentage => Enrolled == 0 ? 0 : Math.Round(CountedCount * 100.0 / Enrolled, 1);

    /// <summary>Percentage points above or below the mean session.</summary>
    public double AgainstAverage => Math.Round(Percentage - CourseAverage, 1);

    public bool BelowThreshold => Percentage < MinimumPercent;

    public IEnumerable<SessionAttendee> Missed =>
        Attendees.Where(a => !a.WasCounted).OrderBy(a => a.OverallPercent);

    public string TimeRange => $"{StartTime:HH:mm}\u2013{EndTime:HH:mm}";
}
