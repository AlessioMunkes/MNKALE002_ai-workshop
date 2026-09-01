# Design decisions

A record of the choices made while building the attendance register, and what
each one traded away. Read this alongside `docs/AI-PROMPT-LOG.md`.

---

## 1. SQLite with EF Core, created at startup

**Chosen because** the system has to run on a marker's machine with nothing
installed but the .NET SDK. SQLite is a single file with no server, and
`EnsureCreated()` avoids needing the `dotnet-ef` tool to apply migrations.

**Trade-off** `EnsureCreated()` cannot evolve a schema. The moment this system
goes past a demonstration it needs real migrations, at which point the existing
database has to be dropped and rebuilt once.

**Alternative rejected** SQL Server LocalDB — Windows-only, and a marker on
macOS could not run the project at all.

---

## 2. Table-per-hierarchy for users

`User` is abstract; `Student` and `Lecturer` inherit from it and share one
`Users` table with a discriminator column.

**Chosen because** the two roles differ by one field each. Splitting them into
separate tables would mean a union query on every sign-in for no benefit.
Making `Role` an abstract property derived from the concrete type means the role
can never disagree with what the object actually is — there is no settable
`Role` field to get out of step.

**Trade-off** `StudentNumber` is nullable at the database level because lecturer
rows do not have one. The unique index is filtered to compensate.

---

## 3. Rich entities, not anemic ones

`Lecture` owns `OpenCheckIn`, `CloseCheckIn` and `CodeMatches`.
`AttendanceRecord` owns `Revise`. `AttendanceQuery` owns `Approve` and `Reject`
and refuses to be resolved twice.

**Chosen because** these are the rules most likely to be duplicated
inconsistently if each page implemented them. `Revise` returning `false` when
nothing actually changed is what lets the importer report "already correct"
separately from "changed" without a second comparison pass.

**Trade-off** Entities need private parameterless constructors for EF, and
setters have to be private, which is more ceremony than public properties.

---

## 4. Cookie authentication with hand-rolled PBKDF2, not ASP.NET Identity

**Chosen because** Identity brings a dozen tables, a UI scaffold and a
migration story for features this system does not have — no self-registration,
no email confirmation, no external providers, no two-factor. PBKDF2-SHA256 with
a per-password salt at 120 000 iterations is the same primitive Identity uses.
The iteration count is stored in the hash so it can be raised later without
invalidating existing rows.

**Trade-off** No lockout after repeated failures and no password reset flow.
Both would be needed before this system held real marks.

---

## 5. Parser strategy plus factory for uploads

`IAttendanceFileParser` has one implementation per format. A factory picks one
by extension, and `AttendanceImportService` only ever sees an `AttendanceSheet`.

**Chosen because** the brief says Excel but the file actually supplied was a
semicolon-delimited CSV. Rather than guess, both are supported, and adding a
third format is a new class rather than an edit to the import logic.

---

## 6. Fixed query count for imports regardless of class size

The import loads the student index, the lecture index and the existing records
in three queries, then makes every decision in memory against dictionaries.
Change detection is switched off around the bulk loop and inserts go in through
`AddRange` with one `SaveChanges` per phase.

**Chosen because** the naive version — look up the student, look up the lecture,
look up the record, save — is four round trips per cell. On the supplied file
that is over ten thousand queries for one upload. This version is four.

**Trade-off** Two intermediate `SaveChanges` calls are still needed to get
identity values for new students and sessions. The whole thing runs inside one
transaction, so a failure leaves nothing behind.

---

## 7. Empty cells are left alone, not treated as absent

A blank cell means "no information", not "absent". Sessions nobody has captured
are excluded from the denominator when a percentage is worked out.

**Chosen because** the alternative silently penalises students for a tutor's
missing paperwork. A student cannot lose DP because a session was never
captured.

---

## 8. Per-cell errors do not fail the file

An unreadable cell is reported with its row number and session date and skipped.
A duplicate student number stops the whole import before anything is written.

**Chosen because** those are different kinds of problem. A typo in one cell
should not force a lecturer to re-upload a hundred rows. A duplicate row means
the file itself is ambiguous, and importing half of it would be worse than
importing none.

