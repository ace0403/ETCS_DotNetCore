/*
Deploy on ibonus database.

Performance notes:
  - Sargable date range on LogDateTimeTerminal (no CAST on column)
  - Drive from AccessLog with filters before joining POSPurchase
  - Skip POS/cash branch when transaction type filter makes it irrelevant
  - Clustered index on #TMP before COUNT / ORDER BY / OFFSET
  - See spCanteentranSummary_New.Indexes.sql for recommended base-table indexes

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

CREATE OR ALTER PROCEDURE [dbo].[spCanteentranSummary_New]
    @startdate AS DATE,
    @enddate AS DATE,
    @transaciontype AS NVARCHAR(40),
    @customerid AS VARCHAR(20),
    @branch AS VARCHAR(20),
    @SchoolId AS VARCHAR(10),
    @Start AS INT = 0,
    @Length AS INT = 0,
    @TotalCount AS INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Start = ISNULL(@Start, 0);
    IF (@Start < 0) SET @Start = 0;

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
            p.Amount,
            1,
            CONVERT(DECIMAL(15, 1), p.Amount),
            s.ItemName,
            a.BalPrepaid,
            s.ItemCode,
            t.Description
        FROM AccessLog a
            INNER JOIN POSPurchase p
                ON p.TransId = a.TransactionID
               AND p.Customerid = a.CustomerID
            INNER JOIN SKU s
                ON s.ItemCode = p.SkuCode
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) t
            LEFT JOIN StudentLogin sl
                ON sl.CustomerID = p.Customerid
        WHERE a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
          AND a.TransactionType IN (SELECT Id FROM #TypeIds)
          AND (@customerid = '' OR @customerid IS NULL OR p.Customerid = @customerid)
          AND (@branch = '' OR @branch IS NULL OR a.TerminalCode = @branch)
          AND (@SchoolId = '' OR @SchoolId IS NULL OR a.BranchCode = @SchoolId)
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
            'Cash',
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
        WHERE a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
          AND a.TransactionType = 1004
          AND a.CustomerID IN ('208', '209')
          AND (@branch = '' OR @branch IS NULL OR a.TerminalCode = @branch)
          AND (@SchoolId = '' OR @SchoolId IS NULL OR a.BranchCode = @SchoolId)
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
