/*
Deploy on MealDB. Meal order summary.

School scope:
  @SchoolCodesCsv / @SchoolIdsCsv = comma-separated filters for scoped multi-school users.
  Empty CSV + empty single-school param = all schools (unrestricted admin only).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

--EXEC [spMealOrderSummary_MealDB_New] '2026-08-26','2026-08-28','','','',0,20,0
CREATE OR ALTER PROCEDURE [dbo].[spMealOrderSummary_MealDB_New]
    @startdate AS DATETIME,
    @enddate AS DATETIME,
    @SchoolId AS VARCHAR(10) = '',
    @SchoolIdsCsv AS VARCHAR(MAX) = '',
    @MealSessionId AS INT = 0,
    @MealTypeId AS INT = 0,
    @Start AS INT = 0,
    @Length AS INT = 0,
    @TotalCount AS INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Start = ISNULL(@Start, 0);
    IF (@Start < 0) SET @Start = 0;

    SET @SchoolId = LTRIM(RTRIM(ISNULL(@SchoolId, '')));
    SET @SchoolIdsCsv = LTRIM(RTRIM(ISNULL(@SchoolIdsCsv, '')));
    DECLARE @SchoolIdInt INT = NULL;
    IF (@SchoolId <> '' AND @SchoolId <> 'All')
        SET @SchoolIdInt = TRY_CAST(@SchoolId AS INT);

    SET @MealSessionId = ISNULL(@MealSessionId, 0);
    SET @MealTypeId = ISNULL(@MealTypeId, 0);

    DECLARE @RangeStart DATETIME = CAST(CAST(@startdate AS DATE) AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = CAST(CAST(@enddate AS DATE) AS DATETIME);

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;

    CREATE TABLE #TMP (
        OrderDate DATETIME NOT NULL,
        StudentId INT NOT NULL,
        PaymentStatus NVARCHAR(50) NOT NULL,
        MealSession NVARCHAR(100) NULL,
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
        MealSession,
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
        LTRIM(RTRIM(ISNULL(COALESCE(ms_pkg.EnumValue, ms_item.EnumValue), ''))),
        LTRIM(RTRIM(ISNULL(COALESCE(mt_pkg.EnumValue, mt_item.EnumValue), ''))),
        LTRIM(RTRIM(ISNULL(
            CASE
                WHEN oi.PackageId IS NOT NULL THEN mp.PackageName
                ELSE mc_item.EnumValue
            END, ''))),
        oi.MealDate,
        LTRIM(RTRIM(ISNULL(DATENAME(weekday, oi.MealDate), ''))),
        LTRIM(RTRIM(ISNULL(COALESCE(pkg_items.ItemsName, mi.ItemName), ''))),
        oi.MealDate,
        o.Id,
        oi.Id
    FROM [Order] o
    INNER JOIN [OrderItem] oi ON oi.OrderId = o.Id
    LEFT JOIN [MealPackages] mp ON mp.Id = oi.PackageId
    LEFT JOIN [MealItem] mi ON mi.Id = oi.ItemId
    LEFT JOIN Enums ms_pkg ON mp.MealSessionId = ms_pkg.Id
    LEFT JOIN Enums ms_item ON mi.MealSessionId = ms_item.Id
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
      AND ISNULL(o.IsPaid, 0) = 1
      AND CAST(oi.MealDate AS DATE) between @RangeStart and @RangeEndExclusive
      AND (
            (@SchoolIdsCsv <> '' AND (
                (mp.Id IS NOT NULL AND mp.SchoolId IN (SELECT TRY_CAST(sc.value AS INT) FROM dbo.fnSplitCsv(@SchoolIdsCsv) sc WHERE TRY_CAST(sc.value AS INT) IS NOT NULL))
                OR (oi.ItemId IS NOT NULL AND (
                    EXISTS (SELECT 1 FROM MealItemSchools mis WHERE mis.MealItemId = mi.Id AND mis.SchoolId IN (SELECT TRY_CAST(sc.value AS INT) FROM dbo.fnSplitCsv(@SchoolIdsCsv) sc WHERE TRY_CAST(sc.value AS INT) IS NOT NULL))
                    OR (NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id) AND mi.SchoolId IN (SELECT TRY_CAST(sc.value AS INT) FROM dbo.fnSplitCsv(@SchoolIdsCsv) sc WHERE TRY_CAST(sc.value AS INT) IS NOT NULL))
                ))
            ))
            OR (@SchoolIdsCsv = '' AND (@SchoolIdInt IS NULL
           OR (mp.Id IS NOT NULL AND mp.SchoolId = @SchoolIdInt)
           OR (oi.ItemId IS NOT NULL AND (
               EXISTS (SELECT 1 FROM MealItemSchools mis WHERE mis.MealItemId = mi.Id AND mis.SchoolId = @SchoolIdInt)
               OR (NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id) AND mi.SchoolId = @SchoolIdInt)
           ))))
          )
      AND (@MealSessionId <= 0
           OR COALESCE(mp.MealSessionId, mi.MealSessionId) = @MealSessionId)
      AND (@MealTypeId <= 0
           OR COALESCE(mp.MealTypeId, mi.MealTypeId) = @MealTypeId)
    OPTION (RECOMPILE);

    CREATE CLUSTERED INDEX CX_MealOrderSummary_MealDB
        ON #TMP (SortOrderDate DESC, OrderDate DESC);

    SELECT @TotalCount = COUNT(*)
    FROM #TMP;

    IF (@Length IS NULL OR @Length <= 0)
    BEGIN
        SELECT
            OrderDate,
            StudentId,
            PaymentStatus,
            MealSession,
            Category,
            Choice,
            DeliveryDate,
            [Day],
            Items
        FROM #TMP
        ORDER BY SortOrderDate DESC, OrderDate DESC;
    END
    ELSE
    BEGIN
        SELECT
            OrderDate,
            StudentId,
            PaymentStatus,
            MealSession,
            Category,
            Choice,
            DeliveryDate,
            [Day],
            Items
        FROM #TMP
        ORDER BY SortOrderDate DESC, OrderDate DESC
        OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;
    END

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;
END
GO