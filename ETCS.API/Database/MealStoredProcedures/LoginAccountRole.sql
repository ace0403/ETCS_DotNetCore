
USE MealDB;
GO

IF OBJECT_ID(N'dbo.LoginAccountRole', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginAccountRole
    (
        LoginAccountId INT NOT NULL,
        RoleId         INT NOT NULL,
        IsDefault      BIT NOT NULL CONSTRAINT DF_LoginAccountRole_IsDefault DEFAULT (0),
        CONSTRAINT PK_LoginAccountRole PRIMARY KEY (LoginAccountId, RoleId)
    );

    CREATE INDEX IX_LoginAccountRole_RoleId ON dbo.LoginAccountRole (RoleId);
END
GO
