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

    // --------------------------------------------------------- student trend --

    /// <summary>
    /// Three queries: the sessions with their counted totals, this student's
    /// records, and the class size. Both running rates are then accumulated in
    /// one pass, which is the only way to guarantee the two lines are measured
    /// over exactly the same sessions.
    /// </summary>
    public async Task<StudentTrend> GetStudentTrendAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);
        var enrolled = await _db.Students.CountAsync(cancellationToken);

        var sessions = await _db.Lectures.AsNoTracking()
            .Where(l => l.CourseId == course.Id)
            .OrderBy(l => l.SessionDate)
            .Select(l => new
            {
                l.Id,
                l.SessionDate,
                Counted = l.AttendanceRecords.Count(r => AttendanceRules.Counted.Contains(r.Status)),
                Recorded = l.AttendanceRecords.Count()
            })
            .ToListAsync(cancellationToken);

        var mine = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .Select(r => new { r.LectureId, r.Status })
            .ToDictionaryAsync(r => r.LectureId, r => r.Status, cancellationToken);

        var points = new List<TrendPoint>();

        var studentAttended = 0;
        var studentHeld = 0;
        var classAttended = 0;
        var classPossible = 0;

        foreach (var session in sessions)
        {
            // A session nobody captured is not a session anyone missed.
            if (session.Recorded == 0)
            {
                continue;
            }

            studentHeld++;
            var counted = mine.TryGetValue(session.Id, out var status) && status.Counts();
            if (counted)
            {
                studentAttended++;
            }

            classAttended += session.Counted;
            classPossible += enrolled;

            points.Add(new TrendPoint(
                session.SessionDate,
                Math.Round(studentAttended * 100.0 / studentHeld, 1),
                classPossible == 0 ? 0 : Math.Round(classAttended * 100.0 / classPossible, 1),
                counted,
                studentAttended,
                studentHeld));
        }

        return new StudentTrend { Points = points, MinimumPercent = course.MinimumAttendancePercent };
    }

    // ------------------------------------------------------- session overview --

    /// <summary>
    /// Built on top of the class list rather than beside it. That already
    /// carries every student with a cell per session and a computed rate, which
    /// is most of what this screen needs — the only extra query is for how each
    /// record on this one lecture was captured.
    /// </summary>
    public async Task<SessionOverview?> GetSessionOverviewAsync(int lectureId,
        CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);

        var lecture = await _db.Lectures.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lectureId && l.CourseId == course.Id, cancellationToken);

        if (lecture is null)
        {
            return null;
        }

        var classList = await GetClassListAsync(cancellationToken);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.LectureId == lectureId)
            .ToDictionaryAsync(r => r.StudentId, cancellationToken);

        var attendees = classList
            .Select(row =>
            {
                records.TryGetValue(row.Summary.StudentId, out var record);
                return new SessionAttendee
                {
                    StudentId = row.Summary.StudentId,
                    StudentNumber = row.Summary.StudentNumber,
                    DisplayName = row.Summary.DisplayName,
                    Email = row.Summary.Email,
                    Status = record?.Status,
                    Source = record?.Source,
                    RecordedAtUtc = record?.RecordedAtUtc,
                    Note = record?.Note,
                    OverallPercent = row.Summary.Percentage
                };
            })
            .ToList();

        var captured = records.Values
            .GroupBy(r => r.Source)
            .Select(g => new CaptureBreakdown(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        // Rank and average come from a light aggregate over every session, not
        // from loading their records.
        var enrolled = classList.Count;

        var perSession = await _db.Lectures.AsNoTracking()
            .Where(l => l.CourseId == course.Id)
            .Select(l => new
            {
                l.Id,
                Counted = l.AttendanceRecords.Count(r => AttendanceRules.Counted.Contains(r.Status)),
                Recorded = l.AttendanceRecords.Count()
            })
            .ToListAsync(cancellationToken);

        var capturedSessions = perSession
            .Where(s => s.Recorded > 0)
            .Select(s => new { s.Id, Percent = enrolled == 0 ? 0 : s.Counted * 100.0 / enrolled })
            .OrderByDescending(s => s.Percent)
            .ToList();

        var average = capturedSessions.Count == 0 ? 0 : Math.Round(capturedSessions.Average(s => s.Percent), 1);
        var rank = capturedSessions.FindIndex(s => s.Id == lectureId) + 1;

        var queries = await _db.AttendanceQueries.AsNoTracking()
            .Include(q => q.Student)
            .Include(q => q.Lecture)
            .Where(q => q.LectureId == lectureId)
            .OrderBy(q => q.Status)
            .ThenByDescending(q => q.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        return new SessionOverview
        {
            LectureId = lecture.Id,
            SessionDate = lecture.SessionDate,
            StartTime = lecture.StartTime,
            EndTime = lecture.EndTime,
            Topic = lecture.Topic,
            Venue = lecture.Venue,
            IsCheckInOpen = lecture.IsCheckInOpen,
            Enrolled = enrolled,
            MinimumPercent = course.MinimumAttendancePercent,
            Attendees = attendees,
            Captured = captured,
            Queries = queries,
            CourseAverage = average,
            Rank = rank,
            SessionsCaptured = capturedSessions.Count
        };
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
