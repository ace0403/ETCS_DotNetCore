namespace ETCS.Shared.Media;

public static class MealImagePaths
{
    public const string MealItemFolder = "meal-image";
    public const string MealComboFolder = "meal-combo";
    public const string ThumbSubFolder = "thumb";

    public const string MealItemDefaultThumbnail = "/images/meal-default.jpeg";
    public const string MealComboDefaultThumbnail = "/images/meal-default.jpeg";
    public const string PosDefaultThumbnail = "/images/meal-default.png";

    public static string GetFolder(MealImageKind kind) =>
        kind == MealImageKind.MealCombo ? MealComboFolder : MealItemFolder;

    public static string GetDefaultThumbnailPath(MealImageKind kind, bool forPos = false) =>
        forPos ? PosDefaultThumbnail : MealItemDefaultThumbnail;
}
