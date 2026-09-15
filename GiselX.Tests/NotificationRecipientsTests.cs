using GiselX.Service;

namespace GiselX.Tests;

public sealed class NotificationRecipientsTests
{
    [Fact]
    public void Parse_returns_nothing_for_an_unset_list()
    {
        Assert.Empty(NotificationRecipients.Parse(null));
        Assert.Empty(NotificationRecipients.Parse(string.Empty));
        Assert.Empty(NotificationRecipients.Parse("   "));
    }

    [Fact]
    public void Parse_accepts_both_separators_and_trims_surrounding_space()
    {
        var recipients = NotificationRecipients.Parse(
            " scm@gandummas.co.id ; b2b.admin@gandummas.co.id , sales@gandummas.co.id ");

        Assert.Equal(
            ["scm@gandummas.co.id", "b2b.admin@gandummas.co.id", "sales@gandummas.co.id"],
            recipients);
    }

    [Fact]
    public void Parse_drops_duplicates_regardless_of_casing()
    {
        var recipients = NotificationRecipients.Parse(
            "scm@gandummas.co.id; SCM@Gandummas.co.id; other@gandummas.co.id");

        Assert.Equal(["scm@gandummas.co.id", "other@gandummas.co.id"], recipients);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@missing.local")]
    [InlineData("two words@gandummas.co.id")]
    public void Parse_drops_entries_that_are_not_addresses(string entry)
    {
        // A list stored before validation existed must still send to the entries that are usable
        // rather than throwing and losing the whole notification.
        var recipients = NotificationRecipients.Parse($"good@gandummas.co.id; {entry}");

        Assert.Equal(["good@gandummas.co.id"], recipients);
    }

    [Fact]
    public void Parse_rejects_a_display_name_form_that_would_hide_the_real_address()
    {
        // "Ops <ops@example.com>" parses as a MailAddress but its Address differs from the entry,
        // so it is refused: the master-data screen should store bare addresses only.
        Assert.Empty(NotificationRecipients.Parse("Ops <ops@gandummas.co.id>"));
    }

    [Fact]
    public void Normalize_produces_a_canonical_delimited_list()
    {
        Assert.Equal("scm@gandummas.co.id; ops@gandummas.co.id",
            NotificationRecipients.Normalize(" scm@gandummas.co.id ,ops@gandummas.co.id "));
    }

    [Fact]
    public void Normalize_returns_null_rather_than_an_empty_string()
    {
        Assert.Null(NotificationRecipients.Normalize("   "));
        Assert.Null(NotificationRecipients.Normalize(";;"));
    }

    [Fact]
    public void FindInvalid_names_every_unusable_entry_so_the_message_can_quote_them()
    {
        var invalid = NotificationRecipients.FindInvalid(
            "good@gandummas.co.id; oops; also bad@x; fine@gandummas.co.id");

        Assert.Equal(["oops", "also bad@x"], invalid);
    }

    [Fact]
    public void FindInvalid_is_empty_for_a_clean_list()
    {
        Assert.Empty(NotificationRecipients.FindInvalid("a@gandummas.co.id; b@gandummas.co.id"));
        Assert.Empty(NotificationRecipients.FindInvalid(null));
    }
}
