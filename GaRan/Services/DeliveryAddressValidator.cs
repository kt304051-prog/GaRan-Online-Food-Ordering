namespace GaRan.Services;

public static class DeliveryAddressValidator
{
    public const string ErrorMessage = "Nhập số nhà kèm tên đường (ví dụ: 12 Nguyễn Trãi) hoặc tên đường đầy đủ.";

    public static bool IsSpecific(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;

        var value = address.Trim();
        if (value.Length > 150) return false;

        var letterCount = value.Count(char.IsLetter);
        var hasHouseNumber = value.Any(char.IsDigit);
        var wordCount = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        return letterCount >= 3 && (hasHouseNumber || wordCount >= 2);
    }
}
