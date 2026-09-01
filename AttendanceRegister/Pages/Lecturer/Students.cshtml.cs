using System.Globalization;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages.Lecturer;

public class StudentsModel : PageModel
{
    private readonly IAnalyticsService _analytics;
    private readonly CourseOptions _course;

    public StudentsModel(IAnalyticsService analytics, IOptions<CourseOptions> course)
    {
        _analytics = analytics;
        _course = course.Value;
    }

    public List<ClassListRow> Rows { get; private set; } = new();

    public string CourseCode => _course.Code;
    public int MinimumPercent => _course.MinimumAttendancePercent;
    public int SessionsHeld { get; private set; }
    public int BelowThreshold { get; private set; }
    public string AverageText { get; private set; } = "0";

    /// <summary>Pre-built draft addressed to everyone under the requirement.</summary>
    public string BulkEmailHref { get; private set; } = string.Empty;

    /// <summary>True when more students are below the line than one link can carry.</summary>
    public bool BulkEmailTruncated { get; private set; }

    public async Task OnGetAsync()
    {
        Rows = await _analytics.GetClassListAsync(HttpContext.RequestAborted);

        SessionsHeld = Rows.Count == 0 ? 0 : Rows[0].Summary.SessionsHeld;
        BelowThreshold = Rows.Count(r => !r.Summary.MeetsThreshold);

        var average = Rows.Count == 0 ? 0 : Rows.Average(r => r.Summary.Percentage);
        AverageText = Math.Round(average, 1).ToString("0.#", CultureInfo.InvariantCulture);

        var below = Rows.Where(r => !r.Summary.MeetsThreshold).ToList();
        if (below.Count > 0)
        {
            var message = AttendanceMessages.ForGroupBelowThreshold(CourseCode, MinimumPercent, below.Count);
            BulkEmailHref = MailTo.Bulk(below.Select(r => r.Summary.Email), message.Subject, message.Body);
            BulkEmailTruncated = below.Count > MailTo.MaxBulkRecipients;
        }
    }
}
