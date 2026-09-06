namespace AttendanceRegister.Infrastructure;

public sealed class CheckInOptions
{
    public const string SectionName = "CheckIn";

    /// <summary>
    /// Absolute base address to encode in the QR code, e.g. "http://10.0.0.12:5000".
    ///
    /// This exists because of a specific trap: run the site normally and the QR
    /// encodes http://localhost:5000, which resolves on a phone to the phone
    /// itself. Every scan fails, and nothing about the failure points at the
    /// cause. Set this to the lecturer machine's address on the venue network
    /// and the QR becomes scannable. Left empty, the request's own address is
    /// used, which is correct once the app is deployed on a real host.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Default minutes a check-in window stays open.</summary>
    public int DefaultWindowMinutes { get; set; } = 15;
}
