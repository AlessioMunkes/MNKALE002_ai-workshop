using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.Import;
using AttendanceRegister.Services.Abstractions;
using AttendanceRegister.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Data;

/// <summary>
/// Creates the database on first run and loads demonstration data by pushing the
/// bundled class list through the real import service, so the seeding path
/// exercises exactly the same code as a lecturer upload.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly AttendanceDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IAttendanceImportService _importService;
    private readonly IAttendanceQueryService _queryService;
    private readonly IWebHostEnvironment _environment;
    private readonly CourseOptions _course;
    private readonly SeedOptions _seed;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AttendanceDbContext db,
        IPasswordHasher hasher,
        IAttendanceImportService importService,
        IAttendanceQueryService queryService,
        IWebHostEnvironment environment,
        IOptions<CourseOptions> course,
        IOptions<SeedOptions> seed,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _hasher = hasher;
        _importService = importService;
        _queryService = queryService;
        _environment = environment;
        _course = course.Value;
        _seed = seed.Value;
        _logger = logger;
    }

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        var course = await EnsureCourseAsync(cancellationToken);
        var lecturer = await EnsureLecturerAsync(cancellationToken);

        if (!_seed.Enabled)
        {
            return;
        }

        if (!await _db.Students.AnyAsync(cancellationToken))
        {
            try
            {
                await ImportBundledClassListAsync(lecturer.Id, cancellationToken);
                await GivePasswordsToSeededStudentsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Seeding is a convenience. A malformed seed file should leave the
                // application running with an empty register, not refuse to start.
                _logger.LogWarning(ex, "Seeding from {File} failed. The register will start empty.", _seed.AttendanceFile);
            }
        }

        await EnsureTodaysSessionAsync(course.Id, cancellationToken);

        try
        {
            await SeedQueriesAsync(lecturer.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not seed demonstration queries.");
        }
    }

    private async Task<Course> EnsureCourseAsync(CancellationToken cancellationToken)
    {
        var code = _course.Code.ToUpperInvariant();
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
        if (course is not null)
        {
            return course;
        }

        course = new Course(_course.Code, _course.Title, _course.MinimumAttendancePercent);
        _db.Courses.Add(course);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created course {Code}.", course.Code);
        return course;
    }

    private async Task<Lecturer> EnsureLecturerAsync(CancellationToken cancellationToken)
    {
        var email = User.NormaliseEmail(_seed.LecturerEmail);
        var lecturer = await _db.Lecturers.FirstOrDefaultAsync(l => l.Email == email, cancellationToken);
        if (lecturer is not null)
        {
            return lecturer;
        }

        lecturer = new Lecturer(_seed.LecturerStaffNumber, "Course Convenor", email, _hasher.Hash(_seed.LecturerPassword));
        _db.Lecturers.Add(lecturer);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created lecturer account {Email}.", email);
        return lecturer;
    }

    private async Task ImportBundledClassListAsync(int lecturerId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_environment.ContentRootPath, _seed.AttendanceFile);
        if (!File.Exists(path))
        {
            _logger.LogWarning("Seed file {Path} was not found. Starting with an empty register.", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var result = await _importService.ImportAsync(new AttendanceImportRequest
        {
            Content = stream,
            FileName = Path.GetFileName(path),
            CreateMissingStudents = true,
            CreateMissingLectures = true,
            PerformedByUserId = lecturerId
        }, cancellationToken);

        _logger.LogInformation(
            "Seeded {Students} students, {Lectures} sessions and {Records} attendance records from {File}.",
            result.StudentsCreated, result.LecturesCreated, result.RecordsCreated, result.FileName);
    }

    /// <summary>
    /// The importer creates students without a usable password. Give every seeded
    /// student the shared demonstration password in one bulk pass.
    /// </summary>
    private async Task GivePasswordsToSeededStudentsAsync(CancellationToken cancellationToken)
    {
        var hash = _hasher.Hash(_seed.DefaultStudentPassword);
        var students = await _db.Students.ToListAsync(cancellationToken);
        foreach (var student in students)
        {
            student.SetPasswordHash(hash);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTodaysSessionAsync(int courseId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (await _db.Lectures.AnyAsync(l => l.CourseId == courseId && l.SessionDate == today, cancellationToken))
        {
            return;
        }

        _db.Lectures.Add(new Lecture(courseId, today, "Live session", "Leslie Social 2A"));
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Added a session for {Date} so check-in can be demonstrated.", today);
    }

    /// <summary>
    /// Puts a handful of realistic queries in the system so the review workflow
    /// has something to act on. Each one is attached to a session the student is
    /// actually marked absent for, so approving it visibly changes the register.
    /// </summary>
    private async Task SeedQueriesAsync(int lecturerId, CancellationToken cancellationToken)
    {
        if (await _db.AttendanceQueries.AnyAsync(cancellationToken))
        {
            return;
        }

        var absences = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.Status == AttendanceStatus.Absent)
            .OrderBy(r => r.StudentId).ThenBy(r => r.LectureId)
            .Select(r => new { r.StudentId, r.LectureId })
            .ToListAsync(cancellationToken);

        var targets = absences
            .GroupBy(a => a.StudentId)
            .Select(g => g.First())
            .Take(5)
            .ToList();

        if (targets.Count < 3)
        {
            return;
        }

        var scripts = new (AttendanceStatus Requested, string Reason)[]
        {
            (AttendanceStatus.Present, "I signed the sheet at the front of the venue but the register has me down as absent for this lecture."),
            (AttendanceStatus.Present, "I typed in the check-in code but my phone lost signal partway through. Could you confirm against the sheet?"),
            (AttendanceStatus.Excused, "I was at a scheduled hospital appointment and emailed the letter to the department that morning."),
            (AttendanceStatus.Present, "The shuttle was late so I arrived about ten minutes in and signed the second sheet that came around."),
            (AttendanceStatus.Excused, "I was representing UCT at an away fixture on this date. The sports office has the fixture list.")
        };

        var created = new List<AttendanceQuery>();
        for (var i = 0; i < targets.Count && i < scripts.Length; i++)
        {
            created.Add(new AttendanceQuery(targets[i].StudentId, targets[i].LectureId,
                scripts[i].Requested, scripts[i].Reason));
        }

        _db.AttendanceQueries.AddRange(created);
        await _db.SaveChangesAsync(cancellationToken);

        // Resolve two through the real service, so the seeded data shows both
        // outcomes and the approved one genuinely rewrites the register.
        if (created.Count > 1)
        {
            await _queryService.ResolveAsync(created[1].Id, true,
                "Checked against the signed sheet for that lecture.", lecturerId, cancellationToken);
        }

        if (created.Count > 2)
        {
            await _queryService.ResolveAsync(created[2].Id, false,
                "No letter reached the department. Send it to the course administrator and I will reopen this.",
                lecturerId, cancellationToken);
        }

        _logger.LogInformation("Seeded {Count} demonstration queries.", created.Count);
    }
}
