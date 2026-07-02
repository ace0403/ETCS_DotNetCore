/*
Deploy on ibonus database.

Standalone clone of dbo.spRptAdmineTransaction with pagination.
Does NOT call the legacy procedure.

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

CREATE OR ALTER PROCEDURE [dbo].[spRptAdmineTransaction_New]
    @StartDate AS DATETIME,
    @EndDate AS DATETIME,
    @TransactionType AS NVARCHAR(75),
    @customerid AS NVARCHAR(75),
    @TerminalCode AS VARCHAR(50),
    @SchoolId AS VARCHAR(10),
    @Start AS INT = 0,
    @Length AS INT = 0,
    @TotalCount AS INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Start = ISNULL(@Start, 0);
    IF (@Start < 0) SET @Start = 0;

    SET @customerid = LTRIM(RTRIM(ISNULL(@customerid, '')));
    SET @TerminalCode = LTRIM(RTRIM(ISNULL(@TerminalCode, '')));
    SET @SchoolId = LTRIM(RTRIM(ISNULL(@SchoolId, '')));
    SET @TransactionType = LTRIM(RTRIM(ISNULL(@TransactionType, '')));

    IF (@TransactionType = '')
        SET @TransactionType = 'ALL';

    DECLARE @RangeStart DATETIME = CAST(CAST(@StartDate AS DATE) AS DATETIME);
    DECLARE @RangeEndExclusive DATETIME = DATEADD(DAY, 1, CAST(CAST(@EndDate AS DATE) AS DATETIME));

    CREATE TABLE #TMP (
        [Datetime] DATETIME NOT NULL,
        StudentID NVARCHAR(75) NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        [Name] NVARCHAR(4000) NULL,
        Class NVARCHAR(200) NULL,
        TransactionType NVARCHAR(200) NULL,
        VAT DECIMAL(18, 2) NOT NULL,
        Terminal NVARCHAR(4000) NULL,
        TransactionID NVARCHAR(100) NULL
    );

    IF (@customerid <> '' AND @TransactionType = 'ALL')
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            a.CustomerID,
            ISNULL(a.Amount, 0),
            sl.StudFirstName + ' ' + sl.StudLastName,
            sl.StudStd + '-' + sl.StudDiv,
            CASE
                WHEN a.TransactionType = 21004 THEN 'Topup'
                WHEN a.TransactionType = 10001 THEN 'Online Topup'
                WHEN a.TransactionType = 21002 THEN 'PURCHASE Student-card'
                WHEN a.TransactionType = 21007 THEN 'Undo Topup'
                WHEN a.TransactionType = 21006 THEN 'Undo Purchase'
                WHEN a.TransactionType = 9001 THEN 'Meal Plan'
            END,
            CASE WHEN a.TransactionType = 21002 THEN CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05) ELSE 0 END,
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
            LEFT JOIN StudentLogin sl ON sl.CustomerID = a.CustomerID
        WHERE a.TransactionType IN (21002, 21004, 21006, 21007, 10001, 9001)
          AND a.CustomerID = @customerid
          AND a.LogDateTimeServer >= @RangeStart
          AND a.LogDateTimeServer < @RangeEndExclusive
          AND (@SchoolId = '' OR a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT))
          AND (
                @TerminalCode = ''
                OR LOWER(@TerminalCode) = 'all'
                OR a.TerminalCode = @TerminalCode
              )
        OPTION (RECOMPILE);
    END
    ELSE IF (@customerid <> '' AND @TransactionType <> 'ALL')
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            a.CustomerID,
            ISNULL(a.Amount, 0),
            sl.StudFirstName + ' ' + sl.StudLastName,
            sl.StudStd + '-' + sl.StudDiv,
            CASE
                WHEN a.TransactionType = 21004 THEN 'Topup'
                WHEN a.TransactionType = 10001 THEN 'Online Topup'
                WHEN a.TransactionType = 21002 THEN 'PURCHASE Student-card'
                WHEN a.TransactionType = 21007 THEN 'Undo Topup'
                WHEN a.TransactionType = 21006 THEN 'Undo Purchase'
                WHEN a.TransactionType = 9001 THEN 'Meal Plan'
            END,
            CASE WHEN a.TransactionType = 21002 THEN CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05) ELSE 0 END,
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
            LEFT JOIN StudentLogin sl ON sl.CustomerID = a.CustomerID
        WHERE a.TransactionType = TRY_CAST(@TransactionType AS INT)
          AND a.CustomerID = @customerid
          AND a.LogDateTimeServer >= @RangeStart
          AND a.LogDateTimeServer < @RangeEndExclusive
          AND (@SchoolId = '' OR a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT))
        OPTION (RECOMPILE);
    END
    ELSE IF (@TransactionType = '1004')
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            '--',
            ISNULL(a.Amount, 0),
            '--',
            '--',
            'PURCHASE Cash',
            CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05),
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
        WHERE a.TransactionType IN (21002, 1004)
          AND a.CustomerID IN ('204', '205', '206', '207', '208')
          AND a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
          AND (@SchoolId = '' OR a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT))
        OPTION (RECOMPILE);
    END
    ELSE IF (@TransactionType = '1007')
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            '--',
            ISNULL(a.Amount, 0),
            '--',
            '--',
            'Undo PURCHASE Cash',
            0,
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
        WHERE a.TransactionType = 1007
          AND a.CustomerID IN ('204', '205', '206', '207', '208')
          AND a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
        OPTION (RECOMPILE);
    END
    ELSE IF (@TransactionType = '2004')
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            '--',
            ISNULL(a.Amount, 0),
            '--',
            '--',
            'PURCHASE Credit Card ',
            CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05),
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
        WHERE a.TransactionType = 2004
          AND a.CustomerID IN ('209', '204', '205', '206', '207', '208')
          AND a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
          AND (@SchoolId = '' OR a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT))
        OPTION (RECOMPILE);
    END
    ELSE IF (@TransactionType <> 'ALL')
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            a.CustomerID,
            ISNULL(a.Amount, 0),
            sl.StudFirstName + ' ' + sl.StudLastName,
            sl.StudStd + '-' + sl.StudDiv,
            CASE
                WHEN a.TransactionType = 21004 THEN 'Topup'
                WHEN a.TransactionType = 10001 THEN 'Online Topup'
                WHEN a.TransactionType = 21002 THEN 'PURCHASE Student-card'
                WHEN a.TransactionType = 21007 THEN 'Undo Topup'
                WHEN a.TransactionType = 21006 THEN 'Undo Purchase'
                WHEN a.TransactionType = 9001 THEN 'Meal Plan'
            END,
            CASE WHEN a.TransactionType = 21002 THEN CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05) ELSE 0 END,
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
            LEFT JOIN StudentLogin sl ON sl.CustomerID = a.CustomerID
        WHERE a.TransactionType = TRY_CAST(@TransactionType AS INT)
          AND a.CustomerID NOT IN ('204', '205', '206', '207', '208', '', '1093710000000028')
          AND a.LogDateTimeServer >= @RangeStart
          AND a.LogDateTimeServer < @RangeEndExclusive
          AND (@SchoolId = '' OR a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT))
        OPTION (RECOMPILE);
    END
    ELSE IF (@SchoolId = '')
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            a.CustomerID,
            ISNULL(a.Amount, 0),
            sl.StudFirstName + ' ' + sl.StudLastName,
            sl.StudStd + '-' + sl.StudDiv,
            CASE
                WHEN a.TransactionType = 21004 THEN 'Topup'
                WHEN a.TransactionType = 10001 THEN 'Online Topup'
                WHEN a.TransactionType = 21002 THEN 'PURCHASE Student-card'
                WHEN a.TransactionType = 21007 THEN 'Undo Topup'
                WHEN a.TransactionType = 21006 THEN 'Undo Purchase'
                WHEN a.TransactionType = 9001 THEN 'Meal Plan'
            END,
            CASE WHEN a.TransactionType = 21002 THEN CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05) ELSE 0 END,
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
            LEFT JOIN StudentLogin sl ON sl.CustomerID = a.CustomerID
        WHERE a.TransactionType IN (21002, 21004, 21006, 21007, 10001, 9001)
          AND a.LogDateTimeServer >= @RangeStart
          AND a.LogDateTimeServer < @RangeEndExclusive
        OPTION (RECOMPILE);
    END
    ELSE
    BEGIN
        INSERT INTO #TMP
        SELECT
            a.LogDateTimeTerminal,
            a.CustomerID,
            ISNULL(a.Amount, 0),
            sl.StudFirstName + ' ' + sl.StudLastName,
            sl.StudStd + '-' + sl.StudDiv,
            CASE
                WHEN a.TransactionType = 21004 THEN 'Topup'
                WHEN a.TransactionType = 10001 THEN 'Online Topup'
                WHEN a.TransactionType = 21002 THEN 'PURCHASE Student-card'
                WHEN a.TransactionType = 21007 THEN 'Undo Topup'
                WHEN a.TransactionType = 21006 THEN 'Undo Purchase'
                WHEN a.TransactionType = 9001 THEN 'Meal Plan'
            END,
            CASE WHEN a.TransactionType = 21002 THEN CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05) ELSE 0 END,
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
            LEFT JOIN StudentLogin sl ON sl.CustomerID = a.CustomerID
        WHERE a.TransactionType IN (21002, 21004, 21006, 21007, 10001, 9001)
          AND a.CustomerID NOT IN ('204', '205', '206', '207', '208', '', '1093710000000028')
          AND a.LogDateTimeServer >= @RangeStart
          AND a.LogDateTimeServer < @RangeEndExclusive
          AND a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT)
          AND (@TerminalCode = '' OR a.TerminalCode = @TerminalCode)

        UNION ALL

        SELECT
            a.LogDateTimeTerminal,
            '--',
            ISNULL(a.Amount, 0),
            '--',
            '--',
            'PURCHASE Cash',
            CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05),
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
        WHERE a.TransactionType IN (21002, 1004)
          AND a.CustomerID IN ('204', '205', '206', '207', '208')
          AND a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
          AND a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT)
          AND (@TerminalCode = '' OR a.TerminalCode = @TerminalCode)

        UNION ALL

        SELECT
            a.LogDateTimeTerminal,
            '--',
            ISNULL(a.Amount, 0),
            '--',
            '--',
            'PURCHASE Credit Card ',
            CONVERT(DECIMAL(10, 2), ISNULL(a.Amount, 0) * 0.05),
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
        WHERE a.TransactionType = 2004
          AND a.CustomerID = '209'
          AND a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
          AND a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT)
          AND (@TerminalCode = '' OR a.TerminalCode = @TerminalCode)

        UNION ALL

        SELECT
            a.LogDateTimeTerminal,
            '--',
            ISNULL(a.Amount, 0),
            '--',
            '--',
            'Undo PURCHASE Cash',
            0,
            ISNULL(term.Description, ''),
            a.TransactionID
        FROM AccessLog a
            OUTER APPLY (
                SELECT TOP (1) t.Description
                FROM IDTerminals t
                WHERE t.TerminalCode = a.TerminalCode
                  AND t.branchcode = a.branchcode
            ) term
        WHERE a.TransactionType = 1007
          AND a.CustomerID IN ('204', '205', '206', '207', '208')
          AND a.LogDateTimeTerminal >= @RangeStart
          AND a.LogDateTimeTerminal < @RangeEndExclusive
          AND a.BranchCode = TRY_CAST(@SchoolId AS SMALLINT)
          AND (@TerminalCode = '' OR a.TerminalCode = @TerminalCode)
        OPTION (RECOMPILE);
    END

    CREATE CLUSTERED INDEX CX_AdminTxn ON #TMP ([Datetime] DESC, TransactionID);

    SELECT @TotalCount = COUNT(*)
    FROM #TMP;

    IF (@Length IS NULL OR @Length <= 0)
    BEGIN
        SELECT
            [Datetime],
            StudentID,
            Amount,
            [Name],
            Class,
            TransactionType,
            VAT,
            Terminal,
            TransactionID
        FROM #TMP
        ORDER BY [Datetime] DESC, TransactionID;
    END
    ELSE
    BEGIN
        SELECT
            [Datetime],
            StudentID,
            Amount,
            [Name],
            Class,
            TransactionType,
            VAT,
            Terminal,
            TransactionID
        FROM #TMP
        ORDER BY [Datetime] DESC, TransactionID
        OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;
    END
END
GO
