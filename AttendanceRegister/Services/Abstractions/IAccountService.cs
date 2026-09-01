using System.Security.Claims;
using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Services.Abstractions;

public sealed record AuthenticatedUser(int Id, string Email, string DisplayName, UserRole Role, string? StudentNumber);

public interface IAccountService
{
    /// <summary>Accepts either an email address or a student number as the identifier.</summary>
    Task<AuthenticatedUser?> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default);

    ClaimsPrincipal BuildPrincipal(AuthenticatedUser user, string authenticationScheme);
}
