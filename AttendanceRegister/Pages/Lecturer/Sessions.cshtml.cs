using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Data;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Pages.Lecturer;

public class SessionsModel : PageModel
{
    private readonly IAttendanceService _attendance;
    private readonly AttendanceDbContext _db;

    public SessionsModel(IAttendanceService attendance, AttendanceDbContext db)
    {
        _attendance = attendance;
        _db = db;
    }

    [BindProperty]
    public NewSessionInput NewSession { get; set; } = new();

    public List<AttendanceRegister.Models.Entities.Lecture> Sessions { get; private set; } = new();
    public AttendanceRegister.Models.Entities.Lecture? OpenSession { get; private set; }
    public int Enrolled { get; private set; }
    public string OpenCloseTime { get; private set; } = string.Empty;

    private Dictionary<int, int> _recorded = new();

    public int RecordedCount(int lectureId) => _recorded.TryGetValue(lectureId, out var count) ? count : 0;

    public class NewSessionInput
    {
        [Required(ErrorMessage = "Pick a date for the session.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateOnly SessionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [DataType(DataType.Time)]
        [Display(Name = "Starts")]
        public TimeOnly StartTime { get; set; } = new(8, 0);

        [DataType(DataType.Time)]
        [Display(Name = "Ends")]
        public TimeOnly EndTime { get; set; } = new(8, 45);

        [StringLength(200)]
        [Display(Name = "Topic")]
        public string Topic { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Venue")]
        public string Venue { get; set; } = string.Empty;
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        if (NewSession.EndTime <= NewSession.StartTime)
        {
            ModelState.AddModelError("NewSession.EndTime", "The end time must be after the start time.");
            await LoadAsync();
            return Page();
        }

        try
        {
            var lecture = await _attendance.CreateSessionAsync(NewSession.SessionDate, NewSession.StartTime,
                NewSession.EndTime, NewSession.Topic, NewSession.Venue, HttpContext.RequestAborted);

            TempData["Flash"] = $"Added the session on {lecture.SessionDate:d MMMM yyyy}.";
            TempData["FlashKind"] = "success";
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("NewSession.SessionDate", ex.Message);
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostOpenAsync(int lectureId, int minutes)
    {
        try
        {
            var code = await _attendance.OpenCheckInAsync(lectureId, minutes, HttpContext.RequestAborted);
            TempData["Flash"] = $"Check-in is open for {minutes} minutes. The code is {code}.";
            TempData["FlashKind"] = "success";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Flash"] = ex.Message;
            TempData["FlashKind"] = "error";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCloseAsync(int lectureId)
    {
        await _attendance.CloseCheckInAsync(lectureId, HttpContext.RequestAborted);
        TempData["Flash"] = "Check-in closed. Students can no longer sign in for that session.";
        TempData["FlashKind"] = "info";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;

        Sessions = (await _attendance.GetSessionsAsync(cancellationToken))
            .OrderByDescending(l => l.SessionDate)
            .ToList();

        OpenSession = await _attendance.GetOpenSessionAsync(cancellationToken);
        if (OpenSession?.CheckInClosesAtUtc is not null)
        {
            OpenCloseTime = OpenSession.CheckInClosesAtUtc.Value.ToLocalTime().ToString("HH:mm");
        }

        Enrolled = await _db.Students.CountAsync(cancellationToken);

        _recorded = await _db.AttendanceRecords.AsNoTracking()
            .GroupBy(r => r.LectureId)
            .Select(g => new { LectureId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LectureId, x => x.Count, cancellationToken);
    }
}
