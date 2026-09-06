using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages.Lecturer;

public class HeatmapModel : PageModel
{
    private readonly IAnalyticsService _analytics;
    private readonly CourseOptions _course;

    public HeatmapModel(IAnalyticsService analytics, IOptions<CourseOptions> course)
    {
        _analytics = analytics;
        _course = course.Value;
    }

    public ClassHeatmap Heatmap { get; private set; } = new();

    public string CourseCode => _course.Code;
    public int MinimumPercent => _course.MinimumAttendancePercent;

    public async Task OnGetAsync()
    {
        // Reuses the class list rather than querying again: it already carries a
        // cell per student per session, which is exactly what the map needs.
        var classList = await _analytics.GetClassListAsync(HttpContext.RequestAborted);
        Heatmap = ClassHeatmap.From(classList, MinimumPercent);
    }
}
