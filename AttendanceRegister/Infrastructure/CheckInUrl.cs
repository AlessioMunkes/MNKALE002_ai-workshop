using Microsoft.AspNetCore.Mvc;

namespace AttendanceRegister.Infrastructure;

/// <summary>
/// Builds the absolute address a QR code points at. In one place because two
/// screens need it and they must agree exactly — a QR that disagrees with the
/// URL printed beside it is worse than no QR at all.
/// </summary>
public static class CheckInUrl
{
    public static string For(IUrlHelper url, HttpRequest request, string? publicBaseUrl, string code)
    {
        var path = url.Page("/Student/CheckIn", new { code }) ?? "/Student/CheckIn/" + code;

        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return publicBaseUrl.TrimEnd('/') + path;
        }

        return $"{request.Scheme}://{request.Host}{path}";
    }

    /// <summary>Tidier to read off a projector than the full address.</summary>
    public static string WithoutScheme(string absoluteUrl) =>
        absoluteUrl.Replace("https://", string.Empty).Replace("http://", string.Empty);
}
