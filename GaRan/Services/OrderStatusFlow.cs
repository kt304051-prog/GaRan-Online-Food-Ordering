namespace GaRan.Services;

public static class OrderStatusFlow
{
    private static readonly IReadOnlyDictionary<string, string[]> NextStatuses = new Dictionary<string, string[]>
    {
        ["Chờ xác nhận"] = ["Đã xác nhận", "Đã hủy"],
        ["Đã xác nhận"] = ["Đang chế biến", "Đã hủy"],
        ["Đang chế biến"] = ["Chờ giao hàng", "Đã hủy"],
        ["Chờ giao hàng"] = ["Đang giao", "Đã hủy"],
        ["Đang giao"] = ["Hoàn thành", "Đã hủy"],
        ["Hoàn thành"] = [],
        ["Đã hủy"] = []
    };

    public static bool CanTransition(string currentStatus, string nextStatus) =>
        string.Equals(currentStatus, nextStatus, StringComparison.Ordinal) ||
        NextStatuses.TryGetValue(currentStatus, out var allowed) && allowed.Contains(nextStatus, StringComparer.Ordinal);

    public static IReadOnlyList<string> GetNextStatuses(string currentStatus) =>
        NextStatuses.TryGetValue(currentStatus, out var allowed) ? allowed : [];
}
