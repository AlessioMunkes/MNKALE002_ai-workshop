using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Models.Import;

/// <summary>A single parsed row: the student plus one raw cell per session column.</summary>
public sealed class AttendanceSheetRow
{
    public int RowNumber { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string StudentNumber { get; init; } = string.Empty;
    public string[] Cells { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Format-neutral representation of an uploaded register. Parsers produce this;
/// the import service consumes it and never sees CSV or Excel specifics.
/// </summary>
public sealed class AttendanceSheet
{
    public string FileName { get; init; } = string.Empty;
    public List<DateOnly> SessionDates { get; init; } = new();
    public List<AttendanceSheetRow> Rows { get; init; } = new();
    public List<ImportIssue> Issues { get; init; } = new();

    public bool HasFatalIssues => Issues.Any(i => i.Severity == ImportIssueSeverity.Error);
}
