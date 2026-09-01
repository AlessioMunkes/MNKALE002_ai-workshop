namespace AttendanceRegister.Models.Entities;

public sealed class Student : User
{
    public string StudentNumber { get; private set; } = string.Empty;

    public ICollection<AttendanceRecord> AttendanceRecords { get; private set; } = new List<AttendanceRecord>();
    public ICollection<AttendanceQuery> Queries { get; private set; } = new List<AttendanceQuery>();

    public override UserRole Role => UserRole.Student;

    private Student() { }

    public Student(string studentNumber, string displayName, string email, string passwordHash)
        : base(email, displayName, passwordHash)
    {
        StudentNumber = NormaliseStudentNumber(studentNumber);
    }

    /// <summary>
    /// Correcting a mistyped student number has to be possible, otherwise the
    /// only fix is deleting the account and losing its attendance with it. The
    /// unique index still stops two students sharing one number.
    /// </summary>
    public void ChangeStudentNumber(string studentNumber) =>
        StudentNumber = NormaliseStudentNumber(studentNumber);

    public static string NormaliseStudentNumber(string studentNumber) =>
        studentNumber.Trim().ToUpperInvariant();
}
