namespace GiselX.Domain;

/// <summary>
/// The addressees for one company's upload notification, resolved in a single query: the
/// distributor's own contact address and the internal PT. GMK list configured on its Business Unit.
/// </summary>
public sealed class UploadNotificationRecipients
{
    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    /// <summary>The distributor's address, from Company.ContactEmail. Null when never configured.</summary>
    public string? ContactEmail { get; set; }

    public string? BusinessUnitName { get; set; }

    /// <summary>Delimited internal list from the company's Business Unit. Null when unset.</summary>
    public string? InternalNotificationEmails { get; set; }

    public string? ChannelName { get; set; }

    /// <summary>Delimited internal list from the company's Channel. Null when unset.</summary>
    public string? ChannelNotificationEmails { get; set; }

    public string? SalesmanName { get; set; }

    /// <summary>
    /// Delimited internal list from the company's Salesman — the GMK person accountable for this
    /// distributor, who wants to know the moment their data lands. Null when unset.
    /// </summary>
    public string? SalesmanNotificationEmails { get; set; }
}
