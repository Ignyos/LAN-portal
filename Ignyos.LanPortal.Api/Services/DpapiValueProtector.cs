using System.Security.Cryptography;
using System.Text;

namespace Ignyos.LanPortal.Api.Services;

public sealed class DpapiValueProtector : IValueProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Ignyos.LanPortal.v1");

    public string Protect(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        if (!OperatingSystem.IsWindows())
        {
            return $"plain:{plainText}";
        }

        try
        {
            var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.LocalMachine);
            return $"enc:{Convert.ToBase64String(protectedBytes)}";
        }
        catch (PlatformNotSupportedException)
        {
            return $"plain:{plainText}";
        }
    }

    public string Unprotect(string protectedText)
    {
        if (protectedText.StartsWith("enc:", StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new InvalidOperationException("Encrypted settings cannot be decrypted on non-Windows platforms.");
            }

            var payload = protectedText[4..];
            var protectedBytes = Convert.FromBase64String(payload);

            try
            {
                var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("Unable to decrypt stored setting value.");
            }
        }

        if (protectedText.StartsWith("plain:", StringComparison.Ordinal))
        {
            return protectedText[6..];
        }

        return protectedText;
    }
}
