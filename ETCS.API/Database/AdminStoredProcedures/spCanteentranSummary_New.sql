/*
Deploy on ibonus. Canteen transaction summary.

School scope:
  @SchoolCodesCsv / @SchoolIdsCsv = comma-separated filters for scoped multi-school users.
  Empty CSV + empty single-school param = all schools (unrestricted admin only).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

--EXEC spCanteentranSummary_New '2026-08-26','2026-08-27','','','','',0,500,0
CREATE OR ALTER PROCEDURE [dbo].[spCanteentranSummary_New]
    @startdate AS DATE,
    @enddate AS DATE,
    @transaciontype AS NVARCHAR(40),
    @customerid AS VARCHAR(20),
    @branch AS VARCHAR(20),
    @SchoolId AS VARCHAR(10),
    @SchoolCodesCsv AS VARCHAR(MAX) = '',
    @Start AS INT = 0,
    @Length AS INT = 0,
    @TotalCount AS INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Start = ISNULL(@Start, 0);
    IF (@Start < 0) SET @Start = 0;

    SET @SchoolId = LTRIM(RTRIM(ISNULL(@SchoolId, '')));
    SET @SchoolCodesCsv = LTRIM(RTRIM(ISNULL(@SchoolCodesCsv, '')));

    DECLARE @RangeStart DATETIME = CAST(@startdate AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = DATEADD(DAY, 1, CAST(@enddate AS DATETIME));
    DECLARE @IncludePos BIT = 1;
    DECLARE @IncludeCash BIT = 1;

    IF (@transaciontype IS NOT NULL AND @transaciontype <> '')
    BEGIN
        IF (@transaciontype = '1004')
        BEGIN
            SET @IncludePos = 0;
            SET @IncludeCash = 1;
        END
        ELSE
        BEGIN
            SET @IncludePos = 1;
            SET @IncludeCash = 0;
        END
    END

    CREATE TABLE #TypeIds (Id INT PRIMARY KEY);

    IF (@transaciontype = '' OR @transaciontype IS NULL)
    BEGIN
        INSERT INTO #TypeIds (Id) VALUES (21002), (21006), (1004), (2004);
    END
    ELSE
    BEGIN
        INSERT INTO #TypeIds (Id) VALUES (CAST(@transaciontype AS INT));
    END

    CREATE TABLE #TMP (
        StudCode NVARCHAR(100) NOT NULL,
        StudFirstName NVARCHAR(4000) NULL,
        [Datetime] DATETIME NOT NULL,
        CustomerID NVARCHAR(100) NULL,
        Price NVARCHAR(100) NULL,
        Quantity INT NOT NULL,
        amount DECIMAL(15, 1) NOT NULL,
        TransactionType NVARCHAR(4000) NULL,
        BalPrepaid DECIMAL(18, 2) NULL,
        ItemCode NVARCHAR(100) NULL,
        [Location] NVARCHAR(4000) NULL
    );

    IF (@IncludePos = 1)
    BEGIN
        INSERT INTO #TMP (
            StudCode,
            StudFirstName,
            [Datetime],
            CustomerID,
            Price,
            Quantity,
            amount,
            TransactionType,
            BalPrepaid,
            ItemCode,
            [Location]
        )
        SELECT
            CASE
                WHEN a.CustomerID = '208' THEN 'Cash'
                WHEN a.CustomerID = '209' THEN 'Bank Card'
                ELSE a.CustomerID
            END,
            sl.StudFirstName + ' ' + sl.StudLastName,
            a.LogDateTimeTerminal,
            a.CustomerID,
            ISNULL(p.Amount,a.Amount),
            1,
            CONVERT(DECIMAL(15, 1), ISNULL(p.Amount,a.Amount)),
            CASE
                WHEN a.TransactionType = 21004 THEN 'Topup'
                WHEN a.TransactionType = 10001 THEN 'Online Topup'
                WHEN a.TransactionType = 2004 THEN 'Purchase Credit-Card'
                WHEN a.TransactionType = 21002 THEN 'Purchase Student-card'
                WHEN a.TransactionType = 21007 THEN 'Undo Topup'
                WHEN a.TransactionType = 21006 THEN 'Undo Purchase'
                WHEN a.TransactionType = 9001 THEN 'Meal Plan'
            END,
            --CASE WHEN CAST(a.LogDateTimeTerminal AS DATE) > '2026-08-01' THEN m.Name ELSE s.ItemName END,
            a.BalPrepaid,
            s.ItemCode,
            t.Description
        FROM AccessLog a
            LEFT JOIN POSPurchase p
                ON p.TransId = a.TransactionID
               AND p.Customerid = a.CustomerID
            LEFT JOIN SKU s
                ON s.ItemCode = p.SkuCode
            LEFT JOIN MealItems m
                ON m.Id = CAST(p.SkuCode AS INT)
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) t
            LEFT JOIN StudentLogin sl
                ON sl.CustomerID = p.Customerid
        WHERE
          CAST(a.LogDateTimeTerminal AS DATE) BETWEEN @RangeStart AND @RangeEndExclusive
          AND a.TransactionType IN (SELECT Id FROM #TypeIds) AND a.TransactionType != 1004
          AND (@customerid = '' OR @customerid IS NULL OR p.Customerid = @customerid)
          AND (@branch = '' OR @branch IS NULL OR a.TerminalCode = @branch)
          AND (
                (@SchoolCodesCsv <> '' AND EXISTS (
                    SELECT 1 FROM dbo.fnSplitCsv(@SchoolCodesCsv) sc
                    WHERE a.BranchCode = sc.value
                ))
                OR (@SchoolCodesCsv = '' AND (@SchoolId = '' OR @SchoolId IS NULL OR a.BranchCode = @SchoolId))
              )
        GROUP BY 
		    LogDateTimeTerminal,
		    A.CustomerID,
		    TransactionType,
		    t.Description,
		    a.CardID,
		    ISNULL(p.Amount,a.Amount),
		    a.Description,
		    s.ItemName,
		    s.ItemCode,
		    sl.StudFirstName+' '+sl.StudLastName,
		    a.BalPrepaid,
		    p.Id

        OPTION (RECOMPILE);
    END

    IF (@IncludeCash = 1)
    BEGIN
        INSERT INTO #TMP (
            StudCode,
            StudFirstName,
            [Datetime],
            CustomerID,
            Price,
            Quantity,
            amount,
            TransactionType,
            BalPrepaid,
            ItemCode,
            [Location]
        )
        SELECT
            CASE
                WHEN a.CustomerID = '208' THEN 'Cash'
                WHEN a.CustomerID = '209' THEN 'Bank Card'
                ELSE a.CustomerID
            END,
            '',
            a.LogDateTimeTerminal,
            a.CustomerID,
            a.Amount,
            1,
            CONVERT(DECIMAL(15, 1), a.Amount),
            'Cash Purchase',
            a.BalPrepaid,
            '',
            ISNULL(t.Description, '')
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) term.Description
                FROM IDTerminals term
                WHERE term.TerminalCode = a.TerminalCode
                  AND term.branchcode = a.branchcode
            ) t
        WHERE 
          CAST(a.LogDateTimeTerminal AS DATE) BETWEEN @RangeStart AND @RangeEndExclusive
          AND a.TransactionType = 1004
          AND a.CustomerID IN ('208', '209')
          AND (@branch = '' OR @branch IS NULL OR a.TerminalCode = @branch)
          AND (
                (@SchoolCodesCsv <> '' AND EXISTS (
                    SELECT 1 FROM dbo.fnSplitCsv(@SchoolCodesCsv) sc
                    WHERE a.BranchCode = sc.value
                ))
                OR (@SchoolCodesCsv = '' AND (@SchoolId = '' OR @SchoolId IS NULL OR a.BranchCode = @SchoolId))
              )
        OPTION (RECOMPILE);
    END

    CREATE CLUSTERED INDEX CX_TMP_Canteen ON #TMP ([Datetime] DESC, StudCode);

    SELECT @TotalCount = COUNT(*)
    FROM #TMP;

    IF (@Length IS NULL OR @Length <= 0)
    BEGIN
        SELECT
            StudCode,
            StudFirstName,
            [Datetime],
            CustomerID,
            Price,
            Quantity,
            amount,
            TransactionType,
            BalPrepaid,
            ItemCode,
            [Location]
        FROM #TMP
        ORDER BY [Datetime] DESC, StudCode;
    END
    ELSE
    BEGIN
        SELECT
            StudCode,
            StudFirstName,
            [Datetime],
            CustomerID,
            Price,
            Quantity,
            amount,
            TransactionType,
            BalPrepaid,
            ItemCode,
            [Location]
        FROM #TMP
        ORDER BY [Datetime] DESC, StudCode
        OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;
    END
END
GO