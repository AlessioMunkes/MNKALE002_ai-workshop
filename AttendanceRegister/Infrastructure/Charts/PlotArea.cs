using System.Globalization;

namespace AttendanceRegister.Infrastructure.Charts;

/// <summary>
/// The arithmetic every chart in this application shares: turn an index into an
/// x, turn a percentage into a y, and round to something an SVG attribute can
/// hold without six decimal places of noise.
///
/// This exists because the first chart did its geometry inline in a page model.
/// The second would have copied it, and by the third the copies would have
/// disagreed about padding — the same drift that made AttendanceRules
/// necessary.
/// </summary>
public sealed class PlotArea
{
    public PlotArea(double width, double height, double left, double top, double right, double bottom)
    {
        Width = width;
        Height = height;
        Left = left;
        Top = top;
        Right = width - right;
        Bottom = height - bottom;
    }

    public double Width { get; }
    public double Height { get; }
    public double Left { get; }
    public double Top { get; }
    public double Right { get; }
    public double Bottom { get; }

    public double InnerWidth => Right - Left;
    public double InnerHeight => Bottom - Top;

    /// <summary>Evenly spaced x for item i of n, inset by half a band at each end.</summary>
    public double BandCentre(int index, int count)
    {
        if (count <= 1)
        {
            return Left + InnerWidth / 2;
        }

        var band = InnerWidth / count;
        return Left + band * (index + 0.5);
    }

    public double BandWidth(int count) => count <= 0 ? InnerWidth : InnerWidth / count;

    public double YForPercent(double percent) =>
        Top + (1 - Math.Clamp(percent, 0, 100) / 100.0) * InnerHeight;

    /// <summary>
    /// Two decimals is plenty for a viewBox coordinate and keeps the rendered
    /// markup readable. Invariant culture matters: a comma decimal separator
    /// would silently produce invalid SVG.
    /// </summary>
    public static string N(double value) =>
        Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
}
