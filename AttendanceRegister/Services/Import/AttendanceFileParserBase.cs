using System.Globalization;
using AttendanceRegister.Models.Import;

namespace AttendanceRegister.Services.Import;

public abstract class AttendanceFileParserBase : IAttendanceFileParser
{
    /// <summary>Tried in order, so the unambiguous ISO-style layouts win first.</summary>
    private static readonly string[] DateFormats =
    {
        "yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d",
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
        "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy",
        "yyyyMMdd"
    };

    public abstract IReadOnlyCollection<string> SupportedExtensions { get; }

    public abstract Task<AttendanceSheet> ParseAsync(Stream content, string fileName,
        CancellationToken cancellationToken = default);

    public bool CanParse(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) &&
               SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    protected static bool TryParseSessionDate(string raw, out DateOnly date)
    {
        date = default;
        var text = raw.Trim().Trim('"');
        if (text.Length == 0)
        {
            return false;
        }

        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        // Excel sometimes hands over a date column as a raw serial number.
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            && serial > 20000 && serial < 80000)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Locates the name and student-number columns from the header text.</summary>
    protected static (int NameIndex, int NumberIndex) MapIdentityColumns(IReadOnlyList<string> headers,
        ICollection<ImportIssue> issues)
    {
        var nameIndex = -1;
        var numberIndex = -1;

        for (var i = 0; i < headers.Count; i++)
        {
            var key = Normalise(headers[i]);
            if (key.Length == 0)
            {
                continue;
            }

            if (numberIndex < 0 && (key.Contains("studentno") || key.Contains("studentnumber")
                                    || key is "no" or "number" or "studno" or "sn"))
            {
                numberIndex = i;
            }
            else if (nameIndex < 0 && key.Contains("name"))
            {
                nameIndex = i;
            }
        }

        if (nameIndex < 0 || numberIndex < 0)
        {
            issues.Add(ImportIssue.Warning(1, "Header",
                "Could not find both a 'Student Name' and a 'Student No' column, so the first column was read as the name and the second as the student number."));
            nameIndex = nameIndex < 0 ? 0 : nameIndex;
            numberIndex = numberIndex < 0 ? 1 : numberIndex;
        }

        return (nameIndex, numberIndex);
    }

    private static string Normalise(string value)
    {
        if (value.Length > 128)
        {
            value = value[..128];
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = char.ToLowerInvariant(ch);
            }
        }

        return new string(buffer[..length]);
    }
}
