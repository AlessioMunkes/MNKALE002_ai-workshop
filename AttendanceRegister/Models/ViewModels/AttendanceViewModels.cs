using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Models.ViewModels;

/// <summary>One cell of the attendance strip shown across the app.</summary>
public sealed record SessionCell(int LectureId, DateOnly SessionDate, AttendanceStatus? Status)
{
    public bool IsRecorded => Status.HasValue;
    public bool IsCounted => Status.Counts();
    public string CssModifier => "is-" + Status.ToModifier();
    public string Label => Status.ToLabel();
}

/// <summary>A student's attendance across the whole course.</summary>
public sealed class StudentAttendanceSummary
{
    public int StudentId { get; init; }
    public string StudentNumber { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Carried here so any screen showing a student can offer to write to them.</summary>
    public string Email { get; init; } = string.Empty;

    public int SessionsHeld { get; init; }
    public int SessionsAttended { get; init; }
    public int MinimumPercent { get; init; } = 80;
    public List<SessionCell> Cells { get; init; } = new();

    public double Percentage => SessionsHeld == 0 ? 0 : Math.Round(SessionsAttended * 100.0 / SessionsHeld, 1);
    public bool MeetsThreshold => Percentage >= MinimumPercent;

    /// <summary>How many further sessions can be missed while staying above the threshold.</summary>
    public int SessionsCanStillMiss
    {
        get
        {
            if (SessionsHeld == 0) return 0;
            var allowedAbsences = (int)Math.Floor(SessionsHeld * (100 - MinimumPercent) / 100.0);
            return Math.Max(0, allowedAbsences - (SessionsHeld - SessionsAttended));
        }
    }

    /// <summary>Sessions still needed to climb back above the threshold, assuming perfect attendance from now.</summary>
    public int SessionsNeededToRecover
    {
        get
        {
            if (MeetsThreshold) return 0;
            var needed = 0;
            double attended = SessionsAttended;
            double held = SessionsHeld;
            while (needed < 500 && (held == 0 || attended * 100.0 / held < MinimumPercent))
            {
                attended++;
                held++;
                needed++;
            }
            return needed;
        }
    }
}

/// <summary>Aggregate figures for one lecture, used by the analytics chart.</summary>
public sealed class LectureAttendanceStat
{
    public int LectureId { get; init; }
    public DateOnly SessionDate { get; init; }
    public string Topic { get; init; } = string.Empty;
    public int PresentCount { get; init; }
    public int RecordedCount { get; init; }
    public int Enrolled { get; init; }

    public double Percentage => Enrolled == 0 ? 0 : Math.Round(PresentCount * 100.0 / Enrolled, 1);
}

/// <summary>Course-wide numbers for the lecturer dashboard.</summary>
public sealed class CourseOverview
{
    public string CourseCode { get; init; } = string.Empty;
    public int Enrolled { get; init; }
    public int SessionsHeld { get; init; }
    public int OpenQueries { get; init; }
    public int MinimumPercent { get; init; } = 80;
    public double AverageAttendance { get; init; }
    public List<LectureAttendanceStat> LectureStats { get; init; } = new();
    public List<StudentAttendanceSummary> AtRiskStudents { get; init; } = new();
}

/// <summary>One student's row on the lecturer's register screen.</summary>
public sealed class RegisterRow
{
    public int StudentId { get; init; }
    public string StudentNumber { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public AttendanceStatus Status { get; set; }
    public AttendanceSource? Source { get; init; }
    public string? Note { get; init; }

    public string SourceLabel => Source.ToLabel();
}

/// <summary>
/// One session in a list, for a student or a lecturer. Replaces the two
/// near-identical row types the student history and student detail pages
/// each used to define for themselves.
/// </summary>
public sealed class SessionDetailRow
{
    public int LectureId { get; init; }
    public DateOnly SessionDate { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string Venue { get; init; } = string.Empty;
    public AttendanceStatus? Status { get; init; }
    public AttendanceSource? Source { get; init; }
    public DateTime? RecordedAtUtc { get; init; }
    public string? Note { get; init; }

    public string StatusLabel => Status.ToLabel();
    public string StatusModifier => Status.ToModifier();
    public string SourceLabel => Source.ToLabel();

    public DateTime? RecordedLocal => RecordedAtUtc?.ToLocalTime();
    public string SortKey => SessionDate.ToString("yyyy-MM-dd");
    public string RecordedSortKey => RecordedLocal?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
}
