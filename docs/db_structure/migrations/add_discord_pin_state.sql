IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'discord_pin_state'
)
BEGIN
    CREATE TABLE discord_pin_state (
        pin_slot           TINYINT      NOT NULL,
        discord_channel_id VARCHAR(20)  NOT NULL,
        discord_message_id VARCHAR(20)  NOT NULL,
        CONSTRAINT PK_discord_pin_state PRIMARY KEY (pin_slot)
    );
END
