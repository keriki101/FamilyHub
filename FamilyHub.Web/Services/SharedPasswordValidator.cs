using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FamilyHub.Web.Services;

public sealed class SharedPasswordValidator
{
    private const int Iterations = 600_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private readonly byte[] salt;
    private readonly byte[] passwordHash;

    public SharedPasswordValidator(string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4
            || parts[0] != "pbkdf2-sha256"
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations < Iterations)
        {
            throw new ArgumentException("The configured password hash is invalid.", nameof(encodedHash));
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            passwordHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The configured password hash is invalid.", nameof(encodedHash), exception);
        }

        if (salt.Length < SaltLength || passwordHash.Length != HashLength)
        {
            throw new ArgumentException("The configured password hash is invalid.", nameof(encodedHash));
        }

        this.IterationCount = iterations;
    }

    private int IterationCount { get; }

    public bool IsValid(string? candidate)
    {
        if (candidate is null || candidate.Length > 1024)
        {
            return false;
        }

        var candidateHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(candidate),
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashLength);
        return CryptographicOperations.FixedTimeEquals(passwordHash, candidateHash);
    }

    public static string CreateHash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length < 16)
        {
            throw new ArgumentException("The shared password must contain at least 16 characters.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength);

        return string.Join(
            '$',
            "pbkdf2-sha256",
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }
}
