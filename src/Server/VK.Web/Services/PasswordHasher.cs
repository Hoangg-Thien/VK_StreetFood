using System.Security.Cryptography;
using System.Text;

namespace VK.Web.Services;

public static class PasswordHasher
{
    public static string Hash(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string plainText, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        var computed = Hash(plainText);
        return string.Equals(computed, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
