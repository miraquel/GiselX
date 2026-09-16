using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class EmailSettingServiceTests
{
    [Fact]
    public async Task With_nothing_saved_the_screen_shows_what_the_app_is_actually_using()
    {
        var service = CreateService(configured: new EmailSettings
        {
            SmtpHost = "mail.example.co.id", SmtpPort = 25, FromAddress = "noreply@example.co.id"
        });

        var response = await service.GetAsync(CancellationToken.None);

        Assert.True(response.Data!.IsFallbackFromConfiguration);
        Assert.Equal("mail.example.co.id", response.Data.SmtpHost);
        Assert.Equal(25, response.Data.SmtpPort);
    }

    [Fact]
    public async Task The_stored_password_is_never_sent_to_the_screen()
    {
        var service = CreateService(new StubRepository(Stored(protectedPassword: "protected:secret")));

        var response = await service.GetAsync(CancellationToken.None);

        Assert.Null(response.Data!.Password);
        Assert.True(response.Data.HasStoredPassword);
    }

    [Fact]
    public async Task A_blank_password_keeps_the_one_already_stored()
    {
        // Otherwise editing the support phone number would silently break authentication.
        var repository = new StubRepository(Stored(protectedPassword: "protected:secret"));
        var service = CreateService(repository);

        await service.SaveAsync(Valid(password: null), "ahmad", CancellationToken.None);

        Assert.Equal("protected:secret", repository.Saved!.PasswordProtected);
    }

    [Fact]
    public async Task A_typed_password_replaces_the_stored_one_and_is_encrypted()
    {
        var repository = new StubRepository(Stored(protectedPassword: "protected:old"));
        var service = CreateService(repository);

        await service.SaveAsync(Valid(password: "new-secret"), "ahmad", CancellationToken.None);

        // The stub fakes encryption, so this proves only that the value went through the protector
        // rather than being stored as typed. DataProtectionEmailSecretProtectorTests covers the real one.
        Assert.Equal("protected:new-secret", repository.Saved!.PasswordProtected);
        Assert.NotEqual("new-secret", repository.Saved.PasswordProtected);
    }

    [Fact]
    public async Task Clearing_the_password_is_explicit()
    {
        var repository = new StubRepository(Stored(protectedPassword: "protected:old"));
        var service = CreateService(repository);

        var dto = Valid(password: null);
        dto.ClearPassword = true;
        await service.SaveAsync(dto, "ahmad", CancellationToken.None);

        Assert.Null(repository.Saved!.PasswordProtected);
    }

    [Theory]
    [InlineData("", "SMTP Host is required.")]
    public async Task A_missing_host_is_refused(string host, string expected)
    {
        var dto = Valid();
        dto.SmtpHost = host;

        var response = await CreateService().SaveAsync(dto, "ahmad", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(expected, response.Errors);
    }

    [Fact]
    public async Task An_unusable_from_address_is_refused_before_it_can_break_every_send()
    {
        var dto = Valid();
        dto.FromAddress = "not-an-address";

        var response = await CreateService().SaveAsync(dto, "ahmad", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("not-an-address"));
    }

    [Fact]
    public async Task An_unknown_time_zone_is_refused_here_rather_than_silently_becoming_utc()
    {
        var dto = Valid();
        dto.TimeZoneId = "Mars Standard Time";

        var response = await CreateService().SaveAsync(dto, "ahmad", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("Mars Standard Time"));
    }

    [Fact]
    public async Task A_dashboard_url_that_is_not_a_full_address_is_refused()
    {
        var dto = Valid();
        dto.DashboardBaseUrl = "giselx-dev";

        var response = await CreateService().SaveAsync(dto, "ahmad", CancellationToken.None);

        Assert.False(response.IsSuccess);
    }

    [Fact]
    public async Task A_trailing_slash_on_the_dashboard_url_is_normalised_away()
    {
        // The link builder appends its own path; a stored trailing slash would double it.
        var repository = new StubRepository(null);
        var dto = Valid();
        dto.DashboardBaseUrl = "https://giselx-dev.gandummas.co.id/";

        await CreateService(repository).SaveAsync(dto, "ahmad", CancellationToken.None);

        Assert.Equal("https://giselx-dev.gandummas.co.id", repository.Saved!.DashboardBaseUrl);
    }

    [Fact]
    public async Task Stored_settings_win_over_appsettings()
    {
        var provider = new EmailSettingsProvider(
            new StubRepository(Stored(protectedPassword: "protected:stored-secret")),
            Options.Create(new EmailSettings { SmtpHost = "from-appsettings", Password = "file-secret" }),
            new StubProtector(),
            NullLogger<EmailSettingsProvider>.Instance);

        var settings = await provider.GetAsync(CancellationToken.None);

        Assert.Equal("mail.stored.co.id", settings.SmtpHost);
        Assert.Equal("stored-secret", settings.Password);
    }

    [Fact]
    public async Task Appsettings_is_used_while_nothing_has_been_saved()
    {
        var provider = new EmailSettingsProvider(
            new StubRepository(null),
            Options.Create(new EmailSettings { SmtpHost = "from-appsettings" }),
            new StubProtector(),
            NullLogger<EmailSettingsProvider>.Instance);

        Assert.Equal("from-appsettings", (await provider.GetAsync(CancellationToken.None)).SmtpHost);
    }

    [Fact]
    public async Task An_undecryptable_password_yields_an_empty_one_rather_than_throwing()
    {
        // Losing the data-protection keys must not stop the application; it must stop authenticating
        // and say so, which is recoverable by re-entering the password.
        var provider = new EmailSettingsProvider(
            new StubRepository(Stored(protectedPassword: "garbage-not-protected")),
            Options.Create(new EmailSettings()),
            new StubProtector(),
            NullLogger<EmailSettingsProvider>.Instance);

        var settings = await provider.GetAsync(CancellationToken.None);

        Assert.Equal(string.Empty, settings.Password);
        Assert.Equal("mail.stored.co.id", settings.SmtpHost);
    }

    private static EmailSettingService CreateService(StubRepository? repository = null,
        EmailSettings? configured = null) =>
        new(repository ?? new StubRepository(null), Options.Create(configured ?? new EmailSettings()),
            new StubProtector());

    private static EmailSetting Stored(string? protectedPassword) => new()
    {
        SmtpHost = "mail.stored.co.id",
        SmtpPort = 587,
        EnableSsl = true,
        Username = "giselx",
        PasswordProtected = protectedPassword,
        FromAddress = "noreply@gandummas.co.id",
        FromName = "GiselX System",
        TimeZoneId = "SE Asia Standard Time"
    };

    private static EmailSettingDto Valid(string? password = null) => new()
    {
        SmtpHost = "mail.gandummas.co.id",
        SmtpPort = 587,
        EnableSsl = true,
        Username = "giselx",
        Password = password,
        FromAddress = "noreply@gandummas.co.id",
        FromName = "GiselX System",
        TimeZoneId = "SE Asia Standard Time"
    };

    private sealed class StubProtector : IEmailSecretProtector
    {
        public string? Protect(string? plainText) =>
            string.IsNullOrEmpty(plainText) ? null : "protected:" + plainText;

        public string? Unprotect(string? protectedText) =>
            protectedText is not null && protectedText.StartsWith("protected:", StringComparison.Ordinal)
                ? protectedText["protected:".Length..]
                : null;
    }

    private sealed class StubRepository(EmailSetting? stored) : IEmailSettingRepository
    {
        public EmailSetting? Saved { get; private set; }

        public Task<EmailSetting?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task SaveAsync(EmailSetting setting, CancellationToken cancellationToken)
        {
            Saved = setting;
            return Task.CompletedTask;
        }
    }
}
