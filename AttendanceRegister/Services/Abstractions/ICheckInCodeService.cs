using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Services.Abstractions;

/// <summary>What a session's code is right now, and whether a submission matches it.</summary>
public interface ICheckInCodeService
{
    string NewSecret();

    /// <summary>The code currently on screen for this session, or null when check-in is closed.</summary>
    string? CurrentCode(Lecture lecture);

    bool Matches(Lecture lecture, string? candidate);

    /// <summary>Seconds until the code changes.</summary>
    int SecondsRemaining();

    int StepSeconds { get; }
}
