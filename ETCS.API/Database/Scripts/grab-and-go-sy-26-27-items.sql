/*
Grab and Go SY 26-27 — Meal Item insert script
Source: GRAB AND GO SY 26-27.xlsx (Snack Menu sheet) + snacks menu image (Dairy & Drinks)
Categories: Sandwiches, Bakery & Dessert, Dairy & Drinks, Salad
Pricing: Snacks menu image
Excluded: Rice Bowl items

Deploy on MealDB. Execute manually — do NOT run from automated tooling.

Before running:
  1. Set @SchoolId and @CreatedBy below.
  2. Review order types / weeks / days if your school differs.
  3. Re-run is safe only when item names do not already exist for the same school + meal type.
*/
USE MealDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SchoolId           INT = 2;    -- TODO: target school id
DECLARE @CreatedBy          INT = 1;    -- TODO: admin user id

DECLARE @MealTypeBreakfast  INT = 25;   -- Breakfast
DECLARE @CategorySandwiches     INT;
DECLARE @CategoryBakeryDessert  INT;
DECLARE @CategoryDairyDrinks    INT;
DECLARE @CategorySalad          INT;
DECLARE @NutritionEnergy    INT = 44;   -- Energy
DECLARE @MeasureKcal        INT = 48;   -- kcal
DECLARE @OrderTypeMealPlan  INT = 24;   -- Meal Plans
DECLARE @OrderTypeAlaCarte  INT = 42;   -- A La Carte
DECLARE @NextEnumId         INT;

-- Ensure meal categories exist (EnumTypeId = 10)
IF NOT EXISTS (SELECT 1 FROM dbo.Enums WHERE EnumTypeId = 10 AND LTRIM(RTRIM(EnumValue)) = N'Sandwiches')
BEGIN
    SET @NextEnumId = (SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Enums);
    INSERT INTO dbo.Enums (Id, EnumValue, Description, EnumTypeId, SortOrder, IsDeletable, IsEditable, IsActive, CreatedBy, CreatedOn)
    VALUES (@NextEnumId, N'Sandwiches', N'Sandwiches', 10, 13, 1, 1, 1, @CreatedBy, GETUTCDATE());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Enums WHERE EnumTypeId = 10 AND LTRIM(RTRIM(EnumValue)) = N'Bakery & Dessert')
BEGIN
    SET @NextEnumId = (SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Enums);
    INSERT INTO dbo.Enums (Id, EnumValue, Description, EnumTypeId, SortOrder, IsDeletable, IsEditable, IsActive, CreatedBy, CreatedOn)
    VALUES (@NextEnumId, N'Bakery & Dessert', N'Bakery & Dessert', 10, 14, 1, 1, 1, @CreatedBy, GETUTCDATE());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Enums WHERE EnumTypeId = 10 AND LTRIM(RTRIM(EnumValue)) = N'Dairy & Drinks')
BEGIN
    SET @NextEnumId = (SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Enums);
    INSERT INTO dbo.Enums (Id, EnumValue, Description, EnumTypeId, SortOrder, IsDeletable, IsEditable, IsActive, CreatedBy, CreatedOn)
    VALUES (@NextEnumId, N'Dairy & Drinks', N'Dairy & Drinks', 10, 15, 1, 1, 1, @CreatedBy, GETUTCDATE());
END;

SET @CategorySandwiches = (
    SELECT TOP (1) Id FROM dbo.Enums
    WHERE EnumTypeId = 10 AND LTRIM(RTRIM(EnumValue)) = N'Sandwiches'
);
SET @CategoryBakeryDessert = (
    SELECT TOP (1) Id FROM dbo.Enums
    WHERE EnumTypeId = 10 AND LTRIM(RTRIM(EnumValue)) = N'Bakery & Dessert'
);
SET @CategoryDairyDrinks = (
    SELECT TOP (1) Id FROM dbo.Enums
    WHERE EnumTypeId = 10 AND LTRIM(RTRIM(EnumValue)) = N'Dairy & Drinks'
);
SET @CategorySalad = (
    SELECT TOP (1) Id FROM dbo.Enums
    WHERE EnumTypeId = 10 AND LTRIM(RTRIM(EnumValue)) IN (N'Salad', N'Salads')
);

IF OBJECT_ID('tempdb..#GrabAndGoItems') IS NOT NULL
    DROP TABLE #GrabAndGoItems;

CREATE TABLE #GrabAndGoItems
(
    ItemName        NVARCHAR(200)  NOT NULL,
    Detail          NVARCHAR(2000) NOT NULL,
    Price           DECIMAL(18, 2) NOT NULL,
    MealCategoryId  INT            NOT NULL,
    EnergyKcal      DECIMAL(18, 2) NOT NULL,
    IngredientIds   NVARCHAR(200)  NULL
);

