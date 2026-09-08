/*
    Student Consumption Report — production setup (MealDB)

    Run on: MealDB (same database as AdminModule / AdminRolePermission)
    Requires: No ibonus schema changes (report reads AccessLog + StudentLogin via app)

    After running:
    1. Deploy updated ETCS.Admin + ETCS.Shared assemblies
    2. Log out and back in (or refresh role permissions) to see "Student Consumption" under Reports
    3. Grant View to other roles via Manage Roles if needed
*/

SET NOCOUNT ON;

DECLARE @ModuleId INT;
DECLARE @AdminRoleId INT;

-- Change this if your primary admin role is not named 'Admin'
DECLARE @AdminRoleName NVARCHAR(100) = N'Admin';

SELECT TOP (1)
    @AdminRoleId = RoleId
FROM AdminRole
WHERE ISNULL(IsActive, 1) = 1
  AND (
        ISNULL(IsSuperAdmin, 0) = 1
        OR LTRIM(RTRIM(RoleName)) = @AdminRoleName
      )
ORDER BY ISNULL(IsSuperAdmin, 0) DESC, RoleId;

IF @AdminRoleId IS NULL
BEGIN
    RAISERROR(
        'Admin role not found. Set @AdminRoleName or assign permissions manually in Manage Roles.',
        16,
        1);
    RETURN;
END;

IF NOT EXISTS (
    SELECT 1
    FROM AdminModule
    WHERE LTRIM(RTRIM(ModuleKey)) = 'Report.StudentConsumption')
BEGIN
    INSERT INTO AdminModule (
        ModuleKey,
        DisplayName,
        GroupName,
        ControllerName,
        ActionName,
        SortOrder,
        IsActive)
    VALUES (
        'Report.StudentConsumption',
        'Student Consumption',
        'Reports',
        'Report',
        'StudentConsumption',
        36,
        1);

    PRINT 'Inserted AdminModule: Report.StudentConsumption';
END
ELSE
BEGIN
    PRINT 'AdminModule Report.StudentConsumption already exists.';
END;

SELECT @ModuleId = ModuleId
FROM AdminModule
WHERE LTRIM(RTRIM(ModuleKey)) = 'Report.StudentConsumption';

IF @ModuleId IS NULL
BEGIN
    RAISERROR('Failed to resolve ModuleId for Report.StudentConsumption.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (
    SELECT 1
    FROM AdminRolePermission
    WHERE RoleId = @AdminRoleId
      AND ModuleId = @ModuleId)
BEGIN
    INSERT INTO AdminRolePermission (
        RoleId,
        ModuleId,
        CanView,
        CanAdd,
        CanEdit,
        CanDelete)
    VALUES (
        @AdminRoleId,
        @ModuleId,
        1,
        0,
        0,
        0);

    PRINT 'Granted View permission to admin role RoleId=' + CAST(@AdminRoleId AS VARCHAR(20));
END
ELSE
BEGIN
    PRINT 'View permission already exists for admin role RoleId=' + CAST(@AdminRoleId AS VARCHAR(20));
END;

SELECT
    m.ModuleId,
    m.ModuleKey,
    m.DisplayName,
    m.GroupName,
    m.ControllerName,
    m.ActionName,
    m.SortOrder,
    m.IsActive
FROM AdminModule m
WHERE m.ModuleId = @ModuleId;

SELECT
    p.RoleId,
    r.RoleName,
    p.CanView,
    p.CanAdd,
    p.CanEdit,
    p.CanDelete
FROM AdminRolePermission p
INNER JOIN AdminRole r ON r.RoleId = p.RoleId
WHERE p.ModuleId = @ModuleId;
