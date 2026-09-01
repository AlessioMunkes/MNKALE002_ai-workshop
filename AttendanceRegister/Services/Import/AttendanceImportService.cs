using AttendanceRegister.Data;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.Import;
using AttendanceRegister.Repositories;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Services.Import;

/// <summary>
/// Applies a parsed register to the database.
///
/// The whole upload costs a fixed number of round trips regardless of class
/// size: one query for the student index, one for the lecture index, one for
/// the existing records, and one SaveChanges per phase. Everything in between
/// is dictionary lookups, so a 104 x 26 sheet is 2 704 decisions with no
/// per-cell database traffic.
/// </summary>
public sealed class AttendanceImportService : IAttendanceImportService
{
    /// <summary>Reporting cap so one badly formatted file cannot flood the page.</summary>
    private const int MaxReportedIssues = 200;

    private readonly AttendanceDbContext _db;
    private readonly IStudentRepository _students;
    private readonly ILectureRepository _lectures;
    private readonly IAttendanceRepository _attendance;
    private readonly IAttendanceFileParserFactory _parserFactory;
    private readonly CourseOptions _course;
    private readonly ILogger<AttendanceImportService> _logger;

    public AttendanceImportService(
        AttendanceDbContext db,
        IStudentRepository students,
        ILectureRepository lectures,
        IAttendanceRepository attendance,
        IAttendanceFileParserFactory parserFactory,
        IOptions<CourseOptions> course,
        ILogger<AttendanceImportService> logger)
    {
        _db = db;
        _students = students;
        _lectures = lectures;
        _attendance = attendance;
        _parserFactory = parserFactory;
        _course = course.Value;
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => _parserFactory.SupportedExtensions;

    public async Task<AttendanceImportResult> ImportAsync(AttendanceImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var parser = _parserFactory.Resolve(request.FileName);
        if (parser is null)
        {
            return AttendanceImportResult.Failed(request.FileName,
                $"'{Path.GetExtension(request.FileName)}' files are not supported. Upload one of: {string.Join(", ", SupportedExtensions)}.");
        }

        var course = await _db.Courses.FirstOrDefaultAsync(cancellationToken);
        if (course is null)
        {
            return AttendanceImportResult.Failed(request.FileName,
                "No course is configured yet, so there is nothing to attach these sessions to.");
        }

        // ClosedXML needs to seek; buffering once keeps both parsers happy.
        await using var buffered = new MemoryStream();
        await request.Content.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        AttendanceSheet sheet;
        try
        {
            sheet = await parser.ParseAsync(buffered, request.FileName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read {File}.", request.FileName);
            return AttendanceImportResult.Failed(request.FileName,
                $"The file could not be read. It may be open in another program or saved in an unexpected format. ({ex.Message})");
        }

        var result = new AttendanceImportResult
        {
            FileName = request.FileName,
            RowsRead = sheet.Rows.Count,
            SessionColumns = sheet.SessionDates.Count,
            SessionDates = sheet.SessionDates.ToList(),
            Issues = sheet.Issues.ToList()
        };

        if (sheet.HasFatalIssues)
        {
            return result;
        }

        var studentIds = await _students.GetIdsByStudentNumberAsync(cancellationToken);
        var lectureIds = await _lectures.GetIdsByDateAsync(course.Id, cancellationToken);

        var newLectures = ResolveLectures(sheet, course.Id, lectureIds, request.CreateMissingLectures, result);
        var newStudents = ResolveStudents(sheet, studentIds, request.CreateMissingStudents, result, _course.StudentEmailDomain);

        if (request.ValidateOnly)
        {
            await PreviewAsync(sheet, studentIds, lectureIds, result, cancellationToken);
            TrimIssues(result);
            return result;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Phase 1 and 2 exist only to obtain generated identity values.
            if (newLectures.Count > 0)
            {
                _lectures.AddRange(newLectures);
                await _db.SaveChangesAsync(cancellationToken);
                foreach (var lecture in newLectures)
                {
                    lectureIds[lecture.SessionDate] = lecture.Id;
                }
            }

            if (newStudents.Count > 0)
            {
                _students.AddRange(newStudents);
                await _db.SaveChangesAsync(cancellationToken);
                foreach (var student in newStudents)
                {
                    studentIds[student.StudentNumber] = student.Id;
                }
            }

            await ApplyCellsAsync(sheet, studentIds, lectureIds, request, result, cancellationToken);

            _db.ImportBatches.Add(new ImportBatch(
                request.FileName, request.PerformedByUserId, result.RowsRead, result.LecturesCreated,
                result.StudentsCreated, result.RecordsCreated, result.RecordsUpdated, result.Issues.Count));

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            result.Committed = true;
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Import of {File} was rolled back.", request.FileName);
            result.Issues.Add(ImportIssue.Error(0, "Database",
                "The upload was rolled back and nothing was saved. This usually means the file contains two rows for the same student number."));
        }

        TrimIssues(result);
        return result;
    }

    // ------------------------------------------------------------- helpers --

    private static List<Lecture> ResolveLectures(AttendanceSheet sheet, int courseId,
        IReadOnlyDictionary<DateOnly, int> existing, bool createMissing, AttendanceImportResult result)
    {
        var toCreate = new List<Lecture>();

        foreach (var date in sheet.SessionDates)
        {
            if (existing.ContainsKey(date) || toCreate.Any(l => l.SessionDate == date))
            {
                continue;
            }

            if (createMissing)
            {
                toCreate.Add(new Lecture(courseId, date, "Imported session"));
            }
            else
            {
                result.Issues.Add(ImportIssue.Warning(1, date.ToString("yyyy/MM/dd"),
                    "No session exists for this date and 'Create missing sessions' is off, so the column was ignored."));
            }
        }

        result.LecturesCreated = toCreate.Count;
        return toCreate;
    }

    private static List<Student> ResolveStudents(AttendanceSheet sheet,
        IReadOnlyDictionary<string, int> existing, bool createMissing, AttendanceImportResult result,
        string emailDomain)
    {
        var toCreate = new List<Student>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in sheet.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.StudentNumber))
            {
                result.Issues.Add(ImportIssue.Error(row.RowNumber, "Student No",
                    "This row has no student number, so it cannot be matched to a student. Add the number or delete the row."));
                continue;
            }

            var number = Student.NormaliseStudentNumber(row.StudentNumber);

            if (!seen.Add(number))
            {
                result.Issues.Add(ImportIssue.Error(row.RowNumber, "Student No",
                    $"Student number {number} appears more than once in this file. Keep one row per student."));
                continue;
            }

            if (existing.ContainsKey(number))
            {
                continue;
            }

            if (!createMissing)
            {
                result.Issues.Add(ImportIssue.Warning(row.RowNumber, "Student No",
                    $"{number} is not enrolled and 'Create missing students' is off, so the row was skipped."));
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(row.StudentName) ? number : row.StudentName;
            toCreate.Add(new Student(number, displayName, BuildEmail(number, emailDomain), string.Empty));
        }

        result.StudentsCreated = toCreate.Count;
        return toCreate;
    }

