using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AttendanceRegister.Pages.Lecturer;

public class RegisterModel : PageModel
{
    private readonly IAttendanceService _attendance;
    private readonly ICurrentUser _currentUser;

    public RegisterModel(IAttendanceService attendance, ICurrentUser currentUser)
    {
        _attendance = attendance;
        _currentUser = currentUser;
    }

    [BindProperty(SupportsGet = true)]
    public int LectureId { get; set; }

    [BindProperty]
    public List<RowInput> Rows { get; set; } = new();

    public string SessionDateText { get; private set; } = string.Empty;
    public string Topic { get; private set; } = string.Empty;
    public SelectList StatusChoices { get; private set; } = new(Array.Empty<SelectListItem>());

    private Dictionary<int, string> _sources = new();

    public string SourceLabel(int studentId) => _sources.TryGetValue(studentId, out var label) ? label : "—";

    public class RowInput
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var lecture = await _attendance.GetSessionAsync(LectureId, HttpContext.RequestAborted);
        if (lecture is null)
        {
            return NotFound();
        }

        await LoadAsync(lecture);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var lecture = await _attendance.GetSessionAsync(LectureId, HttpContext.RequestAborted);
        if (lecture is null)
        {
            return NotFound();
        }

        if (Rows.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Nothing was submitted. Reload the page and try again.");
            await LoadAsync(lecture);
            return Page();
        }

        var statuses = Rows
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.First().Status);

        var changed = await _attendance.SaveRegisterAsync(LectureId, statuses,
            _currentUser.RequireUserId(), HttpContext.RequestAborted);

        TempData["Flash"] = changed == 0
            ? "Nothing changed, so nothing was saved."
            : $"Saved. {changed} {(changed == 1 ? "record" : "records")} updated for {lecture.SessionDate:d MMMM yyyy}.";
        TempData["FlashKind"] = changed == 0 ? "info" : "success";

        return RedirectToPage(new { lectureId = LectureId });
    }

    public async Task<IActionResult> OnPostMarkAllAsync(AttendanceStatus bulkStatus)
    {
        var lecture = await _attendance.GetSessionAsync(LectureId, HttpContext.RequestAborted);
        if (lecture is null)
        {
            return NotFound();
        }

        var register = await _attendance.GetRegisterAsync(LectureId, HttpContext.RequestAborted);
        var statuses = register.ToDictionary(r => r.StudentId, _ => bulkStatus);

        var changed = await _attendance.SaveRegisterAsync(LectureId, statuses,
            _currentUser.RequireUserId(), HttpContext.RequestAborted);

        TempData["Flash"] = $"Marked {statuses.Count} students as {bulkStatus.ToString().ToLowerInvariant()}. {changed} records changed.";
        TempData["FlashKind"] = "success";

        return RedirectToPage(new { lectureId = LectureId });
    }

    private async Task LoadAsync(AttendanceRegister.Models.Entities.Lecture lecture)
    {
        SessionDateText = lecture.SessionDate.ToString("dddd d MMMM yyyy");
        Topic = lecture.Topic;

        var register = await _attendance.GetRegisterAsync(LectureId, HttpContext.RequestAborted);

        Rows = register.Select(r => new RowInput
        {
            StudentId = r.StudentId,
            StudentNumber = r.StudentNumber,
            DisplayName = r.DisplayName,
            Status = r.Status
        }).ToList();

        // RegisterRow.SourceLabel now defers to AttendanceRules, so this page
        // no longer keeps its own copy of the source vocabulary.
        _sources = register.ToDictionary(r => r.StudentId, r => r.SourceLabel);

        StatusChoices = new SelectList(new[]
        {
            new SelectListItem("Present", nameof(AttendanceStatus.Present)),
            new SelectListItem("Absent", nameof(AttendanceStatus.Absent)),
            new SelectListItem("Late", nameof(AttendanceStatus.Late)),
            new SelectListItem("Excused", nameof(AttendanceStatus.Excused))
        }, "Value", "Text");
    }
}
