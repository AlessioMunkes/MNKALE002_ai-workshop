using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AttendanceRegister.Pages.Student;

public class QueriesModel : PageModel
{
    private readonly IAttendanceQueryService _queries;
    private readonly IAttendanceService _attendance;
    private readonly ICurrentUser _currentUser;

    public QueriesModel(IAttendanceQueryService queries, IAttendanceService attendance, ICurrentUser currentUser)
    {
        _queries = queries;
        _attendance = attendance;
        _currentUser = currentUser;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<AttendanceQuery> Queries { get; private set; } = new();
    public SelectList SessionChoices { get; private set; } = new(Array.Empty<SelectListItem>());
    public SelectList StatusChoices { get; private set; } = new(Array.Empty<SelectListItem>());

    public class InputModel
    {
        [Required(ErrorMessage = "Choose the session your query is about.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose the session your query is about.")]
        [Display(Name = "Session")]
        public int LectureId { get; set; }

        [Display(Name = "It should say")]
        public AttendanceStatus RequestedStatus { get; set; } = AttendanceStatus.Present;

        [Required(ErrorMessage = "Explain what happened so your lecturer can act on it.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Give at least 10 characters of explanation.")]
        [Display(Name = "What happened")]
        public string Reason { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(int? lectureId)
    {
        if (lectureId.HasValue)
        {
            Input.LectureId = lectureId.Value;
        }

        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var outcome = await _queries.SubmitAsync(_currentUser.RequireUserId(), Input.LectureId,
            Input.RequestedStatus, Input.Reason, HttpContext.RequestAborted);

        if (!outcome.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            await LoadAsync();
            return Page();
        }

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = "success";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;
        var studentId = _currentUser.RequireUserId();

        Queries = await _queries.GetForStudentAsync(studentId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var sessions = (await _attendance.GetSessionsAsync(cancellationToken))
            .Where(l => l.SessionDate <= today)
            .OrderByDescending(l => l.SessionDate)
            .Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = l.SessionDate.ToString("ddd d MMM yyyy") +
                       (string.IsNullOrWhiteSpace(l.Topic) ? string.Empty : $" — {l.Topic}")
            })
            .ToList();

        SessionChoices = new SelectList(sessions, "Value", "Text");

        StatusChoices = new SelectList(new[]
        {
            new SelectListItem("Present", nameof(AttendanceStatus.Present)),
            new SelectListItem("Late", nameof(AttendanceStatus.Late)),
            new SelectListItem("Excused", nameof(AttendanceStatus.Excused)),
            new SelectListItem("Absent", nameof(AttendanceStatus.Absent))
        }, "Value", "Text");
    }
}
