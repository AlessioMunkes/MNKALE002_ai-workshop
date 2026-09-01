using AttendanceRegister.Models.Entities;
using AttendanceRegister.Models.ViewModels;
using AttendanceRegister.Repositories;
using AttendanceRegister.Services.Abstractions;
using AttendanceRegister.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Services;

public sealed class AttendanceService : IAttendanceService
{
    private readonly IStudentRepository _students;
    private readonly ILectureRepository _lectures;
    private readonly IAttendanceRepository _attendance;
    private readonly ICourseContext _courseContext;
    private readonly IUnitOfWork _unitOfWork;

    public AttendanceService(
        IStudentRepository students,
        ILectureRepository lectures,
        IAttendanceRepository attendance,
        ICourseContext courseContext,
        IUnitOfWork unitOfWork)
    {
        _students = students;
        _lectures = lectures;
        _attendance = attendance;
        _courseContext = courseContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<StudentAttendanceSummary> GetSummaryAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);
        var student = await _students.GetByIdAsync(studentId, cancellationToken)
                      ?? throw new InvalidOperationException($"Student {studentId} was not found.");

        var lectures = await _lectures.GetForCourseAsync(course.Id, cancellationToken);
        var records = (await _attendance.GetForStudentAsync(studentId, cancellationToken))
            .ToDictionary(r => r.LectureId);
        var captured = (await _attendance.GetCapturedLectureIdsAsync(cancellationToken)).ToHashSet();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var cells = new List<SessionCell>(lectures.Count);
        var held = 0;
        var attended = 0;

        foreach (var lecture in lectures)
        {
            var hasRecord = records.TryGetValue(lecture.Id, out var record);

            if (lecture.SessionDate <= today || hasRecord)
            {
                cells.Add(new SessionCell(lecture.Id, lecture.SessionDate, hasRecord ? record!.Status : null));
            }

            // A session only counts once someone has actually captured it.
            if (!captured.Contains(lecture.Id))
            {
                continue;
            }

            held++;
            if (hasRecord && record!.IsCounted)
            {
                attended++;
            }
        }

