namespace AttendanceRegister.Models.Entities;

public sealed class Course
{
    public int Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;

    /// <summary>Attendance percentage required to keep DP for the course.</summary>
    public int MinimumAttendancePercent { get; private set; } = 80;

    public ICollection<Lecture> Lectures { get; private set; } = new List<Lecture>();

    private Course() { }

    public Course(string code, string title, int minimumAttendancePercent)
    {
        Code = code.Trim().ToUpperInvariant();
        Title = title.Trim();
        MinimumAttendancePercent = Math.Clamp(minimumAttendancePercent, 0, 100);
    }

    public void UpdateThreshold(int percent) =>
        MinimumAttendancePercent = Math.Clamp(percent, 0, 100);
}
