using AttendanceRegister.Models.Import;
using ClosedXML.Excel;

namespace AttendanceRegister.Services.Import;

/// <summary>Reads the same register layout from a real Excel workbook.</summary>
public sealed class ExcelAttendanceFileParser : AttendanceFileParserBase
{
    public override IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".xlsx", ".xlsm" };

    public override Task<AttendanceSheet> ParseAsync(Stream content, string fileName,
        CancellationToken cancellationToken = default)
    {
        var sheet = new AttendanceSheet { FileName = fileName };

        using var workbook = new XLWorkbook(content);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            sheet.Issues.Add(ImportIssue.Error(0, "Workbook", "The workbook has no worksheets."));
            return Task.FromResult(sheet);
        }

        var used = worksheet.RangeUsed();
        if (used is null)
        {
            sheet.Issues.Add(ImportIssue.Error(0, "Worksheet",
                $"Worksheet '{worksheet.Name}' is empty. Expected a header row of Student Name, Student No, then one column per session date."));
            return Task.FromResult(sheet);
        }

        var firstRow = used.FirstRow();
        var columnCount = used.ColumnCount();

        var headers = new List<string>(columnCount);
        for (var c = 1; c <= columnCount; c++)
        {
            headers.Add(firstRow.Cell(c).GetFormattedString().Trim());
        }

        var (nameIndex, numberIndex) = MapIdentityColumns(headers, sheet.Issues);

        var sessionColumns = new List<int>();
        for (var i = 0; i < headers.Count; i++)
        {
            if (i == nameIndex || i == numberIndex)
            {
                continue;
            }

            var cell = firstRow.Cell(i + 1);
            DateOnly date;

            if (cell.TryGetValue<DateTime>(out var typedDate))
            {
                date = DateOnly.FromDateTime(typedDate);
            }
            else if (!TryParseSessionDate(headers[i], out date))
            {
                if (!string.IsNullOrWhiteSpace(headers[i]))
                {
                    sheet.Issues.Add(ImportIssue.Warning(1, cell.Address.ToString() ?? $"Column {i + 1}",
                        $"Skipped the column headed '{headers[i]}' because it is not a date."));
                }

                continue;
            }

            sessionColumns.Add(i);
            sheet.SessionDates.Add(date);
        }

        if (sessionColumns.Count == 0)
        {
            sheet.Issues.Add(ImportIssue.Error(1, "Header", "No session date columns were found in the first row."));
            return Task.FromResult(sheet);
        }

        var rowNumber = 1;
        foreach (var row in used.Rows().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var studentNumber = row.Cell(numberIndex + 1).GetFormattedString().Trim();
            var studentName = row.Cell(nameIndex + 1).GetFormattedString().Trim();

            if (studentNumber.Length == 0 && studentName.Length == 0)
            {
                continue;
            }

            var values = new string[sessionColumns.Count];
            for (var c = 0; c < sessionColumns.Count; c++)
            {
                values[c] = row.Cell(sessionColumns[c] + 1).GetFormattedString().Trim();
            }

            sheet.Rows.Add(new AttendanceSheetRow
            {
                RowNumber = rowNumber,
                StudentName = studentName,
                StudentNumber = studentNumber,
                Cells = values
            });
        }

        if (sheet.Rows.Count == 0)
        {
            sheet.Issues.Add(ImportIssue.Error(1, "Rows", "The worksheet has a header but no student rows."));
        }

        return Task.FromResult(sheet);
    }
}
