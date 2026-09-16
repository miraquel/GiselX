using GiselX.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GiselX.Tests;

/// <summary>
/// The real protector, not the stub the service tests use — this is what decides whether reading the
/// database hands someone the mail password.
/// </summary>
public sealed class DataProtectionEmailSecretProtectorTests
{
    [Fact]
    public void A_password_round_trips()
    {
        var protector = Create();

        var stored = protector.Protect("Gmk@123!");

        Assert.NotNull(stored);
        Assert.Equal("Gmk@123!", protector.Unprotect(stored));
    }

    [Fact]
    public void The_stored_value_does_not_contain_the_password()
    {
        var protector = Create();

        var stored = protector.Protect("Gmk@123!")!;

        Assert.DoesNotContain("Gmk@123!", stored, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_protected_by_a_different_key_ring_is_unreadable_rather_than_fatal()
    {
        // What happens when a deployment wipes the key folder: the password stops being readable and
        // has to be entered again, but nothing throws.
        var stored = Create().Protect("Gmk@123!");

        Assert.Null(Create().Unprotect(stored));
    }

    [Fact]
    public void Blank_input_stores_nothing()
    {
        var protector = Create();

        Assert.Null(protector.Protect(null));
        Assert.Null(protector.Protect(""));
        Assert.Null(protector.Unprotect(null));
        Assert.Null(protector.Unprotect(""));
    }

    /// <summary>Each call builds an independent key ring, standing in for a separate installation.</summary>
    private static DataProtectionEmailSecretProtector Create() =>
        new(DataProtectionProvider.Create(
                new DirectoryInfo(Path.Combine(Path.GetTempPath(), "giselx-dp-" + Guid.NewGuid().ToString("N")))),
            NullLogger<DataProtectionEmailSecretProtector>.Instance);
}
