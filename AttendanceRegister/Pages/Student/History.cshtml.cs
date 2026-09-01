using AttendanceRegister.Data;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Pages.Student;

public class HistoryModel : PageModel
{
    private readonly AttendanceDbContext _db;
    private readonly ICourseContext _courseContext;
    private readonly ICurrentUser _currentUser;

    public HistoryModel(AttendanceDbContext db, ICourseContext courseContext, ICurrentUser currentUser)
    {
        _db = db;
        _courseContext = courseContext;
        _currentUser = currentUser;
    }

    // Shares SessionDetailRow with the lecturer's student detail page rather
    // than defining a second row type that says the same things.
    public List<SessionDetailRow> Rows { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;
        var studentId = _currentUser.RequireUserId();
        var course = await _courseContext.GetAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .ToDictionaryAsync(r => r.LectureId, cancellationToken);

        var lectures = await _db.Lectures.AsNoTracking()
            .Where(l => l.CourseId == course.Id && l.SessionDate <= today)
            .OrderByDescending(l => l.SessionDate)
            .ToListAsync(cancellationToken);

        Rows = lectures.Select(l =>
        {
            records.TryGetValue(l.Id, out var record);
            return new SessionDetailRow
            {
                LectureId = l.Id,
                SessionDate = l.SessionDate,
                Topic = l.Topic,
                Venue = l.Venue,
                Status = record?.Status,
                Source = record?.Source,
                RecordedAtUtc = record?.RecordedAtUtc,
                Note = record?.Note
            };
        }).ToList();
    }
}