---

## 9. Timed check-in codes rather than a button

Only one session accepts check-ins at a time, and opening a second closes the
first. Codes are six characters from an alphabet that excludes `O I L 0 1`.

**Chosen because** a plain "I am here" button is signed as easily from a
residence as from the lecture theatre. A code that is only on the projector for
fifteen minutes is not a hard control, but it is a real improvement on a passed-
around paper sheet, and it needs no hardware.

**Trade-off** A student can still read the code to a friend. Proper defence
needs venue Wi-Fi or a rotating QR code, which is out of scope for one week.

---

## 10. Server-rendered SVG for the chart, no JavaScript library

**Chosen because** the chart is a static picture of stored data. Rendering it
server-side means it works with scripts blocked, prints correctly, needs no CDN,
and cannot break because a version pinned in a `<script>` tag moved.

**Trade-off** No zoom, no click-through. The `<title>` on each bar gives the
figures on hover, which covers the actual need.

---

## 11. No client-side validation scripts

Validation is server-side only, surfaced through `asp-validation-for` and a
shared summary partial.

**Chosen because** client-side validation is a convenience, not a control —
every rule has to exist on the server regardless. Skipping jQuery removes three
dependencies from the page.

**Trade-off** Errors cost a round trip. On forms this small that is not felt.

---

## 12. Every attendance record carries its source

`AttendanceSource` distinguishes a student check-in, a spreadsheet upload, a
lecturer edit and a query outcome, and each record stores who wrote it.

**Chosen because** the first question anyone asks about a disputed record is
"where did this come from?" Storing it makes that answerable without a
separate audit table.

---

## 13. Client-side table tooling, as enhancement rather than dependency

Search, filters and column visibility are handled by one 200-line vanilla
JavaScript file that attaches itself to any `<table data-table-tools>`. Which
columns get a filter dropdown, and which cannot be hidden, is declared in the
markup with `data-filter` and `data-lock`.

**Chosen because** the alternative — a query-string round trip per keystroke —
is slower than filtering a hundred rows in the browser and would need paging,
sort state and filter state carried through every form post on the page. Doing
it in markup attributes rather than per-page JavaScript means a new table gets
the toolbar by adding one attribute, and there is no second place to update
when a column changes.

**This amends decision 11.** That decision was about *validation*, and it still
holds: no rule is enforced in the browser that is not also enforced on the
server. Filtering a table changes nothing and validates nothing, so the same
reasoning does not apply. With scripting off, every table still renders in full
— you lose the toolbar, not the data.

**Trade-off** Filtering only covers rows already on the page. If this course
ever had thousands of students the class list would need server-side paging,
and the toolbar would have to move to the server with it.

**One trap worth noting.** The register screen has a `<select>` in every row.
The search index is built from cell text, so those cells would have contributed
every option label — "PresentAbsentLateExcused" — to every row, and a search
for "late" would have matched all of them. Those cells carry an explicit empty
`data-value` to keep them out of the index. Hidden rows still submit their
inputs, so filtering the register never drops a student from a save.

---

## 14. Sorting keys live in the markup, not in a date parser

Clicking a heading sorts the table. First click is highest to lowest, second
reverses it, third restores the order the server sent. Columns that cannot be
meaningfully ordered — the attendance strip, the action links, the register's
status dropdown — carry `data-nosort` and get no button.

**The problem** was that dates render as "Mon 9 Mar 2026" for readability, and
that string sorts alphabetically into nonsense: April before January, and 9
March before 10 February. The obvious fix is to parse the display string back
into a date in JavaScript, which means teaching the browser a format the server
chose, and getting it wrong the moment anyone changes the format string.

**Chosen instead** each cell carries a `data-sort` attribute holding a key that
already sorts correctly as plain text — `yyyy-MM-dd` for dates, a bare number
for counts. Razor writes the key at the same time it writes the display text,
so the two cannot drift apart. The JavaScript never parses a date.

A column is treated as numeric only when *every* value in it is numeric, so one
dash in a column of numbers falls back to text order rather than silently
producing zeros.

