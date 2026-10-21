using GiselX.Service.Interface;
using Microsoft.AspNetCore.DataProtection;

namespace GiselX.Web;

/// <summary>
/// Encrypts the stored SMTP password with ASP.NET Core Data Protection, so reading the database does
/// not hand anyone the mail credentials.
///
/// The keys live wherever Data Protection is configured to keep them. If a deployment replaces that
/// folder the stored password stops being readable — recoverable by re-entering it on the screen,
/// but worth knowing, which is why the failure is logged rather than swallowed silently.
/// </summary>
public sealed class DataProtectionEmailSecretProtector : IEmailSecretProtector
{
    private const string Purpose = "GiselX.EmailSettings.Password.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionEmailSecretProtector> _logger;

    public DataProtectionEmailSecretProtector(IDataProtectionProvider provider,
        ILogger<DataProtectionEmailSecretProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string? Protect(string? plainText) =>
        string.IsNullOrEmpty(plainText) ? null : _protector.Protect(plainText);

    public string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return null;

        try
        {
            return _protector.Unprotect(protectedText);
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            _logger.LogError(exception,
                "The stored SMTP password could not be decrypted; the data-protection keys that " +
                "encrypted it are unavailable. Re-enter the password on the Email Settings screen.");
            return null;
        }
    }
}
