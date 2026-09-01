using AttendanceRegister.Models.Import;

namespace AttendanceRegister.Services.Abstractions;

public interface IAttendanceImportService
{
    Task<AttendanceImportResult> ImportAsync(AttendanceImportRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyCollection<string> SupportedExtensions { get; }
}