    // Domain comes from configuration rather than a literal here, so a
    // different institution changes one line of appsettings.json.
    private static string BuildEmail(string studentNumber, string emailDomain) =>
        $"{studentNumber.ToLowerInvariant()}@{emailDomain}";

    /// <summary>Counts what a real run would change, without writing anything.</summary>
    private async Task PreviewAsync(AttendanceSheet sheet, IReadOnlyDictionary<string, int> studentIds,
        IReadOnlyDictionary<DateOnly, int> lectureIds, AttendanceImportResult result,
        CancellationToken cancellationToken)
    {
        var knownLectureIds = sheet.SessionDates
            .Where(lectureIds.ContainsKey)
            .Select(d => lectureIds[d])
            .ToList();

        var existing = await _attendance.GetKeyedAsync(knownLectureIds, cancellationToken);

        foreach (var row in sheet.Rows)
        {
            var number = Student.NormaliseStudentNumber(row.StudentNumber);
            if (!studentIds.TryGetValue(number, out var studentId))
            {
                result.RecordsCreated += CountReadableCells(row, sheet.SessionDates.Count);
                continue;
            }

            for (var c = 0; c < sheet.SessionDates.Count && c < row.Cells.Length; c++)
            {
                if (AttendanceValueMap.IsBlank(row.Cells[c]) || !AttendanceValueMap.TryMap(row.Cells[c], out var status))
                {
                    continue;
                }

                if (!lectureIds.TryGetValue(sheet.SessionDates[c], out var lectureId))
                {
                    result.RecordsCreated++;
                    continue;
                }

                if (existing.TryGetValue((lectureId, studentId), out var record))
                {
                    if (record.Status == status) result.RecordsUnchanged++;
                    else result.RecordsUpdated++;
                }
                else
                {
                    result.RecordsCreated++;
                }
            }
        }
    }

