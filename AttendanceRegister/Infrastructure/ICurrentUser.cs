using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Infrastructure;

/// <summary>
/// Wraps the signed-in principal so page models and services never have to
/// dig through claims by hand.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string? DisplayName { get; }
    string? StudentNumber { get; }
    UserRole? Role { get; }

    /// <summary>Returns the signed-in user id or throws if nobody is signed in.</summary>
    int RequireUserId();
}
