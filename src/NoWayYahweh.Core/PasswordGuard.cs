using System.Security.Cryptography;
using System.Text;

namespace NoWayYahweh.Core;

/// <summary>
/// Optional password held by someone the user trusts. Stored as a salted PBKDF2-SHA256 hash
/// ("pbkdf2$iterations$salt$hash"); the password itself is never stored. It adds friction, not
/// absolute security: the documented recovery (docs/RECUPERACAO.md) still works without it.
/// </summary>
public static class PasswordGuard
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    public const int MinLength = 6;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, Iterations);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored)
    {
        var parts = stored?.Split('$');
        if (parts is not ["pbkdf2", var iterText, var saltText, var hashText] || !int.TryParse(iterText, out var iterations))
            return false;
        try
        {
            var expected = Convert.FromBase64String(hashText);
            return CryptographicOperations.FixedTimeEquals(Derive(password, Convert.FromBase64String(saltText), iterations), expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
