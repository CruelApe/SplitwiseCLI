using System.Security.Cryptography;
using System.Text;

namespace SplitwiseCLI.Configuration;

// Encrypts secrets at rest with Windows DPAPI, scoped to the current user account,
// so the persisted config file never holds the Splitwise API key in plaintext.
public static class SecretProtector
{
    public static string Protect(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string protectedBase64)
    {
        var protectedBytes = Convert.FromBase64String(protectedBase64);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
