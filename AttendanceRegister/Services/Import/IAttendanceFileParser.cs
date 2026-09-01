using AttendanceRegister.Models.Import;

namespace AttendanceRegister.Services.Import;

/// <summary>
/// Strategy for turning an uploaded file into a format-neutral AttendanceSheet.
/// Adding support for a new format means adding one class, not editing the
/// import service.
/// </summary>
public interface IAttendanceFileParser
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    bool CanParse(string fileName);
    Task<AttendanceSheet> ParseAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

public interface IAttendanceFileParserFactory
{
    IAttendanceFileParser? Resolve(string fileName);
    IReadOnlyCollection<string> SupportedExtensions { get; }
}

public sealed class AttendanceFileParserFactory : IAttendanceFileParserFactory
{
    private readonly IReadOnlyList<IAttendanceFileParser> _parsers;

    public AttendanceFileParserFactory(IEnumerable<IAttendanceFileParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    public IAttendanceFileParser? Resolve(string fileName) =>
        _parsers.FirstOrDefault(p => p.CanParse(fileName));

    public IReadOnlyCollection<string> SupportedExtensions =>
        _parsers.SelectMany(p => p.SupportedExtensions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();
}
