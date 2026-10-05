
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER FUNCTION [dbo].[fnSplitCsv](@Csv VARCHAR(MAX))
RETURNS TABLE
AS
RETURN
(
    SELECT LTRIM(RTRIM(x.i.value('.', 'VARCHAR(50)'))) AS value
    FROM (
        SELECT CAST(
            '<i>'
            + REPLACE(
                REPLACE(ISNULL(@Csv, ''), '&', '&amp;'),
                ',',
                '</i><i>')
            + '</i>' AS XML) AS d
    ) t
    CROSS APPLY t.d.nodes('/i') x(i)
    WHERE LTRIM(RTRIM(x.i.value('.', 'VARCHAR(50)'))) <> ''
);
GO
