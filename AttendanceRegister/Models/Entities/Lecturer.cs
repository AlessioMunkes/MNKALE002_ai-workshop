namespace AttendanceRegister.Models.Entities;

public sealed class Lecturer : User
{
    public string StaffNumber { get; private set; } = string.Empty;

    public override UserRole Role => UserRole.Lecturer;

    private Lecturer() { }

    public Lecturer(string staffNumber, string displayName, string email, string passwordHash)
        : base(email, displayName, passwordHash)
    {
        StaffNumber = staffNumber.Trim();
    }
}
