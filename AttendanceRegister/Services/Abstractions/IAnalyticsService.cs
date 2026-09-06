using AttendanceRegister.Models.ViewModels;

namespace AttendanceRegister.Services.Abstractions;

public interface IAnalyticsService
{
    Task<CourseOverview> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Every enrolled student with their attendance summarised.</summary>
    Task<List<ClassListRow>> GetClassListAsync(CancellationToken cancellationToken = default);

    /// <summary>One student's full session history, or null if no such student.</summary>
    Task<StudentAttendanceDetail?> GetStudentDetailAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Everything about one lecture, or null if no such lecture.</summary>
    Task<SessionOverview?> GetSessionOverviewAsync(int lectureId, CancellationToken cancellationToken = default);
}
