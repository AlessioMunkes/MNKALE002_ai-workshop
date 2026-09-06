namespace AttendanceRegister.Models.ViewModels;

/// <summary>One captured session, with both running rates at that point.</summary>
public sealed record TrendPoint(
    DateOnly SessionDate,
    double StudentPercent,
    double ClassPercent,
    bool StudentCounted,
    int StudentAttended,
    int SessionsSoFar);

/// <summary>
/// A student's attendance as it has accumulated, against the class and against
/// the requirement.
///
/// The figure on the dashboard answers "where am I?". It cannot answer "which
/// way am I going?", and it cannot say whether a dip was personal or a session
/// most of the class missed. Both of those are what a student actually needs
/// before deciding whether to worry.
/// </summary>
public sealed class StudentTrend
{
    public List<TrendPoint> Points { get; init; } = new();
    public int MinimumPercent { get; init; } = 80;

    /// <summary>Two points is the minimum that can show a direction.</summary>
    public bool HasData => Points.Count > 1;

    public double FinalStudent => Points.Count == 0 ? 0 : Points[^1].StudentPercent;
    public double FinalClass => Points.Count == 0 ? 0 : Points[^1].ClassPercent;

    public bool BelowThreshold => FinalStudent < MinimumPercent;

    /// <summary>Percentage points above or below the class.</summary>
    public double AgainstClass => Math.Round(FinalStudent - FinalClass, 1);

    /// <summary>Movement over the last five captured sessions.</summary>
    public double RecentDrift =>
        Points.Count < 6 ? 0 : Math.Round(Points[^1].StudentPercent - Points[^6].StudentPercent, 1);
}
