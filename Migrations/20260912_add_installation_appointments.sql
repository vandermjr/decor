-- Fase P5, Passo 4 - Agendamento e histórico de reagendamentos

CREATE TABLE IF NOT EXISTS installation_appointments (
    AppointmentID INT NOT NULL AUTO_INCREMENT,
    OrderItemID INT NOT NULL,
    ScheduledDate DATE NOT NULL,
    ScheduledTime TIME NULL,
    ExecutorEmployeeID INT NULL,
    ExecutorPartnerID INT NULL,
    Status TINYINT UNSIGNED NOT NULL,
    CreatedByEmployeeID INT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    PRIMARY KEY (AppointmentID),
    KEY IX_installation_appointments_OrderItemID (OrderItemID),
    KEY IX_installation_appointments_ScheduledDate (ScheduledDate),
    KEY IX_installation_appointments_ExecutorEmployeeID (ExecutorEmployeeID),
    KEY IX_installation_appointments_ExecutorPartnerID (ExecutorPartnerID),
    CONSTRAINT FK_installation_appointments_order_items FOREIGN KEY (OrderItemID) REFERENCES order_items (OrderItemID),
    CONSTRAINT FK_installation_appointments_executor_employees FOREIGN KEY (ExecutorEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT FK_installation_appointments_executor_partners FOREIGN KEY (ExecutorPartnerID) REFERENCES partners (PartnerID),
    CONSTRAINT FK_installation_appointments_created_by FOREIGN KEY (CreatedByEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT CK_installation_appointments_executor CHECK ((ExecutorEmployeeID IS NOT NULL AND ExecutorPartnerID IS NULL) OR (ExecutorEmployeeID IS NULL AND ExecutorPartnerID IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS appointment_reschedules (
    RescheduleID INT NOT NULL AUTO_INCREMENT,
    AppointmentID INT NOT NULL,
    PreviousDate DATE NOT NULL,
    NewDate DATE NOT NULL,
    Reason TEXT NOT NULL,
    RegisteredByEmployeeID INT NOT NULL,
    RegisteredAt DATETIME NOT NULL,
    PRIMARY KEY (RescheduleID),
    KEY IX_appointment_reschedules_AppointmentID (AppointmentID),
    CONSTRAINT FK_appointment_reschedules_appointments FOREIGN KEY (AppointmentID) REFERENCES installation_appointments (AppointmentID),
    CONSTRAINT FK_appointment_reschedules_employees FOREIGN KEY (RegisteredByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('InstallationAppointments.View', 'Consultar agendamentos de instalação'),
    ('InstallationAppointments.Create', 'Criar agendamentos de instalação'),
    ('InstallationAppointments.Reschedule', 'Reagendar instalações'),
    ('InstallationAppointments.Cancel', 'Cancelar agendamentos de instalação')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN ('InstallationAppointments.View', 'InstallationAppointments.Create', 'InstallationAppointments.Reschedule', 'InstallationAppointments.Cancel');