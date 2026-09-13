-- Fase P5, Passo 5 - Confirmação de execução do serviço

CREATE TABLE IF NOT EXISTS service_execution_records (
    ExecutionID INT NOT NULL AUTO_INCREMENT,
    AppointmentID INT NOT NULL,
    ExecutedAt DATETIME NOT NULL,
    Observations TEXT NULL,
    CustomerPresent TINYINT(1) NOT NULL,
    CustomerSignedConfirmation TINYINT(1) NULL,
    AbsentAuthorizationNote TEXT NULL,
    PRIMARY KEY (ExecutionID),
    UNIQUE KEY UX_service_execution_records_AppointmentID (AppointmentID),
    CONSTRAINT FK_service_execution_records_appointments FOREIGN KEY (AppointmentID) REFERENCES installation_appointments (AppointmentID),
    CONSTRAINT CK_service_execution_records_confirmation CHECK (
        (CustomerPresent = 1 AND CustomerSignedConfirmation IS NOT NULL AND (AbsentAuthorizationNote IS NULL OR CHAR_LENGTH(TRIM(AbsentAuthorizationNote)) = 0))
        OR (CustomerPresent = 0 AND CustomerSignedConfirmation IS NULL AND AbsentAuthorizationNote IS NOT NULL AND CHAR_LENGTH(TRIM(AbsentAuthorizationNote)) > 0)
    )
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('ServiceExecutionRecords.View', 'Consultar execuções de serviços'),
    ('ServiceExecutionRecords.Create', 'Registrar execução de serviços')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN ('ServiceExecutionRecords.View', 'ServiceExecutionRecords.Create');
