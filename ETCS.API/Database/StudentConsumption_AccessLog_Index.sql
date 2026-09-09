
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_AccessLog_CustomerID_TransactionType_LogDateTimeServer'
      AND object_id = OBJECT_ID('dbo.AccessLog'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AccessLog_CustomerID_TransactionType_LogDateTimeServer
        ON dbo.AccessLog (CustomerID, TransactionType, LogDateTimeServer)
        INCLUDE (Amount, TransactionID);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_StudentLogin_StudSchoolId'
      AND object_id = OBJECT_ID('dbo.StudentLogin'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_StudentLogin_StudSchoolId
        ON dbo.StudentLogin (StudSchoolId)
        INCLUDE (UserId, CustomerID, StudCode, StudFirstName, StudLastName, StudStd);
END;
