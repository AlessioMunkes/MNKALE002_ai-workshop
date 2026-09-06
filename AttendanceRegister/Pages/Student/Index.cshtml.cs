using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages.Student;

public class IndexModel : PageModel
{
    private readonly IAttendanceService _attendance;
    private readonly ICurrentUser _currentUser;

    public IndexModel(IAttendanceService attendance, ICurrentUser currentUser)
    {
        _attendance = attendance;
        _currentUser = currentUser;
    }

    public StudentAttendanceSummary Summary { get; private set; } = new();
    public Lecture? OpenSession { get; private set; }

    /// <summary>Set for one render after a successful check-in.</summary>
    public string? JustCheckedIn { get; private set; }

    /// <summary>
    /// The rate before this check-in, so the figure can count up from where it
    /// was rather than from zero. Attendance climbing by one session is the
    /// thing worth showing; a number sweeping up from nothing is just motion.
    /// </summary>
    public double PreviousPercent { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter the six-character code shown in the lecture.")]
    [StringLength(8, MinimumLength = 4, ErrorMessage = "Session codes are six characters long.")]
    [Display(Name = "Session code")]
    public string Code { get; set; } = string.Empty;

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var outcome = await _attendance.CheckInAsync(_currentUser.RequireUserId(), Code, HttpContext.RequestAborted);

        if (!outcome.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            await LoadAsync();
            return Page();
        }

        TempData["CheckedIn"] = outcome.Message;
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;
        Summary = await _attendance.GetSummaryAsync(_currentUser.RequireUserId(), cancellationToken);
        OpenSession = await _attendance.GetOpenSessionAsync(cancellationToken);

        JustCheckedIn = TempData["CheckedIn"] as string;

        PreviousPercent = Summary.SessionsHeld == 0 || Summary.SessionsAttended == 0
            ? 0
            : Math.Round((Summary.SessionsAttended - 1) * 100.0 / Summary.SessionsHeld, 1);
    }
}
