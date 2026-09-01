namespace AttendanceRegister.Services.Abstractions;

public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

public interface IAttendanceExportService
{
    /// <summary>Writes the register back out in the same wide layout it was imported from.</summary>
    Task<ExportFile> ExportRegisterAsync(CancellationToken cancellationToken = default);
}
