using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Models.ViewModels;

/// <summary>One student's row on the lecturer's class list.</summary>
public sealed class ClassListRow
{
    public required StudentAttendanceSummary Summary { get; init; }

    /// <summary>Most recent session the student was counted at, if any.</summary>
    public DateOnly? LastAttendedOn { get; init; }

    public int OpenQueries { get; init; }
    public bool IsActive { get; init; } = true;

    public string Standing => Summary.MeetsThreshold ? "Above requirement" : "Below requirement";

    /// <summary>Sessions of slack left, or sessions needed to climb back.</summary>
    public string Margin => Summary.MeetsThreshold
        ? $"{Summary.SessionsCanStillMiss} to spare"
        : $"{Summary.SessionsNeededToRecover} to recover";

    /// <summary>
    /// Signed, so one click orders the class from safest to most at risk.
    /// The displayed text stays readable because only the key is signed.
    /// </summary>
    public int MarginSortKey => Summary.MeetsThreshold
        ? Summary.SessionsCanStillMiss
        : -Summary.SessionsNeededToRecover;

    public string LastAttendedSortKey => LastAttendedOn?.ToString("yyyy-MM-dd") ?? string.Empty;
}

/// <summary>Everything the student detail page needs, in one object.</summary>
public sealed class StudentAttendanceDetail
{
    public required StudentAttendanceSummary Summary { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public List<SessionDetailRow> Sessions { get; init; } = new();
    public List<AttendanceQuery> Queries { get; init; } = new();

    public int SelfCheckIns => Sessions.Count(s => s.Source == AttendanceSource.SelfCheckIn);
}
