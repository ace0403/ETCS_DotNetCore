/*
Deploy on MealDB database.

Meal order summary from [Order] and [OrderItem] for admin reporting.
Student details (card no., grade, name) are enriched in application code from ibonus.

Pagination:
  @Start   = zero-based row offset (DataTables start)
  @Length  = page size; 0 or NULL returns all rows (export)

Outputs:
  @TotalCount = total rows for current filters
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spMealOrderSummary_MealDB_New]
    @startdate AS DATETIME,
    @enddate AS DATETIME,
    @SchoolId AS VARCHAR(10) = '',
    @Start AS INT = 0,
    @Length AS INT = 0,
    @TotalCount AS INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Start = ISNULL(@Start, 0);
    IF (@Start < 0) SET @Start = 0;

    SET @SchoolId = LTRIM(RTRIM(ISNULL(@SchoolId, '')));
    DECLARE @SchoolIdInt INT = NULL;
    IF (@SchoolId <> '' AND @SchoolId <> 'All')
        SET @SchoolIdInt = TRY_CAST(@SchoolId AS INT);

    DECLARE @RangeStart DATETIME = CAST(CAST(@startdate AS DATE) AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = DATEADD(DAY, 1, CAST(CAST(@enddate AS DATE) AS DATETIME));

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;

    CREATE TABLE #TMP (
        OrderDate DATETIME NOT NULL,
        StudentId INT NOT NULL,
        PaymentStatus NVARCHAR(50) NOT NULL,
        Category NVARCHAR(100) NULL,
        Choice NVARCHAR(250) NULL,
        DeliveryDate DATETIME NULL,
        [Day] NVARCHAR(20) NULL,
        Items NVARCHAR(500) NULL,
        SortOrderDate DATETIME NOT NULL,
        SortOrderId INT NOT NULL,
        SortOrderItemId INT NOT NULL
    );

    INSERT INTO #TMP (
        OrderDate,
        StudentId,
        PaymentStatus,
        Category,
        Choice,
        DeliveryDate,
        [Day],
        Items,
        SortOrderDate,
        SortOrderId,
        SortOrderItemId
    )
    SELECT
        o.OrderDate,
        o.StudentId,
        CASE WHEN ISNULL(o.IsPaid, 0) = 1 THEN 'PAID' ELSE 'PENDING' END,
        LTRIM(RTRIM(ISNULL(COALESCE(mt_pkg.EnumValue, mt_item.EnumValue), ''))),
        LTRIM(RTRIM(ISNULL(
            CASE
                WHEN oi.PackageId IS NOT NULL THEN mp.PackageName
                ELSE mc_item.EnumValue
            END, ''))),
        oi.MealDate,
        LTRIM(RTRIM(ISNULL(DATENAME(weekday, oi.MealDate), ''))),
        LTRIM(RTRIM(ISNULL(COALESCE(pkg_items.ItemsName, mi.ItemName), ''))),
        o.OrderDate,
        o.Id,
        oi.Id
    FROM [Order] o
    INNER JOIN [OrderItem] oi ON oi.OrderId = o.Id
    LEFT JOIN [MealPackages] mp ON mp.Id = oi.PackageId
    LEFT JOIN [MealItem] mi ON mi.Id = oi.ItemId
    LEFT JOIN Enums mt_pkg ON mp.MealTypeId = mt_pkg.Id
    LEFT JOIN Enums mt_item ON mi.MealTypeId = mt_item.Id
    LEFT JOIN Enums mc_item ON mi.MealCategotyId = mc_item.Id
    OUTER APPLY (
        SELECT STRING_AGG(LTRIM(RTRIM(mi2.ItemName)), ', ') AS ItemsName
        FROM MealPackageItems mpi
        INNER JOIN MealItem mi2 ON mpi.MealItemId = mi2.Id
        WHERE mpi.MealPackageId = oi.PackageId
    ) pkg_items
    WHERE o.OrderTypeId IN (24, 42)
      AND oi.MealDate >= @RangeStart
      AND oi.MealDate < @RangeEndExclusive
      AND (@SchoolIdInt IS NULL OR COALESCE(mp.SchoolId, mi.SchoolId) = @SchoolIdInt)
    OPTION (RECOMPILE);

    CREATE CLUSTERED INDEX CX_MealOrderSummary_MealDB
        ON #TMP (SortOrderDate ASC, SortOrderId ASC, SortOrderItemId ASC);

    SELECT @TotalCount = COUNT(*)
    FROM #TMP;

    IF (@Length IS NULL OR @Length <= 0)
    BEGIN
        SELECT
            OrderDate,
            StudentId,
            PaymentStatus,
            Category,
            Choice,
            DeliveryDate,
            [Day],
            Items
        FROM #TMP
        ORDER BY SortOrderDate ASC, SortOrderId ASC, SortOrderItemId ASC;
    END
    ELSE
    BEGIN
        SELECT
            OrderDate,
            StudentId,
            PaymentStatus,
            Category,
            Choice,
            DeliveryDate,
            [Day],
            Items
        FROM #TMP
        ORDER BY SortOrderDate ASC, SortOrderId ASC, SortOrderItemId ASC
        OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;
    END

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;
END
GO
