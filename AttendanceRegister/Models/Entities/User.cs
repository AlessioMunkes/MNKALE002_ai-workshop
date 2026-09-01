using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceRegister.Models.Entities;

/// <summary>
/// Base class for every account. Mapped with EF Core's table-per-hierarchy
/// strategy, so Student and Lecturer share one Users table plus a discriminator.
/// </summary>
public abstract class User
{
    public int Id { get; private set; }

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Derived from the concrete type, so it can never drift out of sync.</summary>
    [NotMapped]
    public abstract UserRole Role { get; }

    // EF Core materialisation constructor.
    protected User() { }

    protected User(string email, string displayName, string passwordHash)
    {
        Email = NormaliseEmail(email);
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
    }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void Rename(string displayName) => DisplayName = displayName.Trim();

    public void SetEmail(string email) => Email = NormaliseEmail(email);

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();
}
