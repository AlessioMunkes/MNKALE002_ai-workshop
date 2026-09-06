using AttendanceRegister.Services.Abstractions;
using QRCoder;

namespace AttendanceRegister.Services;

/// <summary>
/// Wraps QRCoder behind an interface so pages depend on "give me a QR" rather
/// than on a particular library, and so a generation failure degrades to the
/// text code instead of taking the page down.
/// </summary>
public sealed class QrCodeService : IQrCodeService
{
    private readonly ILogger<QrCodeService> _logger;

    public QrCodeService(ILogger<QrCodeService> logger) => _logger = logger;

    public string? ToSvg(string payload, int pixelsPerModule = 6)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var generator = new QRCodeGenerator();

            // Q corrects around a quarter of the symbol. A projected code gets
            // read at an angle, from the back of a lecture theatre, sometimes
            // across a lens flare — worth the extra modules.
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);

            return new SvgQRCode(data).GetGraphic(pixelsPerModule);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not generate a QR code. The text code will be shown on its own.");
            return null;
        }
    }
}
