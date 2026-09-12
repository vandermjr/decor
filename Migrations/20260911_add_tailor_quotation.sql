-- Fase P4, Passo 3 — TailorQuotation (solicitações de cotação sob medida)
-- Tabelas: tailor_quotation_requests, tailor_quotation_revisions

CREATE TABLE IF NOT EXISTS tailor_quotation_requests (
    RequestID INT NOT NULL AUTO_INCREMENT,
    QuoteItemID INT NOT NULL,
    PartnerID INT NOT NULL,
    RequestedByEmployeeID INT NOT NULL,
    RequestedAt DATETIME NOT NULL,
    Deadline DATETIME NULL,
    Status TINYINT UNSIGNED NOT NULL,
    PRIMARY KEY (RequestID),
    KEY IX_tailor_quotation_requests_QuoteItemID (QuoteItemID),
    KEY IX_tailor_quotation_requests_PartnerID (PartnerID),
    KEY IX_tailor_quotation_requests_RequestedByEmployeeID (RequestedByEmployeeID),
    CONSTRAINT FK_tailor_quotation_requests_quote_items FOREIGN KEY (QuoteItemID) REFERENCES quote_items (QuoteItemID),
    CONSTRAINT FK_tailor_quotation_requests_partners FOREIGN KEY (PartnerID) REFERENCES partners (PartnerID),
    CONSTRAINT FK_tailor_quotation_requests_employees FOREIGN KEY (RequestedByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS tailor_quotation_revisions (
    RevisionID INT NOT NULL AUTO_INCREMENT,
    RequestID INT NOT NULL,
    RevisionNumber INT NOT NULL,
    Price DECIMAL(12,2) NOT NULL,
    ChangeReason VARCHAR(500) NULL,
    RespondedAt DATETIME NOT NULL,
    RegisteredByEmployeeID INT NOT NULL,
    PRIMARY KEY (RevisionID),
    KEY IX_tailor_quotation_revisions_RequestID (RequestID),
    KEY IX_tailor_quotation_revisions_RegisteredByEmployeeID (RegisteredByEmployeeID),
    CONSTRAINT FK_tailor_quotation_revisions_requests FOREIGN KEY (RequestID) REFERENCES tailor_quotation_requests (RequestID),
    CONSTRAINT FK_tailor_quotation_revisions_employees FOREIGN KEY (RegisteredByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('TailorQuotations.View', 'Consultar solicitações de cotação sob medida'),
    ('TailorQuotations.Create', 'Criar solicitações de cotação sob medida'),
    ('TailorQuotations.Respond', 'Registrar revisões de cotação sob medida'),
    ('TailorQuotations.Close', 'Fechar solicitações de cotação sob medida')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN (
      'TailorQuotations.View',
      'TailorQuotations.Create',
      'TailorQuotations.Respond',
      'TailorQuotations.Close'
  );
