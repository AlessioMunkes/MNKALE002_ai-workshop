using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Services.Import;

/// <summary>
/// Translates whatever a tutor typed into a cell into an AttendanceStatus.
/// Kept in one place so CSV and Excel uploads can never disagree.
/// </summary>
public static class AttendanceValueMap
{
    public static bool IsBlank(string raw) => string.IsNullOrWhiteSpace(raw);

    public static bool TryMap(string raw, out AttendanceStatus status)
    {
        status = AttendanceStatus.Absent;
        var value = raw.Trim().Trim('"').ToLowerInvariant();

        switch (value)
        {
            case "1" or "p" or "y" or "x" or "yes" or "true" or "present" or "\u2713" or "\u2714":
                status = AttendanceStatus.Present;
                return true;
            case "0" or "a" or "n" or "no" or "false" or "absent" or "-":
                status = AttendanceStatus.Absent;
                return true;
            case "l" or "late":
                status = AttendanceStatus.Late;
                return true;
            case "e" or "ex" or "excused":
                status = AttendanceStatus.Excused;
                return true;
            default:
                return false;
        }
    }

    public static string Describe() =>
        "1, P, Y, X or Present = present; 0, A, N or Absent = absent; L = late; E = excused; an empty cell is left untouched.";

    public static string ToCell(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "1",
        AttendanceStatus.Late => "L",
        AttendanceStatus.Excused => "E",
        _ => "0"
    };
}