INSERT INTO #GrabAndGoItems (ItemName, Detail, Price, MealCategoryId, EnergyKcal, IngredientIds)
VALUES
    -- Sandwiches — 14.00 AED
    (N'Chicken Fajita Wrap',                              N'160-180 grams', 14.00, @CategorySandwiches,    200, N'56,9'),
    (N'Turkey Cheese Sandwich',                           N'160-180 grams', 14.00, @CategorySandwiches,    255, N'56,9'),
    (N'Baked Falafel Wrap',                               N'160-180 grams', 14.00, @CategorySandwiches,    230, N'56,9'),
    (N'Buffalo Chicken Wrap',                             N'160-180 grams', 14.00, @CategorySandwiches,    200, N'56,55'),
    (N'BBQ Chicken in Samoon Bread',                      N'160-180 grams', 14.00, @CategorySandwiches,    245, N'55,56'),
    (N'Grilled Chicken in Panini Bread with BBQ Sauce',   N'160-180 grams', 14.00, @CategorySandwiches,    245, N'55,56'),
    (N'Chicken Francisco in Samoon Bread',                N'160-180 grams', 14.00, @CategorySandwiches,    235, N'9,56,13'),
    (N'ETCS Chicken Delight Burger',                      N'160-180 grams', 14.00, @CategorySandwiches,    240, N'56,9'),
    (N'Chicken Caesar Baquettes',                         N'160-180 grams', 14.00, @CategorySandwiches,    225, N'56,9'),
    (N'Triple Decker Turkey Club Sandwiches',             N'160-180 grams', 14.00, @CategorySandwiches,    240, N'9,56,13'),

    -- Bakery & Dessert — 8.00 AED
    (N'Cinnamon Swirl',                                    N'85 grams',   8.00, @CategoryBakeryDessert, 245, N'56,9,13'),
    (N'Whole Wheat Vegetable Pizza',                        N'60 grams',   8.00, @CategoryBakeryDessert, 135, N'56,9,13'),
    (N'Whole Wheat Chicken Pizza',                          N'60 grams',   8.00, @CategoryBakeryDessert, 125, N'56,9,13'),
    (N'Cheese Manakish',                                    N'60 grams',   8.00, @CategoryBakeryDessert, 100, N'56,9'),
    (N'Mini Cereals Crossaints',                            N'60 grams',   8.00, @CategoryBakeryDessert, 105, N'56,13,9'),
    (N'Vanilla Muffin',                                     N'85 grams',   8.00, @CategoryBakeryDessert, 225, N'56,9,13'),
    (N'Baked Chicken Roll',                                 N'100 grams',  8.00, @CategoryBakeryDessert, 115, N'56,9,13'),
    (N'Cheese and Tomato Twist',                            N'85 grams',   8.00, @CategoryBakeryDessert, 105, N'56,9,13'),
    (N'Whole Wheat Muffin',                                 N'85 grams',   8.00, @CategoryBakeryDessert, 215, N'56,9,13'),
    (N'Blueberry Muffins',                                  N'85 grams',   8.00, @CategoryBakeryDessert, 230, N'56,9,13'),
    (N'Red Cherry Muffins',                                 N'85 grams',   8.00, @CategoryBakeryDessert, 230, N'56,9,13'),
    (N'Banana Cake',                                        N'70 grams',   8.00, @CategoryBakeryDessert, 155, N'56,9,13'),

    -- Salad — 12.00 AED
    (N'Hummus with Bread',                                  N'100 grams', 12.00, @CategorySalad, 165, N'57,56'),
    (N'Fruit Salad',                                        N'150 grams', 12.00, @CategorySalad,  60, NULL),
    (N'Vines Salad',                                        N'150 grams', 12.00, @CategorySalad, 155, NULL),
    (N'Chicken Caesar Salad',                               N'150 grams', 12.00, @CategorySalad, 120, N'56,9,13'),
    (N'Fattoush Salad',                                     N'150 grams', 12.00, @CategorySalad,  80, N'56'),
    (N'Oriental Salad',                                     N'150 grams', 12.00, @CategorySalad,  70, NULL),
    (N'Greek Salad',                                        N'150 grams', 12.00, @CategorySalad,  80, N'9'),

    -- Dairy & Drinks — from snacks menu image (not in Excel)
    (N'Fresh Orange Juice',                                 N'200 ml',    10.00, @CategoryDairyDrinks,  55, NULL),
    (N'Fresh Lemon & Mint Juice',                           N'200 ml',    10.00, @CategoryDairyDrinks,  30, NULL),
    (N'Fresh Watermelon Juice',                             N'200 ml',    10.00, @CategoryDairyDrinks,  45, NULL),
    (N'Avocado Mango Smoothie',                             N'200 ml',     8.00, @CategoryDairyDrinks, 175, N'9'),
    (N'Low Fat Milk',                                       N'250 ml',     3.50, @CategoryDairyDrinks,  41, N'9'),
    (N'Low Fat Laban Up',                                   N'200 ml',     3.00, @CategoryDairyDrinks,  78, N'9'),
    (N'Flavored Yogurt',                                    N'100 grams',  4.50, @CategoryDairyDrinks,  85, N'9'),
    (N'Bottled Juice',                                      N'200 ml',     3.50, @CategoryDairyDrinks,   0, NULL),
    (N'Flavored Milk',                                      N'200 ml',     3.50, @CategoryDairyDrinks,  42, N'9');

