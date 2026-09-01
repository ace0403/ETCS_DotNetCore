/*
Deploy on ibonus. Admin dashboard overview.

School scope:
  @SchoolCodesCsv / @SchoolIdsCsv = comma-separated filters for scoped multi-school users.
  Empty CSV + empty single-school param = all schools (unrestricted admin only).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spAdminDashboardOverview_New]
    @StartDate AS DATETIME,
    @EndDate AS DATETIME,
    @SchoolCode AS VARCHAR(10) = '',
    @SchoolCodesCsv AS VARCHAR(MAX) = ''
AS
BEGIN
    SET NOCOUNT ON;

    SET @SchoolCode = LTRIM(RTRIM(ISNULL(@SchoolCode, '')));
    SET @SchoolCodesCsv = LTRIM(RTRIM(ISNULL(@SchoolCodesCsv, '')));

    DECLARE @RangeStart DATETIME = CAST(CAST(@StartDate AS DATE) AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = DATEADD(DAY, 1, CAST(CAST(@EndDate AS DATE) AS DATETIME));
    DECLARE @DayCount INT = DATEDIFF(DAY, CAST(@StartDate AS DATE), CAST(@EndDate AS DATE)) + 1;
    DECLARE @PriorEndExclusive DATETIME = @RangeStart;
    DECLARE @PriorStart DATETIME = DATEADD(DAY, -@DayCount, @PriorEndExclusive);

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;

    CREATE TABLE #TMP (
        [Datetime] DATETIME NOT NULL,
        CustomerID NVARCHAR(75) NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        TransactionType NVARCHAR(200) NOT NULL,
        TerminalCode NVARCHAR(50) NULL,
        TerminalName NVARCHAR(4000) NULL,
        StudentName NVARCHAR(4000) NULL,
        TransactionID NVARCHAR(100) NULL,
        SortDate DATE NOT NULL,
        IsStudentCardSale BIT NOT NULL,
        IsCashCardSale BIT NOT NULL
    );

    INSERT INTO #TMP (
        [Datetime],
        CustomerID,
        Amount,
        TransactionType,
        TerminalCode,
        TerminalName,
        StudentName,
        TransactionID,
        SortDate,
        IsStudentCardSale,
        IsCashCardSale
    )
    SELECT
        a.LogDateTimeTerminal,
        a.CustomerID,
        ISNULL(a.Amount, 0),
        CASE
            WHEN a.TransactionType = 21002 THEN 'Student-Card Purchase'
            WHEN a.TransactionType = 21004 THEN 'Manual Topup'
            WHEN a.TransactionType = 10001 THEN 'Online Topup'
            WHEN a.TransactionType = 21006 THEN 'Undo Purchase'
            WHEN a.TransactionType = 21007 THEN 'Undo Topup'
            WHEN a.TransactionType = 9001 THEN 'Meal Plan'
            WHEN a.TransactionType = 1004 THEN 'Cash Purchase'
            WHEN a.TransactionType = 2004 THEN 'Credit Card Purchase'
            WHEN a.TransactionType = 1007 THEN 'Undo Cash Purchase'
            ELSE 'Other'
        END,
        a.TerminalCode,
        ISNULL(term.Description, ''),
        CASE
            WHEN sl.StudFirstName IS NULL AND sl.StudLastName IS NULL THEN '--'
            ELSE LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, '')))
        END,
        a.TransactionID,
        CAST(a.LogDateTimeTerminal AS DATE),
        CASE WHEN a.TransactionType = 21002 THEN 1 ELSE 0 END,
        CASE WHEN a.TransactionType IN (1004, 2004) THEN 1 ELSE 0 END
    FROM AccessLog a
        OUTER APPLY (
            SELECT TOP (1) t.Description
            FROM IDTerminals t
            WHERE t.TerminalCode = a.TerminalCode
              AND t.branchcode = a.branchcode
        ) term
        LEFT JOIN StudentLogin sl ON sl.CustomerID = a.CustomerID
    WHERE a.LogDateTimeTerminal >= @RangeStart
      AND a.LogDateTimeTerminal < @RangeEndExclusive
      AND (
            (@SchoolCodesCsv <> '' AND EXISTS (
                SELECT 1 FROM dbo.fnSplitCsv(@SchoolCodesCsv) sc
                WHERE a.BranchCode = TRY_CAST(sc.value AS SMALLINT)
            ))
            OR (@SchoolCodesCsv = '' AND (@SchoolCode = '' OR a.BranchCode = TRY_CAST(@SchoolCode AS SMALLINT)))
          )
      AND (
            a.TransactionType IN (21002, 21004, 21006, 21007, 10001, 9001)
            OR (a.TransactionType = 1004 AND a.CustomerID IN ('204', '205', '206', '207', '208'))
            OR (a.TransactionType = 2004 AND a.CustomerID IN ('209', '204', '205', '206', '207', '208'))
            OR (a.TransactionType = 1007 AND a.CustomerID IN ('204', '205', '206', '207'))
          )
    OPTION (RECOMPILE);

    CREATE CLUSTERED INDEX CX_Dashboard ON #TMP (SortDate ASC, [Datetime] ASC, TransactionID);

    DECLARE
        @TotalSales DECIMAL(18, 2),
        @TransactionCount INT,
        @StudentCardSales DECIMAL(18, 2),
        @CashCardSales DECIMAL(18, 2),
        @PriorTotalSales DECIMAL(18, 2),
        @PriorTransactionCount INT,
        @PriorStudentCardSales DECIMAL(18, 2),
        @PriorCashCardSales DECIMAL(18, 2);

    SELECT
        @TotalSales = ISNULL(SUM(Amount), 0),
        @TransactionCount = COUNT(*),
        @StudentCardSales = ISNULL(SUM(CASE WHEN IsStudentCardSale = 1 THEN Amount ELSE 0 END), 0),
        @CashCardSales = ISNULL(SUM(CASE WHEN IsCashCardSale = 1 THEN Amount ELSE 0 END), 0)
    FROM #TMP;

    SELECT
        @PriorTotalSales = ISNULL(SUM(ISNULL(a.Amount, 0)), 0),
        @PriorTransactionCount = COUNT(*),
        @PriorStudentCardSales = ISNULL(SUM(CASE WHEN a.TransactionType = 21002 THEN ISNULL(a.Amount, 0) ELSE 0 END), 0),
        @PriorCashCardSales = ISNULL(SUM(CASE WHEN a.TransactionType IN (1004, 2004) THEN ISNULL(a.Amount, 0) ELSE 0 END), 0)
    FROM AccessLog a
    WHERE a.LogDateTimeTerminal >= @PriorStart
      AND a.LogDateTimeTerminal < @PriorEndExclusive
      AND (
            (@SchoolCodesCsv <> '' AND EXISTS (
                SELECT 1 FROM dbo.fnSplitCsv(@SchoolCodesCsv) sc
                WHERE a.BranchCode = TRY_CAST(sc.value AS SMALLINT)
            ))
            OR (@SchoolCodesCsv = '' AND (@SchoolCode = '' OR a.BranchCode = TRY_CAST(@SchoolCode AS SMALLINT)))
          )
      AND (
            a.TransactionType IN (21002, 21004, 21006, 21007, 10001, 9001)
            OR (a.TransactionType = 1004 AND a.CustomerID IN ('204', '205', '206', '207', '208'))
            OR (a.TransactionType = 2004 AND a.CustomerID IN ('209', '204', '205', '206', '207', '208'))
            OR (a.TransactionType = 1007 AND a.CustomerID IN ('204', '205', '206', '207'))
          )
    OPTION (RECOMPILE);

    SELECT
        @TotalSales AS TotalSales,
        @TransactionCount AS TransactionCount,
        @StudentCardSales AS StudentCardSales,
        @CashCardSales AS CashCardSales,
        @PriorTotalSales AS PriorTotalSales,
        @PriorTransactionCount AS PriorTransactionCount,
        @PriorStudentCardSales AS PriorStudentCardSales,
        @PriorCashCardSales AS PriorCashCardSales,
        @PriorStart AS PriorStartDate,
        DATEADD(DAY, -1, @PriorEndExclusive) AS PriorEndDate;

    SELECT
        SortDate AS [Day],
        ISNULL(SUM(Amount), 0) AS SalesAmount,
        COUNT(*) AS TransactionCount
    FROM #TMP
    GROUP BY SortDate
    ORDER BY SortDate ASC;

    SELECT
        TransactionType AS [Label],
        ISNULL(SUM(Amount), 0) AS Amount
    FROM #TMP
    GROUP BY TransactionType
    ORDER BY Amount DESC;

    SELECT TOP (5)
        TerminalCode,
        TerminalName,
        ISNULL(SUM(Amount), 0) AS SalesAmount
    FROM #TMP
    WHERE ISNULL(TerminalCode, '') <> ''
    GROUP BY TerminalCode, TerminalName
    ORDER BY SalesAmount DESC;

    SELECT TOP (10)
        [Datetime],
        CustomerID AS StudentCardNo,
        StudentName,
        TransactionType,
        Amount,
        TerminalName
    FROM #TMP
    ORDER BY [Datetime] DESC, TransactionID DESC;

    IF OBJECT_ID('tempdb..#TMP') IS NOT NULL
        DROP TABLE #TMP;
END
GO