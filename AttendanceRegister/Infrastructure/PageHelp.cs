namespace AttendanceRegister.Infrastructure;

public sealed record PageHelp(string Title, string[] Paragraphs);

/// <summary>
/// What the "?" button says on each screen, in one place.
///
/// The alternative was a block of help text repeated in every .cshtml, which
/// would have been fifteen copies of the same idea to keep in step. The layout
/// looks the current page up here by its route, so a page gets a help button by
/// gaining an entry — no markup change at all.
/// </summary>
public static class PageHelpCatalogue
{
    private static readonly Dictionary<string, PageHelp> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/Index"] = new("Welcome", new[]
        {
            "This is the attendance register for INF3003W. It replaces the printed class list, the tutor's spreadsheet, and the manual update afterwards.",
            "Sign in to see your own attendance, or, if you run the course, the whole class."
        }),

        ["/Account/Login"] = new("Signing in", new[]
        {
            "Students sign in with a student number. Staff sign in with an email address. Either works in the same box.",
            "What you see afterwards depends on which kind of account you used — the student and lecturer areas are kept separate."
        }),

        ["/Help"] = new("Help", new[]
        {
            "A full tour of every screen, the file format the upload expects, and what to do when something looks wrong.",
            "The short version of any single page is on its own \u201c?\u201d button."
        }),

        ["/Student/Index"] = new("Your dashboard", new[]
        {
            "Your attendance percentage, measured only against sessions that have actually been captured, so a session nobody recorded cannot count against you.",
            "When your lecturer opens check-in, a box appears here. Type the six-character code from the screen \u2014 it changes every 30 seconds, so read the one showing now.",
            "The strip is one square per session: filled means you were counted, hollow means you were not, hatched means nobody captured it yet."
        }),

        ["/Student/CheckIn"] = new("Check in", new[]
        {
            "You reached this by scanning the code on the lecture screen. The session and the code are filled in already \u2014 confirm and you are marked present.",
            "Nothing is recorded until you press the button. Scanning on its own, or a link preview opening the page, cannot mark you present."
        }),

        ["/Student/History"] = new("Your attendance", new[]
        {
            "Every session so far, newest first, and how each record was captured — your own check-in, a spreadsheet upload, or your lecturer.",
            "Search or filter to narrow the list, click a heading to sort, and use Query this on any row that looks wrong."
        }),

        ["/Student/Queries"] = new("Queries", new[]
        {
            "Raise a query when a session is recorded wrongly. Say which session, what it should say, and what actually happened.",
            "You can have one open query per session. The outcome and your lecturer's note appear on this page once it is resolved."
        }),

        ["/Lecturer/Index"] = new("Overview", new[]
        {
            "Attendance for each captured session, with the DP requirement drawn across the chart as a dashed line.",
            "Below that, every student currently under the requirement, worst first, with how many sessions each would need to recover."
        }),

        ["/Lecturer/Students"] = new("Class list", new[]
        {
            "One row per student: their attendance pattern, rate, standing against the requirement, and how much margin they have left.",
            "Click any row for that student's full history. Add a student here when someone joins the course late.",
            "Filter Standing to see only the students below the requirement."
        }),

        ["/Lecturer/Session"] = new("Session overview", new[]
        {
            "What happened at one lecture: how many were counted, how each record was captured, and how the session compares with the rest of the term.",
            "The register screen is for changing attendance. This one is for reading it \u2014 use Mark register when something needs correcting.",
            "Everyone not counted present is listed separately, weakest overall attendance first, with a link that drafts an email to all of them."
        }),

        ["/Lecturer/Heatmap"] = new("Class heatmap", new[]
        {
            "The whole register as one picture: a row per student, a column per session, ordered with the strongest attendance at the top.",
            "A pale column is a session most of the class missed \u2014 worth knowing whether something clashed that day. The pale band along the bottom is the group below the requirement.",
            "Click any row to open that student."
        }),

        ["/Lecturer/StudentDetail"] = new("Student detail", new[]
        {
            "Everything recorded for one student: each session, how it was captured, when, and by whom, plus every query they have raised.",
            "Edit details corrects a name, number or address. Deactivating stops the student signing in but keeps their attendance record."
        }),

        ["/Lecturer/StudentNew"] = new("Add a student", new[]
        {
            "Use this when a student joins after the class list was uploaded. The email address is suggested from the student number, and you can change it.",
            "Set a starting password and pass it to the student directly. There is no self-registration and no password reset by email."
        }),

        ["/Lecturer/Sessions"] = new("Sessions", new[]
        {
            "Add a session for a lecture, then open check-in. Project the code with the Display button: students either type it or scan the QR code with their phone camera.",
            "The code changes every 30 seconds and the QR changes with it, so a code passed to somebody off campus is stale before they can use it.",
            "Only one session can accept check-ins at a time. Opening a second one closes the first.",
            "Mark register opens that session so you can set each student by hand."
        }),

        ["/Lecturer/Register"] = new("Mark the register", new[]
        {
            "Set each student's status for this one session and save the lot together. Every change records who made it and when.",
            "Searching or filtering only hides rows from view — saving still writes every student, not just the ones on screen."
        }),

        ["/Lecturer/Import"] = new("Upload a register", new[]
        {
            "Brings in attendance for past lectures from a spreadsheet: one row per student, one column per session date.",
            "Check the file first. It reports exactly what would change without writing anything. Uploading the same file twice is safe.",
            "An unreadable cell is reported by row and date and skipped on its own. A duplicated student number stops the whole file."
        }),

        ["/Lecturer/Queries"] = new("Queries", new[]
        {
            "Approving a query rewrites the register to what the student asked for and notes that it came from a query. Rejecting leaves the register alone.",
            "Edit lets you correct a query, reopen one you resolved too quickly, or delete one raised in error.",
            "Record a query here when a student raises it by email rather than through the system."
        }),

        ["/Lecturer/QueryEdit"] = new("Edit a query", new[]
        {
            "Correct what a query says, resolve it, or remove it.",
            "Reopening deliberately leaves the register untouched: undoing an approval is a separate decision, made on the register screen where the change is visible."
        })
    };

    public static PageHelp? For(string? page) =>
        page is not null && Entries.TryGetValue(page, out var help) ? help : null;
}