DECLARE
    @ItemName       NVARCHAR(200),
    @Detail         NVARCHAR(2000),
    @Price          DECIMAL(18, 2),
    @MealCategoryId INT,
    @EnergyKcal     DECIMAL(18, 2),
    @IngredientIds  NVARCHAR(200),
    @ItemId         INT,
    @IngredientId   INT;

DECLARE item_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT ItemName, Detail, Price, MealCategoryId, EnergyKcal, IngredientIds
FROM #GrabAndGoItems
ORDER BY ItemName;

OPEN item_cursor;
FETCH NEXT FROM item_cursor
INTO @ItemName, @Detail, @Price, @MealCategoryId, @EnergyKcal, @IngredientIds;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.MealItem mi
        WHERE ISNULL(mi.IsDeleted, 0) = 0
          AND mi.SchoolId = @SchoolId
          AND mi.MealTypeId = @MealTypeBreakfast
          AND LTRIM(RTRIM(mi.ItemName)) = LTRIM(RTRIM(@ItemName))
    )
    BEGIN
        PRINT N'Skipped (already exists): ' + @ItemName;
    END
    ELSE
    BEGIN
        BEGIN TRAN;

        INSERT INTO dbo.MealItem
        (
            SchoolId, MealTypeId, MealCategotyId, ItemName, Detail, Price, ImageName,
            IsActive, IsDeleted, CreatedBy, CreatedOn
        )
        VALUES
        (
            @SchoolId, @MealTypeBreakfast, @MealCategoryId, @ItemName, @Detail, @Price, N'',
            1, 0, @CreatedBy, GETUTCDATE()
        );

        SET @ItemId = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO dbo.MealItemNutrition (MealItemId, NutritionId, MeasureValue, MeasureTypeId, CreatedBy, CreatedOn)
        VALUES (@ItemId, @NutritionEnergy, @EnergyKcal, @MeasureKcal, @CreatedBy, GETUTCDATE());

        IF @IngredientIds IS NOT NULL AND LTRIM(RTRIM(@IngredientIds)) <> N''
        BEGIN
            DECLARE ingredient_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT DISTINCT TRY_CAST(LTRIM(RTRIM(value)) AS INT)
            FROM STRING_SPLIT(@IngredientIds, ',')
            WHERE TRY_CAST(LTRIM(RTRIM(value)) AS INT) IS NOT NULL;

            OPEN ingredient_cursor;
            FETCH NEXT FROM ingredient_cursor INTO @IngredientId;

            WHILE @@FETCH_STATUS = 0
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.MealItemIngredients
                    WHERE MealItemId = @ItemId AND IngredientId = @IngredientId
                )
                BEGIN
                    INSERT INTO dbo.MealItemIngredients (MealItemId, IngredientId, CreatedOn)
                    VALUES (@ItemId, @IngredientId, GETUTCDATE());
                END;

                FETCH NEXT FROM ingredient_cursor INTO @IngredientId;
            END;

            CLOSE ingredient_cursor;
            DEALLOCATE ingredient_cursor;
        END;

        INSERT INTO dbo.MealItemWeeks (MealItemId, WeekNo, CreatedOn)
        VALUES
            (@ItemId, 1, GETUTCDATE()),
            (@ItemId, 2, GETUTCDATE()),
            (@ItemId, 3, GETUTCDATE()),
            (@ItemId, 4, GETUTCDATE());

        INSERT INTO dbo.MealItemDays (MealItemId, DayId, CreatedOn)
        VALUES
            (@ItemId, 27, GETUTCDATE()), -- Monday
            (@ItemId, 28, GETUTCDATE()), -- Tuesday
            (@ItemId, 29, GETUTCDATE()), -- Wednesday
            (@ItemId, 30, GETUTCDATE()), -- Thursday
            (@ItemId, 31, GETUTCDATE()); -- Friday

        INSERT INTO dbo.MealItemOrderTypes (MealItemId, OrderTypeId, CreatedOn)
        VALUES
            (@ItemId, @OrderTypeMealPlan, GETUTCDATE()),
            (@ItemId, @OrderTypeAlaCarte, GETUTCDATE());

        IF OBJECT_ID(N'dbo.MealItemSchools', N'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.MealItemSchools (MealItemId, SchoolId, CreatedOn)
            SELECT @ItemId, @SchoolId, GETUTCDATE()
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.MealItemSchools mis
                WHERE mis.MealItemId = @ItemId
                  AND mis.SchoolId = @SchoolId
            );
        END;

        COMMIT TRAN;

        PRINT N'Inserted: ' + @ItemName + N' (Id=' + CAST(@ItemId AS NVARCHAR(20)) + N')';
    END;

    FETCH NEXT FROM item_cursor
    INTO @ItemName, @Detail, @Price, @MealCategoryId, @EnergyKcal, @IngredientIds;
END;

CLOSE item_cursor;
DEALLOCATE item_cursor;

DROP TABLE #GrabAndGoItems;

PRINT N'Done.';
GO
