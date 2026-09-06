using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using AttendanceRegister.Services.Security;

namespace AttendanceRegister.Services;

/// <summary>
/// Keeps the clock out of the entity. Lecture owns whether check-in is open and
/// what the secret is; working out which code that secret produces at this
/// instant is not something a domain object should reach out to the system
/// clock to answer.
/// </summary>
public sealed class CheckInCodeService : ICheckInCodeService
{
    public int StepSeconds => RotatingCheckInCode.StepSeconds;

    public string NewSecret() => RotatingCheckInCode.NewSecret();

    public string? CurrentCode(Lecture lecture) =>
        lecture.IsCheckInOpen && lecture.CheckInSecret is not null
            ? RotatingCheckInCode.For(lecture.CheckInSecret, DateTimeOffset.UtcNow)
            : null;

    public bool Matches(Lecture lecture, string? candidate) =>
        lecture.IsCheckInOpen &&
        lecture.CheckInSecret is not null &&
        RotatingCheckInCode.Matches(lecture.CheckInSecret, candidate, DateTimeOffset.UtcNow);

    public int SecondsRemaining() => RotatingCheckInCode.SecondsRemaining(DateTimeOffset.UtcNow);
}
