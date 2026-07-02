namespace ETCS.Shared.Helpers;

public static class MealPackagePricing
{
    public static decimal GetTotalPrice(decimal price, decimal processingFee) => price + processingFee;
}
