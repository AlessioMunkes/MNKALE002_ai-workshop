using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AttendanceRegister.Data;

namespace AttendanceRegister.Pages.Lecturer;

public class QueriesModel : PageModel
{
    private readonly IAttendanceQueryService _queries;
    private readonly IAttendanceService _attendance;
    private readonly AttendanceDbContext _db;
    private readonly ICurrentUser _currentUser;

    public QueriesModel(IAttendanceQueryService queries, IAttendanceService attendance,
        AttendanceDbContext db, ICurrentUser currentUser)
    {
        _queries = queries;
        _attendance = attendance;
        _db = db;
        _currentUser = currentUser;
    }

    public List<AttendanceQuery> Queries { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public QueryStatus? Filter { get; set; }

    [BindProperty]
    public RaiseInput Raise { get; set; } = new();

    public bool ShowRaiseForm { get; private set; }

    public SelectList StudentChoices { get; private set; } = new(Array.Empty<SelectListItem>());
    public SelectList SessionChoices { get; private set; } = new(Array.Empty<SelectListItem>());
    public SelectList StatusChoices { get; private set; } = new(Array.Empty<SelectListItem>());

    public class RaiseInput
    {
        [Required(ErrorMessage = "Choose the student the query is about.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose the student the query is about.")]
        [Display(Name = "Student")]
        public int StudentId { get; set; }

        [Required(ErrorMessage = "Choose the session the query is about.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose the session the query is about.")]
        [Display(Name = "Session")]
        public int LectureId { get; set; }

        [Display(Name = "It should say")]
        public AttendanceStatus RequestedStatus { get; set; } = AttendanceStatus.Present;

        [Required(ErrorMessage = "Say what the student told you.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Give at least 10 characters of explanation.")]
        [Display(Name = "What the student told you")]
        public string Reason { get; set; } = string.Empty;
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostRaiseAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            ShowRaiseForm = true;
            return Page();
        }

        var outcome = await _queries.RaiseOnBehalfAsync(Raise.StudentId, Raise.LectureId,
            Raise.RequestedStatus, Raise.Reason, _currentUser.RequireUserId(), HttpContext.RequestAborted);

        if (!outcome.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            await LoadAsync();
            ShowRaiseForm = true;
            return Page();
        }

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = "success";
        return RedirectToPage(new { filter = Filter });
    }

    public Task<IActionResult> OnPostApproveAsync(int queryId, string? note) => ResolveAsync(queryId, true, note);

    public Task<IActionResult> OnPostRejectAsync(int queryId, string? note) => ResolveAsync(queryId, false, note);

    private async Task<IActionResult> ResolveAsync(int queryId, bool approve, string? note)
    {
        var outcome = await _queries.ResolveAsync(queryId, approve, note,
            _currentUser.RequireUserId(), HttpContext.RequestAborted);

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = outcome.Success ? "success" : "error";

        return RedirectToPage(new { filter = Filter });
    }

    private async Task LoadAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;

        Queries = await _queries.GetForReviewAsync(Filter, cancellationToken);

        var students = await _db.Students.AsNoTracking()
            .OrderBy(s => s.DisplayName)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.DisplayName + " (" + s.StudentNumber + ")"
            })
            .ToListAsync(cancellationToken);

        StudentChoices = new SelectList(students, "Value", "Text");

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