**Trade-off** Two representations of the same value in the markup, which is
duplication. It is duplication the compiler can see, though: both come from one
expression a few lines apart in the same Razor loop.

**One deliberate detail.** The Margin column sorts on a signed number —
positive for sessions of slack, negative for sessions needed to recover — so
one click puts the students in the most trouble at the bottom and the safest at
the top, in a single ordering. The displayed text stays readable ("3 to spare",
"2 to recover") because only the sort key is signed.

---

## 15. One vocabulary for attendance

`AttendanceRules` now owns which statuses count and how statuses and sources
are named. Five places used to answer those questions for themselves: two view
models and three page models, each with its own copy of the same switch.

**Why it mattered** the copies had already drifted. The same record read
"Your check-in" on the student's history and "Student check-in" on the
lecturer's register — the same fact, two names, because nothing forced them to
agree. That is the failure mode duplication actually causes: not extra lines,
but two answers to one question.

**One wrinkle** the counted statuses are exposed as an array as well as a
predicate. EF Core can translate `Counted.Contains(r.Status)` into a SQL `IN`
clause but cannot translate a call to `Counts()`, so the database queries use
the array and everything in memory uses the method. Both read from the same
list, so they cannot disagree.

The student email domain moved out of the importer into `appsettings.json` at
the same time, and the lecturer's staff number out of the seeder. Neither
belonged in code.

---

## 16. Queries are editable; approvals are not silently reversible

A lecturer can now correct a query, reopen one resolved too quickly, and delete
one raised in error, as well as record a query a student sent by email.

**Reopening deliberately does not touch the register.** An approved query has
already rewritten a record, and that record carries its own source and
timestamp. Silently reversing it on reopen would undo a change the lecturer can
no longer see, so reopening moves the query back to the queue and says plainly
that the register was left alone. Undoing the change itself is done on the
register screen, where it is visible.

Deleting a query works the same way, for the same reason: the query goes, the
register change stays.

---

## 17. Students are deactivated, never deleted

A lecturer can add a student, correct their number, name or address, reset
their password, and deactivate them. There is no delete.

**Chosen because** `AttendanceRecord` cascades from `Student`. Deleting one row
would take that student's entire attendance history with it, and attendance
that has been captured is part of the course record — it is exactly what a DP
dispute would need months later. Deactivating stops the sign-in and keeps
everything else.

**Note on the number being editable.** Student numbers are close to identity
here, and letting one change is a little uncomfortable. The alternative is
worse: a mistyped number could only be fixed by deleting the account and losing
its attendance. The unique index still prevents two students sharing one.

---

## 18. Type, gradient and motion

Montserrat carries anything that has to assert itself — titles, figures,
buttons, column headings — and Lato does the reading. Both are loaded from
Google Fonts with a full system-font fallback stack behind them, so losing the
network costs the typeface and nothing else.

**This amends decision 10's reasoning, not its conclusion.** That decision
avoided a CDN for the *chart*, because a broken script tag would have left no
chart at all. A font that fails to load leaves the page entirely readable in
the fallback face. The risk is not the same, so the answer is not either.

Every gradient is built from the same two brand stops declared once as CSS
custom properties, so there is no second, slightly different blue anywhere.
Motion is decoration only: everything it signals is also carried by layout,
colour and text, which is why `prefers-reduced-motion` can switch all of it off
without loss.

---

## 19. Page help lives in a catalogue, not in the markup

Every screen has a "?" in the masthead that opens a short explanation of what
the page is for. The text lives in `PageHelpCatalogue`, keyed by route.

**Chosen because** the alternative was the same block of markup repeated in
fifteen `.cshtml` files — fifteen copies of one idea to keep in step, which is
decision 15's problem all over again. The layout looks the current route up in
the catalogue, so a page gains a help button by gaining an entry and needs no
markup change at all.

The dialog is the native `<dialog>` element, so focus trapping, Escape to
close, and the backdrop come from the browser rather than from code here. The
supporting script is fourteen lines.

---

## 20. Contacting students uses mailto rather than an SMTP client

