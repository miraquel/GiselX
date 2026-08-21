using GiselX.Service;

namespace GiselX.Tests;

public sealed class PowerBiPublishUrlValidatorTests
{
    [Theory]
    [InlineData("https://app.powerbi.com/view?r=abc123")]
    [InlineData("  https://APP.POWERBI.COM/view/?r=abc123&pageName=ReportSection  ")]
    public void TryNormalize_accepts_publish_to_web_urls(string value)
    {
        var valid = PowerBiPublishUrlValidator.TryNormalize(value, out var normalized, out var error);

        Assert.True(valid, error);
        Assert.True(normalized.StartsWith("https://app.powerbi.com/view", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(" ", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<iframe src=\"https://app.powerbi.com/view?r=abc\"></iframe>")]
    [InlineData("http://app.powerbi.com/view?r=abc")]
    [InlineData("https://app.powerbi.com.evil.example/view?r=abc")]
    [InlineData("https://user@app.powerbi.com/view?r=abc")]
    [InlineData("https://app.powerbi.com/groups/1/reports/2")]
    [InlineData("https://app.powerbi.com/view")]
    [InlineData("https://app.powerbi.com/view?r=")]
    [InlineData("https://app.powerbi.com/view?r=abc#fragment")]
    public void TryNormalize_rejects_non_public_or_unsafe_values(string? value)
    {
        Assert.False(PowerBiPublishUrlValidator.TryNormalize(value, out var normalized, out var error));
        Assert.Equal(string.Empty, normalized);
        Assert.NotEmpty(error);
    }
}
