using System.Security.Claims;
using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Infrastructure;

public sealed class CurrentUser : ICurrentUser
{
    public const string StudentNumberClaim = "student_number";

    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User;
    }

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated == true;

    public int? UserId =>
        int.TryParse(_principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? DisplayName => _principal?.FindFirstValue(ClaimTypes.Name);

    public string? StudentNumber => _principal?.FindFirstValue(StudentNumberClaim);

    public UserRole? Role =>
        Enum.TryParse<UserRole>(_principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public int RequireUserId() =>
        UserId ?? throw new InvalidOperationException("No authenticated user is available on this request.");
}
