using ETCS.Shared.Helpers;

namespace ETCS.Shared.Infrastructure.Meals;

public static class MealDtoMapper
{
    public static MealItemSlimDto ToSlim(MealItemDto item) =>
        new()
        {
            Id = item.Id,
            ItemName = item.ItemName,
            MealTypeName = item.MealTypeName,
            Detail = item.Detail,
            Price = item.Price,
            NutritionList = item.NutritionList,
            StudentAllergies = item.StudentAllergies
        };

    public static MealPackageSlimDto ToSlim(MealPackageDto package) =>
        new()
        {
            Id = package.Id,
            PackageName = package.PackageName,
            MealTypeName = package.MealTypeName,
            Detail = package.Detail,
            ItemsName = package.ItemsName,
            Price = MealPackagePricing.GetTotalPrice(package.Price, package.ProcessingFee),
            ProcessingFee = package.ProcessingFee,
            NutritionList = package.NutritionList,
            StudentAllergies = package.StudentAllergies
        };
}
