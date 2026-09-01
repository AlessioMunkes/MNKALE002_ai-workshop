using System.Text;
using AttendanceRegister.Data;
using AttendanceRegister.Services.Abstractions;
using AttendanceRegister.Services.Import;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Services;

public sealed class AttendanceExportService : IAttendanceExportService
{
    private const char Delimiter = ';';

    private readonly AttendanceDbContext _db;
    private readonly ICourseContext _courseContext;

    public AttendanceExportService(AttendanceDbContext db, ICourseContext courseContext)
    {
        _db = db;
        _courseContext = courseContext;
    }

    public async Task<ExportFile> ExportRegisterAsync(CancellationToken cancellationToken = default)
    {
        var course = await _courseContext.GetAsync(cancellationToken);

        var lectures = await _db.Lectures.AsNoTracking()
            .Where(l => l.CourseId == course.Id)
            .OrderBy(l => l.SessionDate)
            .Select(l => new { l.Id, l.SessionDate })
            .ToListAsync(cancellationToken);

        var students = await _db.Students.AsNoTracking()
            .OrderBy(s => s.StudentNumber)
            .Select(s => new { s.Id, s.StudentNumber, s.DisplayName })
            .ToListAsync(cancellationToken);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Select(r => new { r.StudentId, r.LectureId, r.Status })
            .ToListAsync(cancellationToken);

        var lookup = records.ToDictionary(r => (r.StudentId, r.LectureId), r => r.Status);

        var builder = new StringBuilder(students.Count * (lectures.Count * 2 + 40));
        builder.Append("Student Name").Append(Delimiter).Append("Student No");
        foreach (var lecture in lectures)
        {
            builder.Append(Delimiter).Append(lecture.SessionDate.ToString("yyyy/MM/dd"));
        }

        builder.Append("\r\n");

        foreach (var student in students)
        {
            builder.Append(Escape(student.DisplayName)).Append(Delimiter).Append(Escape(student.StudentNumber));

            foreach (var lecture in lectures)
            {
                builder.Append(Delimiter);
                if (lookup.TryGetValue((student.Id, lecture.Id), out var status))
                {
                    builder.Append(AttendanceValueMap.ToCell(status));
                }
            }

            builder.Append("\r\n");
        }

        var fileName = $"{course.Code}_Attendance_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return new ExportFile(fileName, "text/csv", Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string Escape(string value) =>
        value.Contains(Delimiter) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