The class list and the at-risk list both offer an Email link per student, and a
single link that drafts to everyone below the requirement. Each opens the
lecturer's own mail client with the subject and body already written, filled in
with that student's rate, sessions attended and how far off the line they are.

**Chosen because** sending mail from the application would mean SMTP
credentials in configuration, a retry path for failures, a bounce policy, and
somebody owning deliverability — a substantial amount of infrastructure for a
feature whose whole job is to save typing. Handing the lecturer a pre-filled
draft costs none of that, and the message ends up in their own sent items,
under their own address, so a reply from the student actually reaches them.

**The group draft puts everyone in BCC.** Putting the at-risk list in the To
field would publish, to every student on it, exactly who else is failing to
meet the requirement.

**Trade-off** No record in the system that the mail was sent, and no send from
a server-side scheduled job. If the department later wants "email everyone
below 80% every Friday", that needs a real mail service and this becomes the
wrong shape.

**One limit worth knowing.** Mail clients and browsers both stop honouring very
long links, so the group draft carries at most sixty addresses and the page
says so when the list is longer.

---

## 21. The logo is configuration, not five copies of a path

`BrandingOptions` holds the path, the alternative text, and whether the mark
needs forcing white on the masthead. One `_Logo` partial renders it in all four
places.

**Chosen because** the obvious version — an `<img>` tag in each of the layout,
footer, landing page and sign-in card — makes swapping an SVG for a PNG a
four-file edit, and guarantees that one of them eventually gets missed. It is
decision 15's problem in a different costume.

The invert flag exists because the masthead is dark. A dark logo has to be
forced white to sit on it, but a mark that is already white, or one whose
colour is the point, must be left alone — and which of those you have is a
property of the file, not of the code.

---

## 22. Reveal on scroll, without ever hiding content that cannot come back

Blocks fade up as they enter the viewport. The blocks are simply the direct
children of `<main>`, so no page opts in and there is no selector list to keep
in step with the markup.

**The failure mode this had to avoid** is content that stays invisible because
the reveal never fired — a script that failed to load, an observer that never
triggered, a browser without `IntersectionObserver`. Three things guard it: the
hiding rule is scoped to `html.has-js`, set by an inline script that only runs
if scripting works at all; browsers without `IntersectionObserver` and anyone
who has asked for reduced motion get everything revealed immediately; and a
timeout reveals every block after two and a half seconds regardless.

The inline script sits in `<head>` rather than with the other scripts on
purpose. Set any later and the browser paints the blocks, then hides them, then
fades them back in — a visible flicker on every page load.

Page-to-page transitions use the CSS `@view-transition` at-rule. Chromium
honours it; every other browser ignores the at-rule and navigates as it always
did, so there is no fallback to write.

---

## 23. The masthead logo has three treatments, not a boolean

`BrandingOptions.MastheadTreatment` takes "chip", "none" or "invert", and
defaults to "chip".

**What went wrong with the boolean.** The first version had
`InvertOnDarkBackground`, applying `filter: brightness(0) invert(1)` to force
dark artwork white against the blue bar. That works only for single-colour
artwork on a transparent background. Given a PNG with an opaque white
background it inverts every pixel, background included, and renders a solid
white rectangle — which is exactly what happened. The filter had no way to tell
the mark from the paper behind it.

**Why three options rather than a smarter filter.** No CSS filter can separate
artwork from its background; the information simply is not there. Which
treatment is correct is a property of the file, so it belongs in configuration
where whoever supplies the file can state it. "chip" — the logo on a small
white panel — is the default because it is the only one that is correct for
every file, including artwork nobody inspected first.

---

## 24. The password toggle is created by script, not rendered by the server

Each password field is wrapped in a `[data-password-toggle]` element. The
Show/Hide button does not exist in the markup; `password-toggle.js` builds it.

**Chosen because** a server-rendered button would sit there inert with
scripting off, and the input's right-hand padding would leave a gap for a
control that does nothing. Building it in script means the field is either a
plain password box or a working toggle, never something in between. The padding
comes from a `has-toggle` class the script adds at the same time.

The button is `type="button"`. Left as the default `submit`, revealing a
password inside a form would submit it.