    private static int CountReadableCells(AttendanceSheetRow row, int sessionCount)
    {
        var count = 0;
        for (var c = 0; c < sessionCount && c < row.Cells.Length; c++)
        {
            if (!AttendanceValueMap.IsBlank(row.Cells[c]) && AttendanceValueMap.TryMap(row.Cells[c], out _))
            {
                count++;
            }
        }

        return count;
    }

    private async Task ApplyCellsAsync(AttendanceSheet sheet, IReadOnlyDictionary<string, int> studentIds,
        IReadOnlyDictionary<DateOnly, int> lectureIds, AttendanceImportRequest request,
        AttendanceImportResult result, CancellationToken cancellationToken)
    {
        var touchedLectureIds = sheet.SessionDates
            .Where(lectureIds.ContainsKey)
            .Select(d => lectureIds[d])
            .Distinct()
            .ToList();

        var existing = await _attendance.GetKeyedAsync(touchedLectureIds, cancellationToken);
        var inserts = new List<AttendanceRecord>(sheet.Rows.Count * Math.Max(1, sheet.SessionDates.Count) / 2);

        // Rows added during this pass are still in the Added state. If the file
        // repeats a date column we must revise them in place rather than flip
        // them to Modified, which EF would reject.
        var pending = new HashSet<(int LectureId, int StudentId)>();

        // Change detection is quadratic in tracked entities; switching it off for
        // the bulk loop turns a multi-second import into a sub-second one.
        var autoDetect = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            foreach (var row in sheet.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var number = Student.NormaliseStudentNumber(row.StudentNumber);
                if (!studentIds.TryGetValue(number, out var studentId))
                {
                    continue; // Already reported while resolving students.
                }

                for (var c = 0; c < sheet.SessionDates.Count && c < row.Cells.Length; c++)
                {
                    var raw = row.Cells[c];
                    if (AttendanceValueMap.IsBlank(raw))
                    {
                        continue;
                    }

                    if (!AttendanceValueMap.TryMap(raw, out var status))
                    {
                        result.Issues.Add(ImportIssue.Warning(row.RowNumber,
                            sheet.SessionDates[c].ToString("yyyy/MM/dd"),
                            $"'{raw.Trim()}' is not a value this system understands, so the cell was left as it was. {AttendanceValueMap.Describe()}"));
                        continue;
                    }

                    if (!lectureIds.TryGetValue(sheet.SessionDates[c], out var lectureId))
                    {
                        continue; // Column was skipped because the session does not exist.
                    }

                    var key = (lectureId, studentId);

                    if (existing.TryGetValue(key, out var record))
                    {
                        if (pending.Contains(key))
                        {
                            record.Revise(status, AttendanceSource.SpreadsheetImport, request.PerformedByUserId);
                        }
                        else if (record.Revise(status, AttendanceSource.SpreadsheetImport, request.PerformedByUserId))
                        {
                            _db.Entry(record).State = EntityState.Modified;
                            result.RecordsUpdated++;
                        }
                        else
                        {
                            result.RecordsUnchanged++;
                        }
                    }
                    else
                    {
                        var created = new AttendanceRecord(lectureId, studentId, status,
                            AttendanceSource.SpreadsheetImport, request.PerformedByUserId);
                        inserts.Add(created);
                        existing[key] = created;
                        pending.Add(key);
                        result.RecordsCreated++;
                    }
                }
            }

            if (inserts.Count > 0)
            {
                _attendance.AddRange(inserts);
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }
    }

    private static void TrimIssues(AttendanceImportResult result)
    {
        if (result.Issues.Count <= MaxReportedIssues)
        {
            return;
        }

        var hidden = result.Issues.Count - MaxReportedIssues;
        result.Issues = result.Issues.Take(MaxReportedIssues).ToList();
        result.Issues.Add(ImportIssue.Warning(0, "Report",
            $"{hidden} further messages were hidden. Fix the ones above and upload again."));
    }
}
