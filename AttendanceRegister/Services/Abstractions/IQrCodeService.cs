namespace AttendanceRegister.Services.Abstractions;

public interface IQrCodeService
{
    /// <summary>
    /// Renders the payload as an SVG fragment ready to inline into a page.
    /// Returns null when generation fails, so a caller can fall back to showing
    /// the text code alone rather than showing a broken image.
    /// </summary>
    string? ToSvg(string payload, int pixelsPerModule = 6);
}
