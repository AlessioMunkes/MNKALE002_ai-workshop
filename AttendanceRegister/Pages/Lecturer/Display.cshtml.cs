using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages.Lecturer;

public class DisplayModel : PageModel
{
    private readonly IAttendanceService _attendance;
    private readonly IQrCodeService _qr;
    private readonly ICheckInCodeService _codes;
    private readonly CourseOptions _course;
    private readonly CheckInOptions _checkIn;

    public DisplayModel(IAttendanceService attendance, IQrCodeService qr, ICheckInCodeService codes,
        IOptions<CourseOptions> course, IOptions<CheckInOptions> checkIn)
    {
        _attendance = attendance;
        _qr = qr;
        _codes = codes;
        _course = course.Value;
        _checkIn = checkIn.Value;
    }

    [BindProperty(SupportsGet = true)]
    public int LectureId { get; set; }

    public CheckInDisplay Display { get; private set; } = new();
    public string SessionDate { get; private set; } = string.Empty;

    public string CourseCode => _course.Code;

    public async Task<IActionResult> OnGetAsync()
    {
        var lecture = await _attendance.GetSessionAsync(LectureId, HttpContext.RequestAborted);
        if (lecture is null)
        {
            return NotFound();
        }

        SessionDate = lecture.SessionDate.ToString("dddd d MMMM yyyy");
        Display = await BuildAsync(lecture, "projector");
        return Page();
    }

    /// <summary>
    /// Polled once per rotation by checkin-pulse.js. Returns the code and a
    /// freshly rendered QR, because the QR encodes the code and has to change
    /// with it — regenerating it in the browser would mean shipping a QR
    /// library to reproduce what the server already does.
    /// </summary>
    public async Task<IActionResult> OnGetPulseAsync()
    {
        var lecture = await _attendance.GetSessionAsync(LectureId, HttpContext.RequestAborted);
        if (lecture is null)
        {
            return NotFound();
        }

        var display = await BuildAsync(lecture, "projector");

        return new JsonResult(new
        {
            isOpen = display.IsOpen,
            code = display.Code,
            qrSvg = display.QrSvg,
            secondsRemaining = display.SecondsRemaining,
            closesAt = display.ClosesAt,
            presentCount = display.PresentCount,
            enrolled = display.Enrolled,
            checkedInPercent = display.CheckedInPercent
        });
    }

    private async Task<CheckInDisplay> BuildAsync(Lecture lecture, string variant)
    {
        var code = _codes.CurrentCode(lecture);
        if (code is null)
        {
            return new CheckInDisplay { IsOpen = false, Variant = variant, StepSeconds = _codes.StepSeconds };
        }

        var target = CheckInUrl.For(Url, Request, _checkIn.PublicBaseUrl, code);
        var progress = await _attendance.GetCheckInProgressAsync(lecture.Id, HttpContext.RequestAborted);

        return new CheckInDisplay
        {
            PresentCount = progress.Present,
            Enrolled = progress.Enrolled,
            IsOpen = true,
            Code = code,
            QrSvg = _qr.ToSvg(target, pixelsPerModule: 7),
            SecondsRemaining = _codes.SecondsRemaining(),
            StepSeconds = _codes.StepSeconds,
            PulseUrl = Url.Page("/Lecturer/Display", "Pulse", new { lectureId = lecture.Id }) ?? string.Empty,
            DisplayUrl = CheckInUrl.WithoutScheme(target),
            ClosesAt = lecture.CheckInClosesAtUtc?.ToLocalTime().ToString("HH:mm") ?? string.Empty,
            Variant = variant
        };
    }
}
