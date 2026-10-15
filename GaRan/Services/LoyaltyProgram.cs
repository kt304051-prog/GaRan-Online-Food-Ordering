namespace GaRan.Services;

public static class LoyaltyProgram
{
    public const int VipLifetimePoints = 500;
    public const decimal VipDiscountPercent = 5m;

    public static bool IsVip(int lifetimePoints) => lifetimePoints >= VipLifetimePoints;
    public static int PointsToVip(int lifetimePoints) => Math.Max(0, VipLifetimePoints - lifetimePoints);
}
