using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Models.Import;

public sealed class AttendanceImportResult
{
    public string FileName { get; set; } = string.Empty;
    public bool Committed { get; set; }

    public int RowsRead { get; set; }
    public int SessionColumns { get; set; }
    public int StudentsCreated { get; set; }
    public int LecturesCreated { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsUnchanged { get; set; }

    public List<DateOnly> SessionDates { get; set; } = new();
    public List<ImportIssue> Issues { get; set; } = new();

    public bool HasErrors => Issues.Any(i => i.Severity == ImportIssueSeverity.Error);
    public int ErrorCount => Issues.Count(i => i.Severity == ImportIssueSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == ImportIssueSeverity.Warning);
    public int TotalCellsApplied => RecordsCreated + RecordsUpdated + RecordsUnchanged;

    public static AttendanceImportResult Failed(string fileName, string message) => new()
    {
        FileName = fileName,
        Issues = { ImportIssue.Error(0, "File", message) }
    };
}
