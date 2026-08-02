/*
Deploy on MealDB database.
Admin panel role-based permissions and future multi-school assignment.

IMPORTANT: Verify AdminRole.RoleId values match ibonus.dbo.RoleInfo before/after deploy:
    SELECT RoleID, RoleName FROM ibonus.dbo.RoleInfo ORDER BY RoleID;

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.AdminModule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminModule
    (
        ModuleId   INT            IDENTITY(1, 1) NOT NULL CONSTRAINT PK_AdminModule PRIMARY KEY,
        ModuleKey  NVARCHAR(100)  NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        GroupName      NVARCHAR(50)   NOT NULL,
        ControllerName NVARCHAR(100)  NULL,
        ActionName     NVARCHAR(100)  NULL,
        SortOrder      INT            NOT NULL CONSTRAINT DF_AdminModule_SortOrder DEFAULT (0),
        IsActive       BIT            NOT NULL CONSTRAINT DF_AdminModule_IsActive DEFAULT (1),
        CONSTRAINT UQ_AdminModule_ModuleKey UNIQUE (ModuleKey)
    );
END
GO

IF COL_LENGTH(N'dbo.AdminModule', N'ControllerName') IS NULL
BEGIN
    ALTER TABLE dbo.AdminModule ADD ControllerName NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH(N'dbo.AdminModule', N'ActionName') IS NULL
BEGIN
    ALTER TABLE dbo.AdminModule ADD ActionName NVARCHAR(100) NULL;
END
GO

IF OBJECT_ID(N'dbo.AdminRole', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminRole
    (
        RoleId       INT           NOT NULL CONSTRAINT PK_AdminRole PRIMARY KEY,
        RoleName     NVARCHAR(100) NOT NULL,
        IsSuperAdmin BIT           NOT NULL CONSTRAINT DF_AdminRole_IsSuperAdmin DEFAULT (0),
        IsSystem     BIT           NOT NULL CONSTRAINT DF_AdminRole_IsSystem DEFAULT (0),
        IsActive     BIT           NOT NULL CONSTRAINT DF_AdminRole_IsActive DEFAULT (1),
        CONSTRAINT UQ_AdminRole_RoleName UNIQUE (RoleName)
    );
END
GO

IF OBJECT_ID(N'dbo.AdminRolePermission', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminRolePermission
    (
        RoleId    INT NOT NULL,
        ModuleId  INT NOT NULL,
        CanView   BIT NOT NULL CONSTRAINT DF_AdminRolePermission_CanView DEFAULT (0),
        CanAdd    BIT NOT NULL CONSTRAINT DF_AdminRolePermission_CanAdd DEFAULT (0),
        CanEdit   BIT NOT NULL CONSTRAINT DF_AdminRolePermission_CanEdit DEFAULT (0),
        CanDelete BIT NOT NULL CONSTRAINT DF_AdminRolePermission_CanDelete DEFAULT (0),
        CONSTRAINT PK_AdminRolePermission PRIMARY KEY (RoleId, ModuleId),
        CONSTRAINT FK_AdminRolePermission_Role FOREIGN KEY (RoleId) REFERENCES dbo.AdminRole (RoleId),
        CONSTRAINT FK_AdminRolePermission_Module FOREIGN KEY (ModuleId) REFERENCES dbo.AdminModule (ModuleId)
    );

    CREATE INDEX IX_AdminRolePermission_ModuleId ON dbo.AdminRolePermission (ModuleId);
END
GO

IF OBJECT_ID(N'dbo.LoginAccountSchool', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginAccountSchool
    (
        LoginAccountId INT NOT NULL,
        SchoolId       INT NOT NULL,
        CONSTRAINT PK_LoginAccountSchool PRIMARY KEY (LoginAccountId, SchoolId)
    );

    CREATE INDEX IX_LoginAccountSchool_SchoolId ON dbo.LoginAccountSchool (SchoolId);
END
GO

/* ---- Seed modules ---- */
MERGE dbo.AdminModule AS t
USING (
    VALUES
        (N'Dashboard',                   N'Dashboard',                  N'General',   N'Dashboard',         N'Index',                10),
        (N'School',                      N'Manage Schools',             N'Master',    N'School',            N'Index',                20),
        (N'Guardian',                    N'Manage Parents',             N'Master',    N'Guardian',          N'Index',                30),
        (N'Student',                     N'Manage Students',            N'Master',    N'Student',           N'Index',                40),
        (N'Staff',                       N'Manage Staff',               N'Master',    N'Staff',             N'Index',                50),
        (N'EmailTemplate',               N'Email Templates',            N'Master',    N'EmailTemplate',     N'Index',                60),
        (N'InAppNotification',           N'In-App Notifications',       N'Master',    N'InAppNotification', N'Index',                65),
        (N'Category',                    N'Menu Category',              N'Inventory', N'Category',          N'Index',                70),
        (N'Ingredient',                  N'Ingredients',                N'Inventory', N'Ingredient',        N'Index',                80),
        (N'MealItem',                    N'Item Master',                N'Inventory', N'MealItem',          N'Index',                90),
        (N'MealCombo',                   N'Meal Combo',                 N'Inventory', N'MealCombo',         N'Index',                100),
        (N'MealServingPeriod',           N'Serving Period',             N'Inventory', N'MealServingPeriod', N'Index',                110),
        (N'Report.CanteenTransactions',  N'Canteen Transactions',       N'Reports',   N'Report',            N'CanteenTransactions',  120),
        (N'Report.AdminTransaction',     N'Report on Transactions',     N'Reports',   N'Report',            N'AdminTransaction',     130),
        (N'Report.TerminalSalesSummary', N'Terminal Sales Summary',   N'Reports',   N'Report',            N'TerminalSalesSummary', 140),
        (N'Report.MealOrdersMealDb',     N'Meal Order Report (NEW)',    N'Reports',   N'Report',            N'MealOrdersMealDb',     150),
        (N'Report.MealOrders',           N'Meal Order Report (OLD)',    N'Reports',   N'Report',            N'MealOrders',           160),
        (N'Role',                        N'Manage Roles',               N'Settings',  N'Role',              N'Index',                170)
) AS s (ModuleKey, DisplayName, GroupName, ControllerName, ActionName, SortOrder)
ON t.ModuleKey = s.ModuleKey
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = s.DisplayName,
        GroupName = s.GroupName,
        ControllerName = s.ControllerName,
        ActionName = s.ActionName,
        SortOrder = s.SortOrder
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ModuleKey, DisplayName, GroupName, ControllerName, ActionName, SortOrder, IsActive)
    VALUES (s.ModuleKey, s.DisplayName, s.GroupName, s.ControllerName, s.ActionName, s.SortOrder, 1);
