namespace GiselX.Service.Interface;

/// <summary>
/// Encrypts the SMTP password before it is stored. Implemented in the Web layer over ASP.NET Core
/// Data Protection, so the Service layer keeps no dependency on it.
///
/// The protected value is only readable by an application holding the same data-protection keys. If
/// those keys are lost — a deployment that wipes the key folder, or a different machine — the stored
/// password cannot be recovered and has to be entered again. It is never recoverable as plaintext by
/// reading the database alone, which is the point.
/// </summary>
public interface IEmailSecretProtector
{
    string? Protect(string? plainText);

    /// <summary>Returns null when the value cannot be decrypted, rather than throwing.</summary>
    string? Unprotect(string? protectedText);
}
