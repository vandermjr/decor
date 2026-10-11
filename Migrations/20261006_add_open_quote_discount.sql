ALTER TABLE quotes
    DROP FOREIGN KEY FK_quotes_customers,
    DROP FOREIGN KEY FK_quotes_employees;

ALTER TABLE quotes
    MODIFY COLUMN CustomerID INT NULL,
    MODIFY COLUMN CreatedByEmployeeID INT NULL,
    ADD COLUMN DiscountAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    ADD CONSTRAINT CK_quotes_DiscountAmount CHECK (DiscountAmount >= 0),
    ADD CONSTRAINT FK_quotes_customers FOREIGN KEY (CustomerID) REFERENCES customers (CustomerID),
    ADD CONSTRAINT FK_quotes_employees FOREIGN KEY (CreatedByEmployeeID) REFERENCES employees (EmployeeID);