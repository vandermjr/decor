CREATE TABLE IF NOT EXISTS system_settings (
    SettingKey VARCHAR(100) NOT NULL,
    SettingValue VARCHAR(255) NOT NULL,
    PRIMARY KEY (SettingKey)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT IGNORE INTO system_settings (SettingKey, SettingValue)
SELECT 'IconWeight', SettingValue
FROM user_settings
WHERE UserID = (SELECT UserID FROM users WHERE Username = 'admin' LIMIT 1)
  AND SettingKey = 'IconWeight';

INSERT IGNORE INTO system_settings (SettingKey, SettingValue)
SELECT 'IconStrokeThickness', SettingValue
FROM user_settings
WHERE UserID = (SELECT UserID FROM users WHERE Username = 'admin' LIMIT 1)
  AND SettingKey = 'IconStrokeThickness';