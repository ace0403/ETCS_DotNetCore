/*
Deploy on ibonus. Legacy meal order summary.

School scope:
  @SchoolCodesCsv / @SchoolIdsCsv = comma-separated filters for scoped multi-school users.
  Empty CSV + empty single-school param = all schools (unrestricted admin only).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spMealOrderSummary_New]
    @startdate AS DATETIME,
    @enddate AS DATETIME,
    @SchoolId AS VARCHAR(10) = '',
    @SchoolIdsCsv AS VARCHAR(MAX) = '',
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
    IF (@SchoolId = '' OR @SchoolId = 'All')
        SET @SchoolId = NULL;

    DECLARE @RangeStart DATETIME = CAST(CAST(@startdate AS DATE) AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = CAST(CAST(@enddate AS DATE) AS DATETIME);;

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;

    CREATE TABLE #TMP (
        OrderDate DATETIME NOT NULL,
        StudCode NVARCHAR(50) NULL,
        StudStd NVARCHAR(50) NULL,
        StudDiv NVARCHAR(50) NULL,
        StudFullName NVARCHAR(401) NULL,
        PaymentStatus NVARCHAR(50) NULL,
        Category NVARCHAR(50) NULL,
        Choice NVARCHAR(50) NULL,
        DeliveryDate DATETIME NULL,
        [Day] NVARCHAR(20) NULL,
        Items NVARCHAR(250) NULL,
        SortOrderDate DATETIME NOT NULL,
        SortOrderId BIGINT NOT NULL
    );

    INSERT INTO #TMP (
        OrderDate,
        StudCode,
        StudStd,
        StudDiv,
        StudFullName,
        PaymentStatus,
        Category,
        Choice,
        DeliveryDate,
        [Day],
        Items,
        SortOrderDate,
        SortOrderId
    )
    SELECT
        a.OrderDate,
        a.StudCode,
        c.StudStd,
        c.StudDiv,
        LTRIM(RTRIM(ISNULL(c.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(c.StudLastName, ''))),
        a.PaymentStatus,
        b.Category,
        b.Choice,
        a.DeliveryDate,
        a.[Day],
        b.Items,
        a.DeliveryDate,
        a.OrderID
     FROM MealOrders a, MealPackageM b,StudentLogin c
    WHERE 
        a.MealID = b.ID AND
        a.StudCode = c.StudCode 
        AND b.SchoolId=c.StudSchoolId 
        AND a.[Week] = b.[week]
        AND b.Choice LIKE '%' + LEFT(a.[Day], 2) + '%'
        AND CAST(a.DeliveryDate AS DATE) between @RangeStart and @RangeEndExclusive
        AND (
            (@SchoolIdsCsv <> '' AND b.SchoolId IN (
                SELECT sc.value FROM dbo.fnSplitCsv(@SchoolIdsCsv) sc
            ))
            OR (@SchoolIdsCsv = '' AND (@SchoolId IS NULL OR b.SchoolId = @SchoolId))
        )
        AND a.PaymentStatus = 'PAID'
    OPTION (RECOMPILE);

    CREATE CLUSTERED INDEX CX_MealOrderSummary
        ON #TMP (SortOrderDate DESC, OrderDate DESC);

    SELECT @TotalCount = COUNT(*)
    FROM #TMP;

    IF (@Length IS NULL OR @Length <= 0)
    BEGIN
        SELECT
            OrderDate,
            StudCode,
            StudStd,
            StudDiv,
            StudFullName,
            PaymentStatus,
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
            StudCode,
            StudStd,
            StudDiv,
            StudFullName,
            PaymentStatus,
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