namespace AttendanceRegister.Infrastructure;

/// <summary>Strongly typed access to the "Course" configuration section.</summary>
public sealed class CourseOptions
{
    public const string SectionName = "Course";

    public string Code { get; set; } = "INF3003W";
    public string Title { get; set; } = "Business Application Development";

    /// <summary>Attendance percentage a student must reach to retain DP.</summary>
    public int MinimumAttendancePercent { get; set; } = 80;

    /// <summary>
    /// Domain used when an email address has to be derived from a student
    /// number. Configuration rather than a literal in the importer, so a
    /// different institution changes one line of appsettings.json.
    /// </summary>
    public string StudentEmailDomain { get; set; } = "myuct.ac.za";
}
