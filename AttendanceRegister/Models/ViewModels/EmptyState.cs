namespace AttendanceRegister.Models.ViewModels;

/// <summary>
/// A screen with nothing on it yet.
///
/// A good empty state answers three questions: what belongs here, why it is
/// not here, and what would put it here. A grey box with one sentence answers
/// the first two at best, and a fresh install is often the first thing anyone
/// sees of this system.
/// </summary>
public sealed class EmptyState
{
    /// <summary>"register", "sessions", "queries" or "students".</summary>
    public string Glyph { get; init; } = "register";

    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public string? ActionText { get; init; }
    public string? ActionPage { get; init; }

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionText) && !string.IsNullOrWhiteSpace(ActionPage);
}
