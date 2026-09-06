using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages.Lecturer;

public class SessionModel : PageModel
{
    private readonly IAnalyticsService _analytics;
    private readonly ISessionAdminService _sessions;
    private readonly CourseOptions _course;

    public SessionModel(IAnalyticsService analytics, ISessionAdminService sessions,
        IOptions<CourseOptions> course)
    {
        _analytics = analytics;
        _sessions = sessions;
        _course = course.Value;
    }

    [BindProperty(SupportsGet = true)]
    public int LectureId { get; set; }

    public SessionOverview Overview { get; private set; } = new();

    public string CourseCode => _course.Code;

    public List<SessionAttendee> Missed { get; private set; } = new();
    public List<SessionAttendee> Present { get; private set; } = new();

    /// <summary>
    /// Counted students first, then absent, then uncaptured. Grouping by status
    /// is what makes the grid readable as a block rather than as noise.
    /// </summary>
    public List<SessionAttendee> GridOrder { get; private set; } = new();

    public string MissedEmailHref { get; private set; } = string.Empty;
    public int MissedCount => Missed.Count;

    /// <summary>Spelled out before the button, not after it.</summary>
    public string DeletionSummary { get; private set; } = string.Empty;

    /// <summary>Opens the delete panel when the page was reached from a Delete link.</summary>
    public bool FocusDelete { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var overview = await _analytics.GetSessionOverviewAsync(LectureId, HttpContext.RequestAborted);
        if (overview is null)
        {
            return NotFound();
        }

        Overview = overview;

        Missed = overview.Missed.ToList();

        Present = overview.Attendees
            .Where(a => a.WasCounted)
            .OrderBy(a => a.RecordedAtUtc ?? DateTime.MaxValue)
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        GridOrder = overview.Attendees
            .OrderBy(a => a.Status switch
            {
                AttendanceStatus.Present => 0,
                AttendanceStatus.Late => 1,
                AttendanceStatus.Excused => 2,
                AttendanceStatus.Absent => 3,
                _ => 4
            })
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preview = await _sessions.PreviewDeleteAsync(LectureId, HttpContext.RequestAborted);
        if (preview is not null)
        {
            DeletionSummary = preview.AttendanceRecords == 0
                ? "Nothing has been captured against this session."
                : $"{preview.AttendanceRecords} attendance " +
                  $"{(preview.AttendanceRecords == 1 ? "record" : "records")}" +
                  (preview.Queries > 0
                      ? $" and {preview.Queries} {(preview.Queries == 1 ? "query" : "queries")}"
                      : string.Empty) +
                  " will be deleted with it.";
        }

        FocusDelete = string.Equals(Request.Query["confirm"], "delete", StringComparison.OrdinalIgnoreCase);

        if (Missed.Count > 0)
        {
            var message = AttendanceMessages.ForMissedSession(
                CourseCode, overview.SessionDate, overview.Topic, Missed.Count);

            MissedEmailHref = MailTo.Bulk(Missed.Select(a => a.Email), message.Subject, message.Body);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var outcome = await _sessions.DeleteAsync(LectureId, HttpContext.RequestAborted);

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = outcome.Success ? "success" : "error";

        return outcome.Success
            ? RedirectToPage("/Lecturer/Sessions")
            : RedirectToPage(new { lectureId = LectureId });
    }

    public string EmailHref(SessionAttendee attendee)
    {
        var message = AttendanceMessages.ForMissedSession(
            CourseCode, Overview.SessionDate, Overview.Topic, 1);

        return MailTo.One(attendee.Email, message.Subject, message.Body);
    }
}
