namespace VK.Web.Services;

public static class PasswordHasher
{
    // WorkFactor 12 = ~300ms per hash — đủ chậm để chống brute force
    // SHA-256 cũ: 10 tỷ hash/giây với GPU
    // BCrypt workFactor 12: ~4 hash/giây — không thể brute force thực tế
    private const int WorkFactor = 12;

    public static string Hash(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        return BCrypt.Net.BCrypt.HashPassword(plainText, WorkFactor);
    }

    public static bool Verify(string plainText, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(plainText, storedHash);
    }
}