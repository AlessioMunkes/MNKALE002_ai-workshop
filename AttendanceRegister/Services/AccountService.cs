using System.Security.Claims;
using AttendanceRegister.Data;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using AttendanceRegister.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Services;

public sealed class AccountService : IAccountService
{
    private readonly AttendanceDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AccountService(AttendanceDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(string identifier, string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var email = Models.Entities.User.NormaliseEmail(identifier);
        var studentNumber = Student.NormaliseStudentNumber(identifier);

        // Two narrow queries rather than one query with a type cast: SQLite plans
        // both off an index, and neither depends on EF translating a downcast.
        Models.Entities.User? account = await _db.Users
            .FirstOrDefaultAsync(u => u.IsActive && u.Email == email, cancellationToken);

        account ??= await _db.Students
            .FirstOrDefaultAsync(s => s.IsActive && s.StudentNumber == studentNumber, cancellationToken);

        if (account is null || !_hasher.Verify(password, account.PasswordHash))
        {
            return null;
        }

        return new AuthenticatedUser(
            account.Id,
            account.Email,
            account.DisplayName,
            account.Role,
            (account as Student)?.StudentNumber);
    }

    public ClaimsPrincipal BuildPrincipal(AuthenticatedUser user, string authenticationScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.StudentNumber))
        {
            claims.Add(new Claim(CurrentUser.StudentNumberClaim, user.StudentNumber));
        }

        var identity = new ClaimsIdentity(claims, authenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
