using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Models.Import;

/// <summary>
/// One problem found while reading an uploaded register, addressed to a human:
/// where it happened, what is wrong, and what to do about it.
/// </summary>
public sealed record ImportIssue(int RowNumber, string Location, string Message, ImportIssueSeverity Severity)
{
    public static ImportIssue Error(int rowNumber, string location, string message) =>
        new(rowNumber, location, message, ImportIssueSeverity.Error);

    public static ImportIssue Warning(int rowNumber, string location, string message) =>
        new(rowNumber, location, message, ImportIssueSeverity.Warning);
}
