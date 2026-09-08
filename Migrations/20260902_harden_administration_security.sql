-- Enforces the override domain for databases upgraded from the previous migration.
ALTER TABLE user_permission_overrides
    ADD CONSTRAINT CK_user_permission_overrides_IsGranted CHECK (IsGranted IN (0, 1));
