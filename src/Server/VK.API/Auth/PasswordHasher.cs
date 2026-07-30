using System.Security.Cryptography;
using System.Text;

namespace VK.API.Auth;

/// <summary>
/// PBKDF2-SHA256 password hashing helper.
/// Format: base64(salt) + ":" + base64(hash)
/// Uses 100,000 iterations — tuned for a web API; adjust upward for higher-security contexts.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;       // 128-bit salt
    private const int KeySize = 32;        // 256-bit derived key
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>Hashes a plain-text password. Store the returned string in User.PasswordHash.</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            Algorithm,
            KeySize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>Returns true when <paramref name="password"/> matches <paramref name="storedHash"/>.</summary>
    public static bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expectedHash = Convert.FromBase64String(parts[1]);
        }
        catch
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            Algorithm,
            KeySize);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
