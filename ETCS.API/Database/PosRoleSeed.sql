/*
Seed POS staff role for ETCS.Pos.Web login.

1) Run against ibonus (RoleInfo used by LoginAccount.RoleID).
2) Optionally run AdminRole section against MealDB so the role appears in AdminRole
   without Admin portal module permissions.

Staff assignment: Admin → Manage Staff → set Role = POS.
*/
USE ibonus;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.RoleInfo WHERE LTRIM(RTRIM(RoleName)) = N'POS')
BEGIN
    DECLARE @NextRoleId INT =
        ISNULL((SELECT MAX(RoleID) FROM dbo.RoleInfo), 0) + 1;

    INSERT INTO dbo.RoleInfo (RoleID, RoleName)
    VALUES (@NextRoleId, N'POS');

    PRINT N'Inserted RoleInfo POS with RoleID = ' + CAST(@NextRoleId AS nvarchar(20));
END
ELSE
BEGIN
    PRINT N'RoleInfo POS already exists.';
END
GO

/*
Optional: mirror into MealDB.AdminRole (no AdminModule permissions).
RoleId must match ibonus.dbo.RoleInfo.RoleID for POS.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.AdminRole', N'U') IS NOT NULL
BEGIN
    DECLARE @PosRoleId INT =
    (
        SELECT TOP (1) RoleID
        FROM ibonus.dbo.RoleInfo
        WHERE LTRIM(RTRIM(RoleName)) = N'POS'
        ORDER BY RoleID
    );

    IF @PosRoleId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.AdminRole WHERE RoleId = @PosRoleId)
    BEGIN
        INSERT INTO dbo.AdminRole (RoleId, RoleName, IsSuperAdmin, IsSystem, IsActive)
        VALUES (@PosRoleId, N'POS', 0, 1, 1);

        PRINT N'Inserted AdminRole POS with RoleId = ' + CAST(@PosRoleId AS nvarchar(20));
    END
    ELSE IF @PosRoleId IS NOT NULL
    BEGIN
        UPDATE dbo.AdminRole
        SET RoleName = N'POS',
            IsSuperAdmin = 0,
            IsSystem = 1,
            IsActive = 1
        WHERE RoleId = @PosRoleId;

        PRINT N'Updated AdminRole POS (RoleId = ' + CAST(@PosRoleId AS nvarchar(20)) + N').';
    END
    ELSE
    BEGIN
        PRINT N'Skipped AdminRole seed: POS role not found in ibonus.RoleInfo.';
    END
END
GO
