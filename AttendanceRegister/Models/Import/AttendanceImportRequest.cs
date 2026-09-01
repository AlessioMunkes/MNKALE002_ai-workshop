namespace AttendanceRegister.Models.Import;

public sealed class AttendanceImportRequest
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }

    /// <summary>Add students that appear in the file but not yet in the database.</summary>
    public bool CreateMissingStudents { get; init; } = true;

    /// <summary>Add lecture sessions for date columns that do not exist yet.</summary>
    public bool CreateMissingLectures { get; init; } = true;

    /// <summary>Parse and report without writing anything. Used by the preview button.</summary>
    public bool ValidateOnly { get; init; }

    /// <summary>Account credited with the change in the audit trail.</summary>
    public int? PerformedByUserId { get; init; }
}
