namespace AttendanceRegister.Models.Entities;

/// <summary>Audit trail for every spreadsheet upload the lecturer commits.</summary>
public sealed class ImportBatch
{
    public int Id { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public DateTime ImportedAtUtc { get; private set; } = DateTime.UtcNow;
    public int? ImportedByUserId { get; private set; }

    public int RowsRead { get; private set; }
    public int LecturesCreated { get; private set; }
    public int StudentsCreated { get; private set; }
    public int RecordsCreated { get; private set; }
    public int RecordsUpdated { get; private set; }
    public int IssueCount { get; private set; }

    private ImportBatch() { }

    public ImportBatch(string fileName, int? importedByUserId, int rowsRead, int lecturesCreated,
        int studentsCreated, int recordsCreated, int recordsUpdated, int issueCount)
    {
        FileName = fileName;
        ImportedByUserId = importedByUserId;
        RowsRead = rowsRead;
        LecturesCreated = lecturesCreated;
        StudentsCreated = studentsCreated;
        RecordsCreated = recordsCreated;
        RecordsUpdated = recordsUpdated;
        IssueCount = issueCount;
    }
}
