using System.Security.Cryptography;

namespace ProjectPulse.Api;

// LOCAL DEVELOPMENT ONLY. Passwords use salted PBKDF2 hashes, never reversible encryption.
public static class LocalAuth
{
    private const int Iterations = 210_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashLength);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password, string stored)
    {
        try
        {
            var segments = stored.Split('$');
            if (segments.Length != 4 || segments[0] != "pbkdf2-sha256" || !int.TryParse(segments[1], out var iterations) || iterations < 100_000 || iterations > 1_000_000) return false;
            var salt = Convert.FromBase64String(segments[2]);
            var expected = Convert.FromBase64String(segments[3]);
            if (salt.Length != SaltLength || expected.Length != HashLength) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
