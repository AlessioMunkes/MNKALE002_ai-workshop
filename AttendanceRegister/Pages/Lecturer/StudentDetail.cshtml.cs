using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages.Lecturer;

public class StudentDetailModel : PageModel
{
    private readonly IAnalyticsService _analytics;
    private readonly IStudentAdminService _students;

    public StudentDetailModel(IAnalyticsService analytics, IStudentAdminService students)
    {
        _analytics = analytics;
        _students = students;
    }

    [BindProperty(SupportsGet = true)]
    public int StudentId { get; set; }

    [BindProperty]
    public EditInput Edit { get; set; } = new();

    [BindProperty]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "The new password needs at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    public StudentAttendanceDetail Detail { get; private set; } =
        new() { Summary = new StudentAttendanceSummary() };

    public bool ShowEditForm { get; private set; }

    /// <summary>
    /// The same chart the student sees on their own dashboard. Showing the
    /// lecturer something different from what the student is looking at is how
    /// a conversation about attendance goes wrong.
    /// </summary>
    public StudentTrend Trend { get; private set; } = new();

    public string DriftText
    {
        get
        {
            var drift = Trend.RecentDrift;
            if (Math.Abs(drift) < 0.1) { return string.Empty; }
            var direction = drift > 0 ? "up" : "down";
            return $"{direction} {Math.Abs(drift).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} points over the last five sessions";
        }
    }

    public int MinimumPasswordLength => StudentAdminService.MinimumPasswordLength;

    public class EditInput
    {
        [Required(ErrorMessage = "A student needs a number.")]
        [StringLength(20)]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "A student needs a name.")]
        [StringLength(200)]
        [Display(Name = "Full name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "A student needs an email address.")]
        [EmailAddress(ErrorMessage = "That does not look like an email address.")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync())
        {
            return NotFound();
        }

        Edit = new EditInput
        {
            StudentNumber = Detail.Summary.StudentNumber,
            DisplayName = Detail.Summary.DisplayName,
            Email = Detail.Email
        };

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        // Only the edit form's own fields matter here; a blank password box on
        // the neighbouring form must not block the save.
        ModelState.Remove(nameof(NewPassword));

        if (!ModelState.IsValid)
        {
            return await RedisplayAsync();
        }

        var outcome = await _students.UpdateAsync(StudentId, Edit.StudentNumber, Edit.DisplayName,
            Edit.Email, HttpContext.RequestAborted);

        return await FinishAsync(outcome);
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        ModelState.Remove("Edit.StudentNumber");
        ModelState.Remove("Edit.DisplayName");
        ModelState.Remove("Edit.Email");

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ModelState.AddModelError(nameof(NewPassword), "Enter the new password.");
        }

        if (!ModelState.IsValid)
        {
            return await RedisplayAsync();
        }

        var outcome = await _students.ResetPasswordAsync(StudentId, NewPassword!, HttpContext.RequestAborted);
        return await FinishAsync(outcome);
    }

    public async Task<IActionResult> OnPostSetActiveAsync(bool active)
    {
        var outcome = await _students.SetActiveAsync(StudentId, active, HttpContext.RequestAborted);
        return await FinishAsync(outcome);
    }

    private async Task<IActionResult> FinishAsync(StudentAdminOutcome outcome)
    {
        if (!outcome.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            return await RedisplayAsync();
        }

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = "success";
        return RedirectToPage(new { studentId = StudentId });
    }

    private async Task<IActionResult> RedisplayAsync()
    {
        if (!await LoadAsync())
        {
            return NotFound();
        }

        ShowEditForm = true;
        return Page();
    }

    private async Task<bool> LoadAsync()
    {
        var detail = await _analytics.GetStudentDetailAsync(StudentId, HttpContext.RequestAborted);
        if (detail is null)
        {
            return false;
        }

        Detail = detail;
        Trend = await _analytics.GetStudentTrendAsync(StudentId, HttpContext.RequestAborted);
        return true;
    }
}
