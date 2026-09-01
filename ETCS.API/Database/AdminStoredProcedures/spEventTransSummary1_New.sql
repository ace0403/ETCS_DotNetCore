/*
Deploy on ibonus. Terminal sales summary.

School scope:
  @SchoolCodesCsv / @SchoolIdsCsv = comma-separated filters for scoped multi-school users.
  Empty CSV + empty single-school param = all schools (unrestricted admin only).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spEventTransSummary1_New]
    @StartDate AS DATETIME,
    @EndDate AS DATETIME,
    @EventId AS NVARCHAR(75) = '',
    @TransectionType AS NVARCHAR(75) = 'ALL',
    @SchoolCode AS NVARCHAR(75) = '',
    @SchoolCodesCsv AS VARCHAR(MAX) = '',
    @TerminalCode AS NVARCHAR(75) = '',
    @Start AS INT = 0,
    @Length AS INT = 0,
    @TotalCount AS INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Start = ISNULL(@Start, 0);
    IF (@Start < 0) SET @Start = 0;

    SET @EventId = LTRIM(RTRIM(ISNULL(@EventId, '')));
    SET @TransectionType = LTRIM(RTRIM(ISNULL(@TransectionType, '')));
    SET @SchoolCode = LTRIM(RTRIM(ISNULL(@SchoolCode, '')));
    SET @SchoolCodesCsv = LTRIM(RTRIM(ISNULL(@SchoolCodesCsv, '')));
    SET @TerminalCode = LTRIM(RTRIM(ISNULL(@TerminalCode, '')));

    IF (@TransectionType = '')
        SET @TransectionType = 'ALL';

    DECLARE @RangeStart DATETIME = CAST(CAST(@StartDate AS DATE) AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = DATEADD(DAY, 1, CAST(CAST(@EndDate AS DATE) AS DATETIME));

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;

    CREATE TABLE #TMP (
        TerminalCode NVARCHAR(50) NOT NULL,
        TerminalName NVARCHAR(4000) NULL,
        [Date] NVARCHAR(20) NOT NULL,
        StudentsCount INT NOT NULL,
        StudentCardPurchase DECIMAL(18, 2) NOT NULL,
        CashPurchase DECIMAL(18, 2) NOT NULL,
        CreditCardPurchase DECIMAL(18, 2) NOT NULL,
        StudentCardManualTopup DECIMAL(18, 2) NOT NULL,
        StudentCardUndoTopup DECIMAL(18, 2) NOT NULL,
        OnlineStudentCardTopup DECIMAL(18, 2) NOT NULL,
        UndoCashPurchase DECIMAL(18, 2) NOT NULL,
        SortDate DATE NOT NULL
    );

    INSERT INTO #TMP (
        TerminalCode,
        TerminalName,
        [Date],
        StudentsCount,
        StudentCardPurchase,
        CashPurchase,
        CreditCardPurchase,
        StudentCardManualTopup,
        StudentCardUndoTopup,
        OnlineStudentCardTopup,
        UndoCashPurchase,
        SortDate
    )
    SELECT
        a.TerminalCode,
        t.Description,
        CONVERT(VARCHAR(10), a.LogDateTimeTerminal, 103),
        COUNT(a.CustomerID),
        SUM(CASE WHEN a.TransactionType IN (21002) AND a.CustomerID NOT IN ('204', '205', '206', '207') THEN ISNULL(a.Amount, 0) ELSE 0 END),
        SUM(CASE WHEN a.TransactionType IN (21002, 1004) AND a.CustomerID IN ('204', '205', '206', '207', '208') THEN ISNULL(a.Amount, 0) ELSE 0 END),
        SUM(CASE WHEN a.TransactionType IN (2004) AND a.CustomerID IN ('209') THEN ISNULL(a.Amount, 0) ELSE 0 END),
        SUM(CASE WHEN a.TransactionType IN (21004) AND a.CustomerID NOT IN ('204', '205', '206', '207') THEN ISNULL(a.Amount, 0) ELSE 0 END),
        SUM(CASE WHEN a.TransactionType IN (21007) AND a.CustomerID NOT IN ('204', '205', '206', '207') THEN ISNULL(a.Amount, 0) ELSE 0 END),
        SUM(CASE WHEN a.TransactionType IN (10001) AND a.CustomerID NOT IN ('204', '205', '206', '207') THEN ISNULL(a.Amount, 0) ELSE 0 END),
        SUM(CASE WHEN a.TransactionType IN (1007) AND a.CustomerID IN ('204', '205', '206', '207') THEN ISNULL(a.Amount, 0) ELSE 0 END),
        CAST(a.LogDateTimeTerminal AS DATE)
    FROM AccessLog a
    INNER JOIN IDTerminals t ON t.TerminalCode = a.TerminalCode AND a.BranchCode = t.BranchCode
    WHERE a.LogDateTimeTerminal >= @RangeStart
      AND a.LogDateTimeTerminal < @RangeEndExclusive
      AND (@EventId = '')
      AND (
            (@SchoolCodesCsv <> '' AND EXISTS (
                SELECT 1 FROM dbo.fnSplitCsv(@SchoolCodesCsv) sc
                WHERE LTRIM(RTRIM(sc.value)) <> ''
                  AND t.BranchCode = TRY_CAST(LTRIM(RTRIM(sc.value)) AS SMALLINT)
            ))
            OR (@SchoolCodesCsv = '' AND (@SchoolCode = '' OR t.BranchCode = TRY_CAST(@SchoolCode AS SMALLINT)))
          )
      AND (@TerminalCode = '' OR t.TerminalCode = @TerminalCode)
      AND (
            @TransectionType = 'ALL'
            OR a.TransactionType = TRY_CAST(@TransectionType AS INT)
          )
    GROUP BY
        CONVERT(VARCHAR(10), a.LogDateTimeTerminal, 103),
        a.TerminalCode,
        t.Description,
        CAST(a.LogDateTimeTerminal AS DATE)
    OPTION (RECOMPILE);

    -- Clustered index supports COUNT, ORDER BY, and OFFSET/FETCH on #TMP.
    CREATE CLUSTERED INDEX CX_EventTransSummary
        ON #TMP (SortDate DESC, TerminalCode ASC);

    SELECT @TotalCount = COUNT(*)
    FROM #TMP;

    IF (@Length IS NULL OR @Length <= 0)
    BEGIN
        SELECT
            TerminalCode,
            TerminalName,
            [Date],
            StudentsCount,
            StudentCardPurchase,
            CashPurchase,
            CreditCardPurchase,
            StudentCardManualTopup,
            StudentCardUndoTopup,
            OnlineStudentCardTopup,
            UndoCashPurchase
        FROM #TMP
        ORDER BY SortDate DESC, TerminalCode ASC;
    END
    ELSE
    BEGIN
        SELECT
            TerminalCode,
            TerminalName,
            [Date],
            StudentsCount,
            StudentCardPurchase,
            CashPurchase,
            CreditCardPurchase,
            StudentCardManualTopup,
            StudentCardUndoTopup,
            OnlineStudentCardTopup,
            UndoCashPurchase
        FROM #TMP
        ORDER BY SortDate DESC, TerminalCode ASC
        OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;
    END

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;
END
GO