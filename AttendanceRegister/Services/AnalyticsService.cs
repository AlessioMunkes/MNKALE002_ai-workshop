using AttendanceRegister.Data;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly AttendanceDbContext _db;
    private readonly ICourseContext _courseContext;
    private readonly IAttendanceQueryService _queries;

    public AnalyticsService(AttendanceDbContext db, ICourseContext courseContext, IAttendanceQueryService queries)
    {
        _db = db;
        _courseContext = courseContext;
        _queries = queries;
    }

    // ------------------------------------------------------------- overview --

    public async Task<CourseOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);
        var enrolled = await _db.Students.CountAsync(cancellationToken);

        // Only sessions that have been captured count towards any percentage.
        var capturedIds = await _db.AttendanceRecords.AsNoTracking()
            .Select(r => r.LectureId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var stats = await _db.Lectures.AsNoTracking()
            .Where(l => l.CourseId == course.Id && capturedIds.Contains(l.Id))
            .OrderBy(l => l.SessionDate)
            .Select(l => new LectureAttendanceStat
            {
                LectureId = l.Id,
                SessionDate = l.SessionDate,
                Topic = l.Topic,
                Enrolled = enrolled,
                RecordedCount = l.AttendanceRecords.Count(),
                // AttendanceRules.Counted is an array, which EF turns into IN.
                PresentCount = l.AttendanceRecords.Count(r => AttendanceRules.Counted.Contains(r.Status))
            })
            .ToListAsync(cancellationToken);

        var perStudent = await _db.Students.AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.StudentNumber,
                s.DisplayName,
                s.Email,
                Attended = s.AttendanceRecords.Count(r =>
                    capturedIds.Contains(r.LectureId) && AttendanceRules.Counted.Contains(r.Status))
            })
            .ToListAsync(cancellationToken);

        var held = stats.Count;

        var atRisk = perStudent
            .Select(s => new StudentAttendanceSummary
            {
                StudentId = s.Id,
                StudentNumber = s.StudentNumber,
                DisplayName = s.DisplayName,
                Email = s.Email,
                SessionsHeld = held,
                SessionsAttended = s.Attended,
                MinimumPercent = course.MinimumAttendancePercent
            })
            .Where(s => !s.MeetsThreshold)
            .OrderBy(s => s.Percentage)
            .ToList();

        return new CourseOverview
        {
            CourseCode = course.Code,
            Enrolled = enrolled,
            SessionsHeld = held,
            MinimumPercent = course.MinimumAttendancePercent,
            OpenQueries = await _queries.CountOpenAsync(cancellationToken),
            AverageAttendance = stats.Count == 0 ? 0 : Math.Round(stats.Average(s => s.Percentage), 1),
            LectureStats = stats,
            AtRiskStudents = atRisk
        };
    }

    // ----------------------------------------------------------- class list --

    /// <summary>
    /// Four queries regardless of class size: sessions, students, records and
    /// open-query counts. Everything else is composed in memory, because the
    /// per-session strip needs every cell anyway and pulling them one student
    /// at a time would be a hundred round trips.
    /// </summary>
    public async Task<List<ClassListRow>> GetClassListAsync(CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);

        var lectures = await _db.Lectures.AsNoTracking()
            .Where(l => l.CourseId == course.Id)
            .OrderBy(l => l.SessionDate)
            .Select(l => new { l.Id, l.SessionDate })
            .ToListAsync(cancellationToken);

        var students = await _db.Students.AsNoTracking()
            .OrderBy(s => s.DisplayName)
            .Select(s => new { s.Id, s.StudentNumber, s.DisplayName, s.Email, s.IsActive })
            .ToListAsync(cancellationToken);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Select(r => new { r.StudentId, r.LectureId, r.Status })
            .ToListAsync(cancellationToken);

        var openQueries = await _db.AttendanceQueries.AsNoTracking()
            .Where(q => q.Status == QueryStatus.Open)
            .GroupBy(q => q.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count, cancellationToken);

        var captured = records.Select(r => r.LectureId).Distinct().ToHashSet();
        var byStudent = records.ToLookup(r => r.StudentId);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var rows = new List<ClassListRow>(students.Count);

        foreach (var student in students)
        {
            var statusByLecture = byStudent[student.Id].ToDictionary(r => r.LectureId, r => r.Status);

            var cells = new List<SessionCell>(lectures.Count);
            var held = 0;
            var attended = 0;
            DateOnly? lastAttended = null;

            foreach (var lecture in lectures)
            {
                var hasRecord = statusByLecture.TryGetValue(lecture.Id, out var status);

                if (lecture.SessionDate <= today || hasRecord)
                {
                    cells.Add(new SessionCell(lecture.Id, lecture.SessionDate, hasRecord ? status : null));
                }

                if (!captured.Contains(lecture.Id))
                {
                    continue;
                }

                held++;
                if (hasRecord && status.Counts())
                {
                    attended++;
                    lastAttended = lecture.SessionDate; // lectures are in date order
                }
            }

            rows.Add(new ClassListRow
            {
                Summary = new StudentAttendanceSummary
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentNumber,
                    DisplayName = student.DisplayName,
                    Email = student.Email,
                    SessionsHeld = held,
                    SessionsAttended = attended,
                    MinimumPercent = course.MinimumAttendancePercent,
                    Cells = cells
                },
                LastAttendedOn = lastAttended,
                IsActive = student.IsActive,
                OpenQueries = openQueries.TryGetValue(student.Id, out var count) ? count : 0
            });
        }

        return rows;
    }

    // -------------------------------------------------------- student detail --

    public async Task<StudentAttendanceDetail?> GetStudentDetailAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);

        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.StudentNumber, s.DisplayName, s.Email, s.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return null;
        }

        var lectures = await _db.Lectures.AsNoTracking()
            .Where(l => l.CourseId == course.Id)
            .OrderBy(l => l.SessionDate)
            .Select(l => new { l.Id, l.SessionDate, l.Topic, l.Venue })
            .ToListAsync(cancellationToken);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .ToDictionaryAsync(r => r.LectureId, cancellationToken);

        var captured = (await _db.AttendanceRecords.AsNoTracking()
            .Select(r => r.LectureId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var cells = new List<SessionCell>(lectures.Count);
        var sessions = new List<SessionDetailRow>(lectures.Count);
        var held = 0;
        var attended = 0;

        foreach (var lecture in lectures)
        {
            var hasRecord = records.TryGetValue(lecture.Id, out var record);

            if (lecture.SessionDate <= today || hasRecord)
            {
                cells.Add(new SessionCell(lecture.Id, lecture.SessionDate, hasRecord ? record!.Status : null));

                sessions.Add(new SessionDetailRow
                {
                    LectureId = lecture.Id,
                    SessionDate = lecture.SessionDate,
                    Topic = lecture.Topic,
                    Venue = lecture.Venue,
                    Status = hasRecord ? record!.Status : null,
                    Source = hasRecord ? record!.Source : null,
                    RecordedAtUtc = hasRecord ? record!.RecordedAtUtc : null,
                    Note = hasRecord ? record!.Note : null
                });
            }

            if (!captured.Contains(lecture.Id))
            {
                continue;
            }

            held++;
            if (hasRecord && record!.Status.Counts())
            {
                attended++;
            }
        }

        sessions.Reverse(); // newest first, which is what anyone reading a record wants

        return new StudentAttendanceDetail
        {
            Summary = new StudentAttendanceSummary
            {
                StudentId = student.Id,
                StudentNumber = student.StudentNumber,
                DisplayName = student.DisplayName,
                Email = student.Email,
                SessionsHeld = held,
                SessionsAttended = attended,
                MinimumPercent = course.MinimumAttendancePercent,
                Cells = cells
            },
            Email = student.Email,
            IsActive = student.IsActive,
            Sessions = sessions,
            Queries = await _queries.GetForStudentAsync(studentId, cancellationToken)
        };
    }
}
