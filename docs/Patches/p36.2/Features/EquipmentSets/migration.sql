-- Equipment Set Synergy Bonuses Migration (IMPROVEMENT-025)
-- Run once against the game database before deploying the updated server binary.

CREATE TABLE equipment_sets (
    set_id  INT          NOT NULL IDENTITY(1,1),
    name    NVARCHAR(64) NOT NULL,
    CONSTRAINT PK_equipment_sets PRIMARY KEY (set_id),
    CONSTRAINT UQ_equipment_sets_name UNIQUE (name)
);

CREATE TABLE equipment_set_members (
    set_id      INT NOT NULL,
    definition  INT NOT NULL,
    CONSTRAINT PK_equipment_set_members PRIMARY KEY (set_id, definition),
    CONSTRAINT FK_equipment_set_members_set FOREIGN KEY (set_id)
        REFERENCES equipment_sets (set_id),
    CONSTRAINT FK_equipment_set_members_def FOREIGN KEY (definition)
        REFERENCES entitydefaults (definition)
);

CREATE TABLE equipment_set_bonus_thresholds (
    set_id          INT   NOT NULL,
    required_pieces INT   NOT NULL,
    aggregate_field INT   NOT NULL,
    bonus_value     FLOAT NOT NULL,
    CONSTRAINT PK_equipment_set_bonus_thresholds
        PRIMARY KEY (set_id, required_pieces, aggregate_field),
    CONSTRAINT FK_equipment_set_bonus_thresholds_set FOREIGN KEY (set_id)
        REFERENCES equipment_sets (set_id)
);
