-- Normalize the protected group identifiers while preserving role IDs and assignments.

UPDATE roles canonical
INNER JOIN roles legacy ON legacy.RoleName = 'Administrador'
SET canonical.HierarchyLevel = GREATEST(canonical.HierarchyLevel, legacy.HierarchyLevel),
    canonical.IsSystemProtected = GREATEST(canonical.IsSystemProtected, legacy.IsSystemProtected),
    canonical.Description = COALESCE(canonical.Description, legacy.Description)
WHERE canonical.RoleName = 'Administradores';

INSERT IGNORE INTO user_roles (UserID, RoleID)
SELECT legacy_assignment.UserID, canonical.RoleID
FROM user_roles legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Administrador'
INNER JOIN roles canonical ON canonical.RoleName = 'Administradores';

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT canonical.RoleID, legacy_assignment.PermissionID
FROM role_permissions legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Administrador'
INNER JOIN roles canonical ON canonical.RoleName = 'Administradores';

DELETE legacy_assignment
FROM user_roles legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Administrador'
INNER JOIN roles canonical ON canonical.RoleName = 'Administradores';

DELETE legacy_assignment
FROM role_permissions legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Administrador'
INNER JOIN roles canonical ON canonical.RoleName = 'Administradores';

DELETE legacy
FROM roles legacy
INNER JOIN roles canonical ON canonical.RoleName = 'Administradores'
WHERE legacy.RoleName = 'Administrador';

UPDATE roles
SET RoleName = 'Administradores'
WHERE RoleName = 'Administrador';

UPDATE roles canonical
INNER JOIN roles legacy ON legacy.RoleName = 'Supervisor'
SET canonical.HierarchyLevel = GREATEST(canonical.HierarchyLevel, legacy.HierarchyLevel),
    canonical.IsSystemProtected = GREATEST(canonical.IsSystemProtected, legacy.IsSystemProtected),
    canonical.Description = COALESCE(canonical.Description, legacy.Description)
WHERE canonical.RoleName = 'Supervisores';

INSERT IGNORE INTO user_roles (UserID, RoleID)
SELECT legacy_assignment.UserID, canonical.RoleID
FROM user_roles legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Supervisor'
INNER JOIN roles canonical ON canonical.RoleName = 'Supervisores';

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT canonical.RoleID, legacy_assignment.PermissionID
FROM role_permissions legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Supervisor'
INNER JOIN roles canonical ON canonical.RoleName = 'Supervisores';

DELETE legacy_assignment
FROM user_roles legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Supervisor'
INNER JOIN roles canonical ON canonical.RoleName = 'Supervisores';

DELETE legacy_assignment
FROM role_permissions legacy_assignment
INNER JOIN roles legacy ON legacy.RoleID = legacy_assignment.RoleID AND legacy.RoleName = 'Supervisor'
INNER JOIN roles canonical ON canonical.RoleName = 'Supervisores';

DELETE legacy
FROM roles legacy
INNER JOIN roles canonical ON canonical.RoleName = 'Supervisores'
WHERE legacy.RoleName = 'Supervisor';

UPDATE roles
SET RoleName = 'Supervisores'
WHERE RoleName = 'Supervisor';