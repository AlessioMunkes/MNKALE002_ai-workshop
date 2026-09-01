using System.Text;
using AttendanceRegister.Models.Import;

namespace AttendanceRegister.Services.Import;

/// <summary>
/// Reads .csv / .txt / .tsv registers one line at a time. The delimiter is
/// detected from the header, and only the mapped session columns are copied out
/// of each row, so memory stays proportional to the class size rather than to
/// the file size.
/// </summary>
public sealed class DelimitedAttendanceFileParser : AttendanceFileParserBase
{
    public override IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".csv", ".txt", ".tsv" };

    public override async Task<AttendanceSheet> ParseAsync(Stream content, string fileName,
        CancellationToken cancellationToken = default)
    {
        var sheet = new AttendanceSheet { FileName = fileName };

        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 8192, leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            sheet.Issues.Add(ImportIssue.Error(1, "Header", "The file is empty. Expected a header row of Student Name, Student No, then one column per session date."));
            return sheet;
        }

        var delimiter = DelimitedLine.DetectDelimiter(headerLine);
        var scratch = new StringBuilder(64);
        var cells = new List<string>(32);

        DelimitedLine.Split(headerLine, delimiter, cells, scratch);
        var headers = cells.ToList();

        var (nameIndex, numberIndex) = MapIdentityColumns(headers, sheet.Issues);

        // Map every remaining column to a session date once, up front.
        var sessionColumns = new List<int>();
        for (var i = 0; i < headers.Count; i++)
        {
            if (i == nameIndex || i == numberIndex)
            {
                continue;
            }

            if (TryParseSessionDate(headers[i], out var date))
            {
                sessionColumns.Add(i);
                sheet.SessionDates.Add(date);
            }
            else if (!string.IsNullOrWhiteSpace(headers[i]))
            {
                sheet.Issues.Add(ImportIssue.Warning(1, ColumnName(i),
                    $"Skipped the column headed '{headers[i].Trim()}' because it is not a date. Use a format such as 2026/03/09."));
            }
        }

        if (sessionColumns.Count == 0)
        {
            sheet.Issues.Add(ImportIssue.Error(1, "Header", "No session date columns were found. Every column after Student No must be headed with a date, for example 2026/03/09."));
            return sheet;
        }

        var rowNumber = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            DelimitedLine.Split(line, delimiter, cells, scratch);

            if (cells.Count != headers.Count)
            {
                sheet.Issues.Add(ImportIssue.Warning(rowNumber, "Row",
                    $"Expected {headers.Count} columns but found {cells.Count}. Missing values were treated as blank."));
            }

            var values = new string[sessionColumns.Count];
            for (var c = 0; c < sessionColumns.Count; c++)
            {
                var index = sessionColumns[c];
                values[c] = index < cells.Count ? cells[index] : string.Empty;
            }

            sheet.Rows.Add(new AttendanceSheetRow
            {
                RowNumber = rowNumber,
                StudentName = nameIndex < cells.Count ? cells[nameIndex].Trim() : string.Empty,
                StudentNumber = numberIndex < cells.Count ? cells[numberIndex].Trim() : string.Empty,
                Cells = values
            });
        }

        if (sheet.Rows.Count == 0)
        {
            sheet.Issues.Add(ImportIssue.Error(1, "Rows", "The file has a header but no student rows."));
        }

        return sheet;
    }

    private static string ColumnName(int zeroBasedIndex)
    {
        var name = string.Empty;
        var index = zeroBasedIndex;
        do
        {
            name = (char)('A' + index % 26) + name;
            index = index / 26 - 1;
        } while (index >= 0);

        return $"Column {name}";
    }
}