GO

/*
Seed roles — adjust RoleId to match ibonus.RoleInfo.RoleID for each RoleName.
Example mapping (verify in your environment):
    Account      -> RoleId 1
    Terminal     -> RoleId 2
    Admin        -> RoleId 3  (full access)
    School Admin -> RoleId 4
*/
MERGE dbo.AdminRole AS t
USING (
    VALUES
        (1, N'School Admin', 0, 1, 1),
        (2, N'Account',0, 1, 1),
        (3, N'Admin', 1, 1, 1),
        (4, N'Terminal', 0, 1, 1)
) AS s (RoleId, RoleName, IsSuperAdmin, IsSystem, IsActive)
ON t.RoleId = s.RoleId
WHEN MATCHED THEN
    UPDATE SET
        RoleName = s.RoleName,
        IsSuperAdmin = s.IsSuperAdmin,
        IsSystem = s.IsSystem,
        IsActive = s.IsActive
WHEN NOT MATCHED BY TARGET THEN
    INSERT (RoleId, RoleName, IsSuperAdmin, IsSystem, IsActive)
    VALUES (s.RoleId, s.RoleName, s.IsSuperAdmin, s.IsSystem, s.IsActive);
GO

/* Grant Admin role full permissions on every module */
DECLARE @AdminRoleId INT = (SELECT TOP (1) RoleId FROM dbo.AdminRole WHERE RoleName = N'Admin');

IF @AdminRoleId IS NOT NULL
BEGIN
    MERGE dbo.AdminRolePermission AS t
    USING (
        SELECT @AdminRoleId AS RoleId, m.ModuleId
        FROM dbo.AdminModule m
        WHERE m.IsActive = 1
    ) AS s
    ON t.RoleId = s.RoleId AND t.ModuleId = s.ModuleId
    WHEN MATCHED THEN
        UPDATE SET CanView = 1, CanAdd = 1, CanEdit = 1, CanDelete = 1
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (RoleId, ModuleId, CanView, CanAdd, CanEdit, CanDelete)
        VALUES (s.RoleId, s.ModuleId, 1, 1, 1, 1);
END
GO
