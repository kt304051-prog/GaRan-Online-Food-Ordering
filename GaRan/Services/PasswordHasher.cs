namespace GaRan.Services;

public static class PasswordHasher
{
    private static readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> Hasher = new();

    public static string Hash(string password)
    {
        return Hasher.HashPassword(new object(), password);
    }

    public static bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;
        if (IsLegacyHash(hash))
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(bytes, Convert.FromHexString(hash));
        }

        try
        {
            return Hasher.VerifyHashedPassword(new object(), hash, password) !=
                   Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool NeedsRehash(string hash) => IsLegacyHash(hash);

    private static bool IsLegacyHash(string hash) => hash.Length == 64 && hash.All(Uri.IsHexDigit);
}
