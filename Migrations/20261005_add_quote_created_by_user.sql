ALTER TABLE quotes
    ADD COLUMN CreatedByUserID INT NULL AFTER CreatedByEmployeeID,
    ADD KEY IX_quotes_CreatedByUserID (CreatedByUserID),
    ADD CONSTRAINT FK_quotes_created_by_users FOREIGN KEY (CreatedByUserID) REFERENCES users (UserID);