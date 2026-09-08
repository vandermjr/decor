-- Run after 20260830_add_authentication_authorization.sql.
-- Existing users keep normal access by default. The seeded admin is marked only when it
-- still has the initial temporary credential created by the preceding migration.
ALTER TABLE users
    ADD COLUMN MustChangePassword TINYINT(1) NOT NULL DEFAULT 0
    AFTER PasswordHash;

UPDATE users
SET MustChangePassword = 1
WHERE Username = 'admin'
  AND PasswordHash = 'PBKDF2-SHA256$210000$jvVoQcXYELRUbyk8pMrW9w==$SIOahYSewK5diBBU9Dl8CIjTAJSnaDgn0aFHSUhZijs=';
