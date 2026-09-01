namespace AttendanceRegister.Models.Entities;

/// <summary>Who the account belongs to. Drives authorisation policies.</summary>
public enum UserRole
{
    Student = 1,
    Lecturer = 2
}

/// <summary>The state of one student at one lecture.</summary>
public enum AttendanceStatus
{
    Absent = 0,
    Present = 1,
    Late = 2,
    Excused = 3
}

/// <summary>Where an attendance record came from. Kept for auditability.</summary>
public enum AttendanceSource
{
    SelfCheckIn = 1,
    SpreadsheetImport = 2,
    LecturerEdit = 3,
    QueryResolution = 4
}

/// <summary>Lifecycle of a student's attendance dispute.</summary>
public enum QueryStatus
{
    Open = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>Severity of a problem found while reading an uploaded register.</summary>
public enum ImportIssueSeverity
{
    Warning = 1,
    Error = 2
}
