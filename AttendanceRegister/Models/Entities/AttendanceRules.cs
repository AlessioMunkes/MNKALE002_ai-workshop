namespace AttendanceRegister.Models.Entities;

/// <summary>
/// Single source of truth for which statuses count towards attendance and how
/// statuses and sources are described to a person.
///
/// These rules previously lived as copy-pasted switch expressions in two view
/// models and three page models, and had already drifted: the same source read
/// "Your check-in" on one screen and "Student check-in" on another. Anything
/// that needs to label or count a status now comes here.
/// </summary>
public static class AttendanceRules
{
    /// <summary>
    /// Statuses that count towards a student's percentage.
    ///
    /// Deliberately an array rather than a predicate method: EF Core translates
    /// <c>Counted.Contains(r.Status)</c> into a SQL IN clause, where it could
    /// not translate a call to <see cref="Counts"/>. One list, two consumers.
    /// </summary>
    public static readonly AttendanceStatus[] Counted =
    {
        AttendanceStatus.Present,
        AttendanceStatus.Late,
        AttendanceStatus.Excused
    };

    /// <summary>In-memory counterpart of <see cref="Counted"/>.</summary>
    public static bool Counts(this AttendanceStatus status) =>
        status is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.Excused;

    public static bool Counts(this AttendanceStatus? status) =>
        status.HasValue && status.Value.Counts();

    public static string ToLabel(this AttendanceStatus? status) =>
        status?.ToString() ?? "Not captured";

    /// <summary>Bare modifier word. Callers add their own block prefix.</summary>
    public static string ToModifier(this AttendanceStatus? status) => status switch
    {
        AttendanceStatus.Present => "present",
        AttendanceStatus.Late => "late",
        AttendanceStatus.Excused => "excused",
        AttendanceStatus.Absent => "absent",
        _ => "unrecorded"
    };

    public static string ToLabel(this AttendanceSource? source) => source switch
    {
        AttendanceSource.SelfCheckIn => "Student check-in",
        AttendanceSource.SpreadsheetImport => "Spreadsheet upload",
        AttendanceSource.LecturerEdit => "Lecturer",
        AttendanceSource.QueryResolution => "Query outcome",
        _ => "Not captured"
    };
}
