namespace ETCS.Shared.Helpers;

public static class TopupAmountRules
{
    public static bool MeetsMinimum(decimal amount, decimal? minimumTopup) =>
        amount > 0 && (minimumTopup is null or <= 0 || amount >= minimumTopup.Value);
}
