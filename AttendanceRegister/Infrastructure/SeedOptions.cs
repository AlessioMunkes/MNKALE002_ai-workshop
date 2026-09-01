namespace AttendanceRegister.Infrastructure;

/// <summary>Controls the first-run bootstrap of demonstration data.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; } = true;
    public string AttendanceFile { get; set; } = "App_Data/seed/INF3003_2026_AttendanceList.csv";
    public string LecturerEmail { get; set; } = "lecturer@uct.ac.za";
    public string LecturerPassword { get; set; } = "Lecturer#2026";
    public string LecturerStaffNumber { get; set; } = "STAFF001";
    public string DefaultStudentPassword { get; set; } = "Student#2026";
}
