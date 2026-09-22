CREATE TABLE IF NOT EXISTS user_settings (
    UserID INT NOT NULL,
    SettingKey VARCHAR(128) NOT NULL,
    SettingValue VARCHAR(512) NOT NULL,
    ValueType VARCHAR(32) NOT NULL,
    PRIMARY KEY (UserID, SettingKey),
    CONSTRAINT FK_user_settings_users FOREIGN KEY (UserID) REFERENCES users (UserID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
