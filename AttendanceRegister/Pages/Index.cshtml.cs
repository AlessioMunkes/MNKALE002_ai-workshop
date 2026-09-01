using AttendanceRegister.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages;

public class IndexModel : PageModel
{
    private readonly CourseOptions _course;

    public IndexModel(IOptions<CourseOptions> course) => _course = course.Value;

    public string CourseTitle => _course.Title;

    public IActionResult OnGet()
    {
        if (User.IsInRole("Lecturer"))
        {
            return RedirectToPage("/Lecturer/Index");
        }

        return User.IsInRole("Student") ? RedirectToPage("/Student/Index") : Page();
    }
}
