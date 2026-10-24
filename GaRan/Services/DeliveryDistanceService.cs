using GaRan.Data;
using GaRan.Models;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Services;

public sealed class DeliveryDistanceService
{
    private readonly IConfiguration _configuration;
    private readonly GaRanDbContext _context;

    public DeliveryDistanceService(IConfiguration configuration, GaRanDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public async Task<string> GetStoreAddressAsync(CancellationToken cancellationToken = default)
    {
        var configuredAddress = await _context.CauHinhGiaoHangs.AsNoTracking()
            .Where(x => x.CauHinhGiaoHangID == 1)
            .Select(x => x.DiaChiCuaHang)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(configuredAddress)
            ? _configuration["Delivery:StoreAddress"] ?? string.Empty
            : configuredAddress.Trim();
    }

    public Task<decimal?> GetDistanceKmAsync(string destination, CancellationToken cancellationToken = default) =>
        GetDistanceKmAsync(destination, null, cancellationToken);

    public async Task<decimal?> GetDistanceKmAsync(
        string destination,
        string? approximateDestination,
        CancellationToken cancellationToken = default)
    {
        var origin = await GetStoreAddressAsync(cancellationToken);
        var deliveryArea = string.IsNullOrWhiteSpace(approximateDestination) ? destination : approximateDestination;
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(deliveryArea)) return null;

        var settings = await _context.CauHinhGiaoHangs.AsNoTracking()
            .Where(x => x.CauHinhGiaoHangID == 1)
            .Select(x => new { x.MienPhiDenKm, x.ToiDaKm, x.UocTinhCungPhuongKm, x.UocTinhKhacPhuongKm })
            .FirstOrDefaultAsync(cancellationToken);

        var maxDistance = settings?.ToiDaKm ?? _configuration.GetValue<decimal>("Delivery:MaxDistanceKm", 5m);
        var freeDistance = settings?.MienPhiDenKm ?? _configuration.GetValue<decimal>("Delivery:FreeDistanceKm", 3m);
        if (maxDistance <= 0) return null;

        var originArea = ExtractWard(origin);
        var destinationArea = ExtractWard(deliveryArea);
        var sameWard = !string.IsNullOrWhiteSpace(originArea) &&
                       string.Equals(originArea, destinationArea, StringComparison.OrdinalIgnoreCase);
        var estimate = sameWard
            ? settings?.UocTinhCungPhuongKm ?? Math.Min(freeDistance * 0.75m, maxDistance * 0.4m)
            : settings?.UocTinhKhacPhuongKm ?? maxDistance * 0.9m;
        return estimate > 0
            ? Math.Round(estimate, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string ExtractWard(string address)
    {
        var part = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => value.StartsWith("Phường ", StringComparison.OrdinalIgnoreCase) ||
                                     value.StartsWith("Xã ", StringComparison.OrdinalIgnoreCase) ||
                                     value.StartsWith("Đặc khu ", StringComparison.OrdinalIgnoreCase));
        if (part == null) return string.Empty;

        foreach (var prefix in new[] { "Phường ", "Xã ", "Đặc khu " })
        {
            if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                part = part[prefix.Length..].Trim();
                break;
            }
        }
        return part;
    }
}
