using AttendanceRegister.Infrastructure;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages;

[AllowAnonymous]
public class HelpModel : PageModel
{
    private readonly CourseOptions _course;
    private readonly IAttendanceImportService _importService;

    public HelpModel(IOptions<CourseOptions> course, IAttendanceImportService importService)
    {
        _course = course.Value;
        _importService = importService;
    }

    public int MinimumPercent => _course.MinimumAttendancePercent;
    public IReadOnlyCollection<string> SupportedExtensions => _importService.SupportedExtensions;

    public void OnGet() { }
}
