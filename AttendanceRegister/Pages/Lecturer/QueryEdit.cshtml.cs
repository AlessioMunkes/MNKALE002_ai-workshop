using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AttendanceRegister.Pages.Lecturer;

public class QueryEditModel : PageModel
{
    private readonly IAttendanceQueryService _queries;
    private readonly ICurrentUser _currentUser;

    public QueryEditModel(IAttendanceQueryService queries, ICurrentUser currentUser)
    {
        _queries = queries;
        _currentUser = currentUser;
    }

    [BindProperty(SupportsGet = true)]
    public int QueryId { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    [StringLength(1000)]
    [Display(Name = "Note for the student")]
    public string? Note { get; set; }

    public string StudentName { get; private set; } = string.Empty;
    public string SessionText { get; private set; } = string.Empty;
    public string StatusText { get; private set; } = string.Empty;
    public string StatusModifier { get; private set; } = "open";
    public string ResolvedText { get; private set; } = string.Empty;
    public string? ResolutionNote { get; private set; }
    public bool IsOpen { get; private set; }
    public bool RaisedByLecturer { get; private set; }
    public bool Amended { get; private set; }
    public int LectureId { get; private set; }

    public SelectList StatusChoices { get; private set; } = new(Array.Empty<SelectListItem>());

    public class InputModel
    {
        [Display(Name = "It should say")]
        public AttendanceStatus RequestedStatus { get; set; } = AttendanceStatus.Present;

        [Required(ErrorMessage = "A query needs a reason.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Give at least 10 characters of explanation.")]
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var query = await LoadAsync();
        if (query is null)
        {
            return NotFound();
        }

        Input.RequestedStatus = query.RequestedStatus;
        Input.Reason = query.Reason;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var outcome = await _queries.AmendAsync(QueryId, Input.RequestedStatus, Input.Reason,
            HttpContext.RequestAborted);

        return await FinishAsync(outcome, stayOnPage: true);
    }

    public Task<IActionResult> OnPostApproveAsync() => ResolveAsync(true);

    public Task<IActionResult> OnPostRejectAsync() => ResolveAsync(false);

    public async Task<IActionResult> OnPostReopenAsync()
    {
        var outcome = await _queries.ReopenAsync(QueryId, HttpContext.RequestAborted);
        return await FinishAsync(outcome, stayOnPage: true);
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var outcome = await _queries.DeleteAsync(QueryId, HttpContext.RequestAborted);

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = outcome.Success ? "success" : "error";
        return RedirectToPage("/Lecturer/Queries");
    }

    private async Task<IActionResult> ResolveAsync(bool approve)
    {
        var outcome = await _queries.ResolveAsync(QueryId, approve, Note,
            _currentUser.RequireUserId(), HttpContext.RequestAborted);

        return await FinishAsync(outcome, stayOnPage: true);
    }

    private async Task<IActionResult> FinishAsync(QuerySubmissionOutcome outcome, bool stayOnPage)
    {
        if (!outcome.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            await LoadAsync();
            return Page();
        }

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = "success";
        return stayOnPage
            ? RedirectToPage(new { queryId = QueryId })
            : RedirectToPage("/Lecturer/Queries");
    }

    private async Task<AttendanceQuery?> LoadAsync()
    {
        StatusChoices = new SelectList(new[]
        {
            new SelectListItem("Present", nameof(AttendanceStatus.Present)),
            new SelectListItem("Late", nameof(AttendanceStatus.Late)),
            new SelectListItem("Excused", nameof(AttendanceStatus.Excused)),
            new SelectListItem("Absent", nameof(AttendanceStatus.Absent))
        }, "Value", "Text");

        var query = await _queries.GetAsync(QueryId, HttpContext.RequestAborted);
        if (query is null)
        {
            return null;
        }

        StudentName = query.Student?.DisplayName ?? "Unknown student";
        SessionText = query.Lecture is null
            ? "Session removed"
            : query.Lecture.SessionDate.ToString("dddd d MMMM yyyy");
        StatusText = query.Status.ToString();
        StatusModifier = query.Status.ToString().ToLowerInvariant();
        ResolutionNote = query.ResolutionNote;
        IsOpen = query.IsOpen;
        RaisedByLecturer = query.WasRaisedByLecturer;
        Amended = query.WasAmended;
        LectureId = query.LectureId;

        ResolvedText = query.ResolvedAtUtc.HasValue
            ? " on " + query.ResolvedAtUtc.Value.ToLocalTime().ToString("d MMM yyyy HH:mm")
            : string.Empty;

        return query;
    }
}
