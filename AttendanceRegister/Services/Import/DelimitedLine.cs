using System.Text;

namespace AttendanceRegister.Services.Import;

/// <summary>
/// Minimal RFC 4180 style field splitter. Writes into a caller-owned list so a
/// large upload reuses one buffer instead of allocating per row.
/// </summary>
internal static class DelimitedLine
{
    private static readonly char[] Candidates = { ';', ',', '\t', '|' };

    public static char DetectDelimiter(string headerLine)
    {
        var best = ',';
        var bestCount = 0;

        foreach (var candidate in Candidates)
        {
            var count = 0;
            var inQuotes = false;
            foreach (var ch in headerLine)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (ch == candidate && !inQuotes)
                {
                    count++;
                }
            }

            if (count > bestCount)
            {
                bestCount = count;
                best = candidate;
            }
        }

        return best;
    }

    public static void Split(string line, char delimiter, List<string> buffer, StringBuilder scratch)
    {
        buffer.Clear();
        scratch.Clear();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        scratch.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    scratch.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                buffer.Add(scratch.ToString());
                scratch.Clear();
            }
            else if (ch != '\r')
            {
                scratch.Append(ch);
            }
        }

        buffer.Add(scratch.ToString());
    }
}
