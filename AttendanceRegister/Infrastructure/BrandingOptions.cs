namespace AttendanceRegister.Infrastructure;

/// <summary>
/// Where the institutional mark lives and how the masthead should treat it.
///
/// The path is configuration rather than a literal in several .cshtml files, so
/// swapping an SVG for a PNG is one line of appsettings.json instead of a hunt
/// through the markup.
/// </summary>
public sealed class BrandingOptions
{
    public const string SectionName = "Branding";

    /// <summary>Application-relative path, e.g. "/img/uct-logo.png".</summary>
    public string LogoPath { get; set; } = "/img/uct-logo.svg";

    /// <summary>Alternative text. Not used where the mark is decorative.</summary>
    public string LogoAlt { get; set; } = "University of Cape Town";

    /// <summary>
    /// How the mark is placed on the dark masthead. Which one is right is a
    /// property of the artwork, not of the code, so it is configuration:
    ///
    ///   "chip"   — sits the logo on a small white rounded panel. The only
    ///              option that works for every file, including artwork with
    ///              an opaque background. The safe default.
    ///   "none"   — draws the file untouched. Correct when the mark is already
    ///              white or light on a transparent background.
    ///   "invert" — forces the artwork white. Correct only for dark, single
    ///              colour artwork on a transparent background. Applied to a
    ///              file with an opaque background it produces a white block,
    ///              because every pixel gets inverted, not just the mark.
    /// </summary>
    public string MastheadTreatment { get; set; } = "chip";

    public string MastheadModifier => MastheadTreatment?.Trim().ToLowerInvariant() switch
    {
        "invert" => "logo--invert",
        "none" => string.Empty,
        _ => "logo--chip"
    };
}
