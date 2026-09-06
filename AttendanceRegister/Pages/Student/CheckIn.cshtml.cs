using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages.Student;

/// <summary>
/// Where a scanned QR code lands. Sits inside the Student folder, so an
/// unauthenticated scan is bounced through sign-in and returned here by the
/// cookie handler's returnUrl — the student never has to find this page again
/// by hand.
/// </summary>
public class CheckInModel : PageModel
{
    private readonly IAttendanceService _attendance;
    private readonly ICurrentUser _currentUser;

    public CheckInModel(IAttendanceService attendance, ICurrentUser currentUser)
    {
        _attendance = attendance;
        _currentUser = currentUser;
    }

    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Enter the session code shown in the lecture.")]
    [StringLength(8, MinimumLength = 4, ErrorMessage = "Session codes are six characters long.")]
    [Display(Name = "Session code")]
    public string Code { get; set; } = string.Empty;

    public Lecture? Session { get; private set; }
    public string ClosesAt { get; private set; } = string.Empty;
    public string Headline { get; private set; } = "Check in";

    public async Task OnGetAsync()
    {
        await LoadAsync();

        if (Session is not null)
        {
            Headline = string.IsNullOrWhiteSpace(Code) ? "Check in" : "Confirm your attendance";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var outcome = await _attendance.CheckInAsync(_currentUser.RequireUserId(), Code,
            HttpContext.RequestAborted);

        if (!outcome.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            await LoadAsync();
            return Page();
        }

        // The dashboard shows its own confirmation, so the flash banner would
        // only repeat it above a larger version of the same message.
        TempData["CheckedIn"] = outcome.Message;
        return RedirectToPage("/Student/Index");
    }

    private async Task LoadAsync()
    {
        Session = await _attendance.GetOpenSessionAsync(HttpContext.RequestAborted);

        if (Session?.CheckInClosesAtUtc is not null)
        {
            ClosesAt = Session.CheckInClosesAtUtc.Value.ToLocalTime().ToString("HH:mm");
        }
    }
}
