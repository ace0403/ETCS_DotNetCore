/*
Deploy on MealDB. Meal order payment summary.

School scope:
  @SchoolCodesCsv / @SchoolIdsCsv = comma-separated filters for scoped multi-school users.
  Empty CSV + empty single-school param = all schools (unrestricted admin only).

Student filter:
  @StudentId = 0 means all students; otherwise exact Order.StudentId match
  (resolved from StudentLogin.CustomerId in the app layer).

HasAllergenConsent:
  1 when any OrderItem for the order has persisted consent.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spMealOrderPaymentSummary_MealDB_New]
    @startdate AS DATETIME,
    @enddate AS DATETIME,
    @SchoolId AS VARCHAR(10) = '',
    @SchoolIdsCsv AS VARCHAR(MAX) = '',
    @TransactionId AS NVARCHAR(50) = '',
    @StudentId AS INT = 0,
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

    SET @StudentId = ISNULL(@StudentId, 0);

    DECLARE @RangeStart DATETIME = CAST(CAST(@startdate AS DATE) AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = CAST(CAST(@enddate AS DATE) AS DATETIME);

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;

    CREATE TABLE #TMP (
        OrderDate DATETIME NOT NULL,
        StudentId INT NOT NULL,
        TransactionId NVARCHAR(100) NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        HasAllergenConsent BIT NOT NULL,
        AllergenItemText NVARCHAR(500) NULL,
        SortOrderDate DATETIME NOT NULL,
        SortOrderId INT NOT NULL,
        SortOrderItemId INT NOT NULL
    );

    INSERT INTO #TMP (
        OrderDate,
        StudentId,
        TransactionId,
        Amount,
        HasAllergenConsent,
        AllergenItemText,
        SortOrderDate,
        SortOrderId,
        SortOrderItemId
    )
    SELECT
        o.OrderDate,
        o.StudentId,
        LTRIM(RTRIM(ISNULL(t.TransactionId, ''))),
        ISNULL(o.Total, 0),
        CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM [OrderItem] oi
                WHERE oi.OrderId = o.Id
                  AND ISNULL(oi.HasAllergenConsent, 0) = 1
            ) THEN 1 ELSE 0 END AS BIT),
        (
            SELECT STRING_AGG(LTRIM(RTRIM(x.Text)), '; ')
            FROM (
                SELECT DISTINCT oi2.AllergenItemText AS Text
                FROM [OrderItem] oi2
                WHERE oi2.OrderId = o.Id
                  AND oi2.AllergenItemText IS NOT NULL
                  AND LTRIM(RTRIM(oi2.AllergenItemText)) <> ''
            ) x
        ),
        o.OrderDate,
        o.Id,
        o.Id
    FROM [Order] o
    LEFT JOIN [Transaction] t ON t.Id = o.TransactionId
    LEFT JOIN ibonus.dbo.StudentLogin s ON s.UserId = o.StudentId
    WHERE o.OrderTypeId IN (24, 42)
      AND ISNULL(o.IsPaid, 0) = 1
      AND CAST(o.OrderDate AS DATE) between @RangeStart and @RangeEndExclusive
      AND (
            (@SchoolIdsCsv <> '' AND s.UserId IS NOT NULL AND s.StudSchoolId IN (
                SELECT TRY_CAST(sc.value AS INT) FROM dbo.fnSplitCsv(@SchoolIdsCsv) sc WHERE TRY_CAST(sc.value AS INT) IS NOT NULL
            ))
            OR (@SchoolIdsCsv = '' AND (@SchoolIdInt IS NULL OR (s.UserId IS NOT NULL AND s.StudSchoolId = @SchoolIdInt)))
          )
      AND (@TransactionId = '' OR t.TransactionId = @TransactionId)
      AND (@StudentId = 0 OR o.StudentId = @StudentId)
     
    OPTION (RECOMPILE);

    CREATE CLUSTERED INDEX CX_MealOrderPaymentSummary_MealDB
        ON #TMP (SortOrderDate DESC);

    SELECT @TotalCount = COUNT(*)
    FROM #TMP;

    IF (@Length IS NULL OR @Length <= 0)
    BEGIN
        SELECT
            OrderDate,
            StudentId,
            TransactionId,
            Amount,
            HasAllergenConsent,
            AllergenItemText
        FROM #TMP
        ORDER BY SortOrderDate DESC;
    END
    ELSE
    BEGIN
        SELECT
            OrderDate,
            StudentId,
            TransactionId,
            Amount,
            HasAllergenConsent,
            AllergenItemText
        FROM #TMP
        ORDER BY SortOrderDate DESC
        OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;
    END

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;
END
GO
