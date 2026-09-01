using System.Text;

namespace AttendanceRegister.Infrastructure;

/// <summary>
/// Builds mailto: links.
///
/// The system deliberately does not send mail itself. Sending would mean SMTP
/// credentials in configuration, a queue for failures, and someone owning
/// deliverability — none of which a one-week attendance tool should carry.
/// Handing a pre-filled draft to the lecturer's own mail client keeps the
/// message in their sent items, under their own address, where a reply from
/// the student will actually reach them.
/// </summary>
public static class MailTo
{
    /// <summary>
    /// Mail clients and browsers both stop honouring very long links. Sixty
    /// addresses is comfortably inside every limit worth worrying about.
    /// </summary>
    public const int MaxBulkRecipients = 60;

    public static string One(string address, string subject, string body) =>
        $"mailto:{Uri.EscapeDataString(address)}?{Query(subject, body)}";

    /// <summary>
    /// Everyone goes in BCC so no student sees who else was written to. The
    /// alternative quietly publishes the at-risk list to the whole group.
    /// </summary>
    public static string Bulk(IEnumerable<string> addresses, string subject, string body)
    {
        var recipients = addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxBulkRecipients)
            .ToList();

        var bcc = Uri.EscapeDataString(string.Join(",", recipients));
        return $"mailto:?bcc={bcc}&{Query(subject, body)}";
    }

    private static string Query(string subject, string body)
    {
        var builder = new StringBuilder();
        builder.Append("subject=").Append(Uri.EscapeDataString(subject));
        builder.Append("&body=").Append(Uri.EscapeDataString(body));
        return builder.ToString();
    }
}