        return new StudentAttendanceSummary
        {
            StudentId = student.Id,
            StudentNumber = student.StudentNumber,
            DisplayName = student.DisplayName,
            SessionsHeld = held,
            SessionsAttended = attended,
            MinimumPercent = course.MinimumAttendancePercent,
            Cells = cells
        };
    }

    public async Task<List<Lecture>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);
        return await _lectures.GetForCourseAsync(course.Id, cancellationToken);
    }

    public Task<Lecture?> GetSessionAsync(int lectureId, CancellationToken cancellationToken = default) =>
        _lectures.GetByIdAsync(lectureId, cancellationToken);

    public async Task<Lecture?> GetOpenSessionAsync(CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);
        return await _lectures.GetOpenCheckInAsync(course.Id, cancellationToken);
    }

    public async Task<CheckInOutcome> CheckInAsync(int studentId, string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new CheckInOutcome(false, "Enter the session code shown by your lecturer.");
        }

        var lecture = await GetOpenSessionAsync(cancellationToken);
        if (lecture is null)
        {
            return new CheckInOutcome(false,
                "No session is open for check-in right now. Check-in opens when your lecturer starts it, and closes automatically.");
        }

        if (!lecture.CodeMatches(code))
        {
            return new CheckInOutcome(false,
                $"That code does not match the one for {lecture.SessionDate:d MMMM yyyy}. Check the projector and try again.");
        }

        var existing = await _attendance.FindAsync(lecture.Id, studentId, cancellationToken);
        if (existing is not null && existing.IsCounted)
        {
            return new CheckInOutcome(false,
                $"You are already marked {existing.Status.ToString().ToLowerInvariant()} for {lecture.SessionDate:d MMMM yyyy}.");
        }

        if (existing is null)
        {
            _attendance.Add(new AttendanceRecord(lecture.Id, studentId, AttendanceStatus.Present,
                AttendanceSource.SelfCheckIn, studentId));
        }
        else
        {
            existing.Revise(AttendanceStatus.Present, AttendanceSource.SelfCheckIn, studentId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new CheckInOutcome(true, $"Recorded. You are marked present for {lecture.SessionDate:d MMMM yyyy}.");
    }

    public async Task<List<RegisterRow>> GetRegisterAsync(int lectureId,
        CancellationToken cancellationToken = default)
    {
        var records = (await _attendance.GetForLectureAsync(lectureId, cancellationToken))
            .ToDictionary(r => r.StudentId);

        var students = await _students.QueryReadOnly()
            .OrderBy(s => s.StudentNumber)
            .Select(s => new { s.Id, s.StudentNumber, s.DisplayName })
            .ToListAsync(cancellationToken);

        return students.Select(s =>
        {
            records.TryGetValue(s.Id, out var record);
            return new RegisterRow
            {
                StudentId = s.Id,
                StudentNumber = s.StudentNumber,
                DisplayName = s.DisplayName,
                Status = record?.Status ?? AttendanceStatus.Absent,
                Source = record?.Source,
                Note = record?.Note
            };
        }).ToList();
    }

    public async Task<int> SaveRegisterAsync(int lectureId, IReadOnlyDictionary<int, AttendanceStatus> statuses,
        int actorId, CancellationToken cancellationToken = default)
    {
        var existing = (await _attendance.GetForLectureAsync(lectureId, cancellationToken))
            .ToDictionary(r => r.StudentId);

        var changed = 0;
        var inserts = new List<AttendanceRecord>();

        foreach (var (studentId, status) in statuses)
        {
            if (existing.TryGetValue(studentId, out var record))
            {
                if (record.Revise(status, AttendanceSource.LecturerEdit, actorId))
                {
                    changed++;
                }
            }
            else
            {
                inserts.Add(new AttendanceRecord(lectureId, studentId, status, AttendanceSource.LecturerEdit, actorId));
                changed++;
            }
        }

        if (inserts.Count > 0)
        {
            _attendance.AddRange(inserts);
        }

        if (changed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    public async Task<bool> SetStatusAsync(int lectureId, int studentId, AttendanceStatus status,
        AttendanceSource source, int actorId, string? note, CancellationToken cancellationToken = default)
    {
        var record = await _attendance.FindAsync(lectureId, studentId, cancellationToken);
        if (record is null)
        {
            _attendance.Add(new AttendanceRecord(lectureId, studentId, status, source, actorId, note));
        }
        else if (!record.Revise(status, source, actorId, note))
        {
            return false;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Lecture> CreateSessionAsync(DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime,
        string topic, string venue, CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);

        var clash = await _lectures.Query()
            .FirstOrDefaultAsync(l => l.CourseId == course.Id && l.SessionDate == sessionDate, cancellationToken);
        if (clash is not null)
        {
            throw new InvalidOperationException($"A session already exists for {sessionDate:d MMMM yyyy}. Edit that one instead of creating a second.");
        }

        var lecture = new Lecture(course.Id, sessionDate, topic, venue);
        lecture.UpdateDetails(sessionDate, startTime, endTime, topic, venue);
        _lectures.Add(lecture);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return lecture;
    }

    public async Task<string> OpenCheckInAsync(int lectureId, int minutes,
        CancellationToken cancellationToken = default)
    {
        var lecture = await _lectures.GetByIdAsync(lectureId, cancellationToken)
                      ?? throw new InvalidOperationException("That session no longer exists.");

        // Only one session may accept check-ins at a time, otherwise a student
        // could sit in one lecture and sign into another.
        var course = await _courseContext.GetAsync(cancellationToken);
        var alreadyOpen = await _lectures.GetOpenCheckInAsync(course.Id, cancellationToken);
        if (alreadyOpen is not null && alreadyOpen.Id != lectureId)
        {
            alreadyOpen.CloseCheckIn();
        }

        var code = lecture.OpenCheckIn(TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 240)), () => CheckInCodeGenerator.Generate());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return code;
    }

    public async Task CloseCheckInAsync(int lectureId, CancellationToken cancellationToken = default)
    {
        var lecture = await _lectures.GetByIdAsync(lectureId, cancellationToken);
        if (lecture is null)
        {
            return;
        }

        lecture.CloseCheckIn();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
