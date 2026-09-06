using System.Globalization;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages.Lecturer;

public class IndexModel : PageModel
{
    private readonly IAnalyticsService _analytics;

    public IndexModel(IAnalyticsService analytics) => _analytics = analytics;

    // Chart geometry. Rendering the SVG on the server keeps the page working
    // without JavaScript and makes it print cleanly.
    private const double Width = 900;
    private const double Height = 280;
    private const double PadLeft = 46;
    private const double PadRight = 14;
    private const double PadTop = 18;
    private const double PadBottom = 54;

    public double ChartWidth => Width;
    public double ChartHeight => Height;
    public double PlotLeft => PadLeft;
    public double PlotRight => Width - PadRight;
    public double LabelY => Height - PadBottom + 22;
    public double ThresholdY { get; private set; }

    public CourseOverview Overview { get; private set; } = new();
    public List<Bar> Bars { get; private set; } = new();
    public List<GridLine> GridLines { get; private set; } = new();
    public List<Tick> Ticks { get; private set; } = new();

    /// <summary>Built from the same per-session figures the bars use, so the two cannot disagree.</summary>
    public CumulativeChart Cumulative { get; private set; } = new();

    public string AverageText => Overview.AverageAttendance.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Direction of travel over the last five sessions, or nothing when it is flat.</summary>
    public string DriftText
    {
        get
        {
            var drift = Cumulative.RecentDrift;
            if (Math.Abs(drift) < 0.1) { return string.Empty; }
            var direction = drift > 0 ? "up" : "down";
            return $", {direction} {Math.Abs(drift).ToString("0.#", CultureInfo.InvariantCulture)} points over the last five sessions";
        }
    }

    /// <summary>Pre-built draft addressed to everyone under the requirement.</summary>
    public string BulkEmailHref { get; private set; } = string.Empty;

    public bool BulkEmailTruncated { get; private set; }

    public sealed record Bar(double X, double Y, double Width, double Height, string Tip, bool BelowThreshold, int LectureId, string Url);
    public sealed record GridLine(double Y, string Label);
    public sealed record Tick(double X, string Label);

    public async Task OnGetAsync()
    {
        Overview = await _analytics.GetOverviewAsync(HttpContext.RequestAborted);
        Cumulative = CumulativeChart.From(Overview.LectureStats, Overview.MinimumPercent);

        if (Overview.AtRiskStudents.Count > 0)
        {
            var message = AttendanceMessages.ForGroupBelowThreshold(
                Overview.CourseCode, Overview.MinimumPercent, Overview.AtRiskStudents.Count);

            BulkEmailHref = MailTo.Bulk(Overview.AtRiskStudents.Select(s => s.Email),
                message.Subject, message.Body);

            BulkEmailTruncated = Overview.AtRiskStudents.Count > MailTo.MaxBulkRecipients;
        }

        var plotHeight = Height - PadTop - PadBottom;
        var plotWidth = Width - PadLeft - PadRight;

        double YFor(double percent) => PadTop + (1 - Math.Clamp(percent, 0, 100) / 100.0) * plotHeight;

        ThresholdY = YFor(Overview.MinimumPercent);

        for (var percent = 0; percent <= 100; percent += 25)
        {
            GridLines.Add(new GridLine(Math.Round(YFor(percent), 2), percent + "%"));
        }

        var stats = Overview.LectureStats;
        if (stats.Count == 0)
        {
            return;
        }

        var slot = plotWidth / stats.Count;
        var barWidth = Math.Max(3, Math.Min(34, slot * 0.62));

        // Thin the date labels so they never overlap on a busy semester.
        var labelEvery = (int)Math.Ceiling(stats.Count / 14.0);

        for (var i = 0; i < stats.Count; i++)
        {
            var stat = stats[i];
            var centre = PadLeft + slot * (i + 0.5);
            var y = YFor(stat.Percentage);

            Bars.Add(new Bar(
                Math.Round(centre - barWidth / 2, 2),
                Math.Round(y, 2),
                Math.Round(barWidth, 2),
                Math.Round(PadTop + plotHeight - y, 2),
                $"{stat.SessionDate:d MMM yyyy}: {stat.PresentCount} of {stat.Enrolled} ({stat.Percentage:0.#}%) — open this session",
                stat.Percentage < Overview.MinimumPercent,
                stat.LectureId,
                Url.Page("/Lecturer/Session", new { lectureId = stat.LectureId }) ?? string.Empty));

            if (i % labelEvery == 0)
            {
                Ticks.Add(new Tick(Math.Round(centre, 2), stat.SessionDate.ToString("d MMM")));
            }
        }
    }
}
