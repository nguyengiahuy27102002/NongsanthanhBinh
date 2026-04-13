using System.Security.Cryptography;

namespace DuongVanDung.WebApp.Services.Auth;

public sealed class PasswordHashService : IPasswordHashService
{
    private const int IterationCount = 100_000;
    private const int HashSize = 32;

    public bool Verify(string password, string saltBase64, string expectedHashBase64)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(saltBase64) ||
            string.IsNullOrWhiteSpace(expectedHashBase64))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(saltBase64);
            expectedHash = Convert.FromBase64String(expectedHashBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashSize);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
