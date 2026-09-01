namespace AttendanceRegister.Services.Abstractions;

public sealed record StudentAdminOutcome(bool Success, string Message, int? StudentId = null);

/// <summary>
/// Everything a lecturer can do to a student record. Kept apart from
/// IAccountService, which is only about signing in.
/// </summary>
public interface IStudentAdminService
{
    Task<StudentAdminOutcome> AddAsync(string studentNumber, string displayName, string? email,
        string password, CancellationToken cancellationToken = default);

    Task<StudentAdminOutcome> UpdateAsync(int studentId, string studentNumber, string displayName,
        string email, CancellationToken cancellationToken = default);

    Task<StudentAdminOutcome> SetActiveAsync(int studentId, bool active,
        CancellationToken cancellationToken = default);

    Task<StudentAdminOutcome> ResetPasswordAsync(int studentId, string password,
        CancellationToken cancellationToken = default);

    /// <summary>Suggests an address for a student number, using the configured domain.</summary>
    string SuggestEmail(string studentNumber);
}
