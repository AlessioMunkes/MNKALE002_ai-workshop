namespace AttendanceRegister.Models.ViewModels;

/// <summary>
/// Everything both screens need to show a rotating code: the sessions page in a
/// card, and the projector view at the size of a lecture theatre.
///
/// One shape for both, so the two can never disagree about which code is
/// current — the failure that would be hardest to diagnose from the back of a
/// room.
/// </summary>
public sealed class CheckInDisplay
{
    public bool IsOpen { get; init; }
    public string Code { get; init; } = string.Empty;
    public string? QrSvg { get; init; }

    public int SecondsRemaining { get; init; }
    public int StepSeconds { get; init; } = 30;

    /// <summary>Endpoint the page polls for the next code and the current count.</summary>
    public string PulseUrl { get; init; } = string.Empty;

    /// <summary>Address printed under the QR, for anyone typing it by hand.</summary>
    public string DisplayUrl { get; init; } = string.Empty;

    public string ClosesAt { get; init; } = string.Empty;

    /// <summary>"card" on the sessions screen, "projector" on the big display.</summary>
    public string Variant { get; init; } = "card";

    /// <summary>How many of the class are counted present for this session so far.</summary>
    public int PresentCount { get; init; }

    public int Enrolled { get; init; }

    public bool ShowsCounter => Enrolled > 0;

    public double Fraction => StepSeconds <= 0 ? 0 : (double)SecondsRemaining / StepSeconds;

    public double CheckedInPercent =>
        Enrolled == 0 ? 0 : Math.Round(PresentCount * 100.0 / Enrolled, 1);
}
