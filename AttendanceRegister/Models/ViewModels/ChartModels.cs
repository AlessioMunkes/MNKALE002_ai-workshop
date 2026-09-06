using AttendanceRegister.Infrastructure.Charts;
using AttendanceRegister.Models.Entities;

namespace AttendanceRegister.Models.ViewModels;

/// <summary>One point on the cumulative line.</summary>
public sealed record CumulativePoint(
    DateOnly SessionDate,
    double SessionPercent,
    double CumulativePercent,
    int AttendedToDate,
    int PossibleToDate);

/// <summary>
/// Attendance for the course as it has accumulated, session by session.
///
/// The bar chart answers "how was that lecture?". This answers "which way is
/// the class going?" — a run of poor sessions bends the line even when no
/// single bar looks alarming.
/// </summary>
public sealed class CumulativeChart
{
    public List<CumulativePoint> Points { get; init; } = new();
    public int MinimumPercent { get; init; } = 80;

    public bool HasData => Points.Count > 1;
    public double Final => Points.Count == 0 ? 0 : Points[^1].CumulativePercent;
    public bool EndsBelowThreshold => Final < MinimumPercent;

    /// <summary>Change over the last five sessions, in percentage points.</summary>
    public double RecentDrift
    {
        get
        {
            if (Points.Count < 6) return 0;
            return Math.Round(Points[^1].CumulativePercent - Points[^6].CumulativePercent, 1);
        }
    }

    public static CumulativeChart From(IEnumerable<LectureAttendanceStat> stats, int minimumPercent)
    {
        var points = new List<CumulativePoint>();
        var attended = 0;
        var possible = 0;

        foreach (var stat in stats)
        {
            attended += stat.PresentCount;
            possible += stat.Enrolled;

            points.Add(new CumulativePoint(
                stat.SessionDate,
                stat.Percentage,
                possible == 0 ? 0 : Math.Round(attended * 100.0 / possible, 1),
                attended,
                possible));
        }

        return new CumulativeChart { Points = points, MinimumPercent = minimumPercent };
    }
}

/// <summary>One student's row in the class heatmap.</summary>
public sealed record HeatmapRow(int StudentId, string StudentNumber, string DisplayName,
    double Percentage, IReadOnlyList<SessionCell> Cells);

/// <summary>
/// The whole register as one picture: a row per student, a column per session.
///
/// Rows are ordered by rate, best at the top, so the students in trouble form a
/// band along the bottom rather than being scattered. A pale column is a
/// session the whole class missed, which no per-student view would show.
/// </summary>
public sealed class ClassHeatmap
{
    public List<DateOnly> SessionDates { get; init; } = new();
    public List<HeatmapRow> Rows { get; init; } = new();
    public int MinimumPercent { get; init; } = 80;

    public bool HasData => Rows.Count > 0 && SessionDates.Count > 0;

    /// <summary>Index of the first row that falls below the requirement, or -1.</summary>
    public int FirstRowBelowThreshold =>
        Rows.FindIndex(r => r.Percentage < MinimumPercent);

    public static ClassHeatmap From(IEnumerable<ClassListRow> classList, int minimumPercent)
    {
        var rows = classList.ToList();

        var dates = rows
            .SelectMany(r => r.Summary.Cells)
            .Select(c => c.SessionDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return new ClassHeatmap
        {
            SessionDates = dates,
            MinimumPercent = minimumPercent,
            Rows = rows
                .OrderByDescending(r => r.Summary.Percentage)
                .ThenBy(r => r.Summary.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(r => new HeatmapRow(
                    r.Summary.StudentId,
                    r.Summary.StudentNumber,
                    r.Summary.DisplayName,
                    r.Summary.Percentage,
                    r.Summary.Cells))
                .ToList()
        };
    }
}

/// <summary>
/// A student's running attendance rate as a tiny path, for the class list.
///
/// Plotting the running rate rather than each session keeps the line readable
/// at twenty pixels tall: individual sessions would be a barcode, whereas the
/// running rate has a direction you can read at a glance.
/// </summary>
public static class Sparkline
{
    public static string Path(IReadOnlyList<SessionCell> cells, double width, double height)
    {
        var recorded = cells.Where(c => c.IsRecorded).ToList();
        if (recorded.Count < 2)
        {
            return string.Empty;
        }

        var attended = 0;
        var segments = new List<string>(recorded.Count);

        for (var i = 0; i < recorded.Count; i++)
        {
            if (recorded[i].IsCounted)
            {
                attended++;
            }

            var rate = attended * 100.0 / (i + 1);
            var x = recorded.Count == 1 ? 0 : width * i / (recorded.Count - 1);
            var y = height - (Math.Clamp(rate, 0, 100) / 100.0 * height);

            segments.Add($"{(i == 0 ? "M" : "L")}{PlotArea.N(x)} {PlotArea.N(y)}");
        }

        return string.Join(" ", segments);
    }

    /// <summary>Running rate at the final recorded session, for the tooltip.</summary>
    public static double FinalRate(IReadOnlyList<SessionCell> cells)
    {
        var recorded = cells.Where(c => c.IsRecorded).ToList();
        if (recorded.Count == 0) return 0;
        return Math.Round(recorded.Count(c => c.IsCounted) * 100.0 / recorded.Count, 1);
    }
}
