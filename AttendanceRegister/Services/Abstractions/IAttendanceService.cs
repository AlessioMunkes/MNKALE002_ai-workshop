using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.ViewModels;

namespace AttendanceRegister.Services.Abstractions;

public sealed record CheckInOutcome(bool Success, string Message);

public interface IAttendanceService
{
    Task<StudentAttendanceSummary> GetSummaryAsync(int studentId, CancellationToken cancellationToken = default);
    Task<List<Lecture>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<Lecture?> GetSessionAsync(int lectureId, CancellationToken cancellationToken = default);
    Task<Lecture?> GetOpenSessionAsync(CancellationToken cancellationToken = default);

    Task<CheckInOutcome> CheckInAsync(int studentId, string code, CancellationToken cancellationToken = default);

    Task<List<RegisterRow>> GetRegisterAsync(int lectureId, CancellationToken cancellationToken = default);
    Task<int> SaveRegisterAsync(int lectureId, IReadOnlyDictionary<int, AttendanceStatus> statuses,
        int actorId, CancellationToken cancellationToken = default);

    Task<bool> SetStatusAsync(int lectureId, int studentId, AttendanceStatus status, AttendanceSource source,
        int actorId, string? note, CancellationToken cancellationToken = default);

    Task<Lecture> CreateSessionAsync(DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime,
        string topic, string venue, CancellationToken cancellationToken = default);

    Task<string> OpenCheckInAsync(int lectureId, int minutes, CancellationToken cancellationToken = default);
    Task CloseCheckInAsync(int lectureId, CancellationToken cancellationToken = default);
}
