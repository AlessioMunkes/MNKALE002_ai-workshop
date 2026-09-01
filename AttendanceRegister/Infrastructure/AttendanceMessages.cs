using AttendanceRegister.Models.ViewModels;

namespace AttendanceRegister.Infrastructure;

/// <summary>
/// The wording of the mail a lecturer sends about attendance, in one place.
///
/// Drafts stay short on purpose: a mailto link carries its body in the URL, and
/// a long template is both awkward to edit afterwards and close to the length
/// some mail clients silently truncate.
/// </summary>
public static class AttendanceMessages
{
    public static (string Subject, string Body) ForStudent(StudentAttendanceSummary student, string courseCode)
    {
        var subject = $"{courseCode} attendance — {student.StudentNumber}";

        var standing = student.MeetsThreshold
            ? $"That is above the {student.MinimumPercent}% requirement, with {student.SessionsCanStillMiss} " +
              $"{(student.SessionsCanStillMiss == 1 ? "session" : "sessions")} of margin left."
            : $"That is below the {student.MinimumPercent}% requirement. Attending the next " +
              $"{student.SessionsNeededToRecover} {(student.SessionsNeededToRecover == 1 ? "session" : "sessions")} " +
              "would bring you back above it.";

        var body =
            $"Hi {FirstName(student.DisplayName)},\n\n" +
            $"Your {courseCode} attendance currently stands at {student.Percentage:0.#}% " +
            $"({student.SessionsAttended} of {student.SessionsHeld} sessions captured so far).\n\n" +
            $"{standing}\n\n" +
            "If any of those sessions look wrong, raise a query on the attendance register and I will check it " +
            "against the record for that lecture.\n\n" +
            "Regards\n";

        return (subject, body);
    }

    public static (string Subject, string Body) ForGroupBelowThreshold(string courseCode, int minimumPercent, int count)
    {
        var subject = $"{courseCode} attendance — action needed";

        var body =
            "Hi everyone,\n\n" +
            $"You are receiving this because your {courseCode} attendance is currently below the " +
            $"{minimumPercent}% requirement. Everyone is in BCC, so nobody else can see who was written to.\n\n" +
            "Sign in to the attendance register to see exactly which sessions you are missing and how many " +
            "you would need to attend to get back above the line.\n\n" +
            "If a session is recorded wrongly, raise a query there rather than replying to this message — " +
            "it goes straight into the queue with the session attached.\n\n" +
            "Regards\n";

        return (subject, body);
    }

    private static string FirstName(string displayName)
    {
        var trimmed = displayName.Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }
}
