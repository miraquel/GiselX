using System.Net.Mail;

namespace GiselX.Service;

/// <summary>
/// Internal notification addresses are stored as one delimited string per Business Unit rather than
/// as their own table, because they are a short list maintained by hand and never joined against.
/// This is the single place that decides how that string is split and what counts as a valid entry,
/// so the master-data screen validates exactly what the mailer will later send to.
/// </summary>
public static class NotificationRecipients
{
    /// <summary>Accepts both separators: operators paste lists from Outlook, which uses ';'.</summary>
    private static readonly char[] Separators = [';', ','];

    public const int MaxLength = 1000;

    /// <summary>
    /// Splits a stored list into addresses, dropping blanks and duplicates. Never throws — a list
    /// that was stored before validation existed must not break the send.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsValidAddress)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Normalises a list for storage: trimmed, de-duplicated and separated by "; ". Returns null for
    /// an empty list so the column stays NULL rather than holding an empty string.
    /// </summary>
    public static string? Normalize(string? value)
    {
        var addresses = Parse(value);
        return addresses.Count == 0 ? null : string.Join("; ", addresses);
    }

    /// <summary>
    /// Returns the entries that are not usable addresses, so the caller can name them in a
    /// validation message instead of silently dropping them.
    /// </summary>
    public static IReadOnlyList<string> FindInvalid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => !IsValidAddress(entry))
            .ToList();
    }

    private static bool IsValidAddress(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry)) return false;

        // MailAddress is the same parser MimeKit will effectively face, and it rejects the shapes
        // that matter here (missing @, spaces, trailing separators) without a regex of our own.
        try
        {
            var parsed = new MailAddress(entry);
            return string.Equals(parsed.Address, entry, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
