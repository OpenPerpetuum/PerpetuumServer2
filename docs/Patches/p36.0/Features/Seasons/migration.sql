-- Seasons System Migration
-- Run once against the game database before deploying the updated server binary.

CREATE TABLE seasons (
    id          INT IDENTITY(1,1) NOT NULL,
    name        VARCHAR(128)      NOT NULL,
    description VARCHAR(512)      NOT NULL DEFAULT '',
    start_time  DATETIME          NOT NULL,
    end_time    DATETIME          NOT NULL,
    is_active   BIT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_seasons PRIMARY KEY (id)
);

CREATE TABLE season_activity_rates (
    id              INT IDENTITY(1,1) NOT NULL,
    season_id       INT               NOT NULL REFERENCES seasons(id),
    activity_type   INT               NOT NULL,
    points_per_unit FLOAT             NOT NULL,
    unit_scale      INT               NOT NULL DEFAULT 1,
    CONSTRAINT PK_season_activity_rates PRIMARY KEY (id)
);

CREATE TABLE season_objectives (
    id            INT IDENTITY(1,1) NOT NULL,
    season_id     INT               NOT NULL REFERENCES seasons(id),
    name          VARCHAR(128)      NOT NULL,
    description   VARCHAR(512)      NOT NULL DEFAULT '',
    activity_type INT               NOT NULL,
    target_value  BIGINT            NOT NULL,
    bonus_points  INT               NOT NULL,
    display_order INT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_season_objectives PRIMARY KEY (id)
);

CREATE TABLE season_tiers (
    id              INT IDENTITY(1,1) NOT NULL,
    season_id       INT               NOT NULL REFERENCES seasons(id),
    tier_number     INT               NOT NULL,
    tier_name       VARCHAR(64)       NOT NULL,
    points_required INT               NOT NULL,
    package_id      INT               NOT NULL,
    CONSTRAINT PK_season_tiers PRIMARY KEY (id)
);

CREATE TABLE season_leaderboard_rewards (
    id         INT IDENTITY(1,1) NOT NULL,
    season_id  INT               NOT NULL REFERENCES seasons(id),
    rank_min   INT               NOT NULL,
    rank_max   INT               NOT NULL,
    package_id INT               NOT NULL,
    CONSTRAINT PK_season_leaderboard_rewards PRIMARY KEY (id)
);

CREATE TABLE season_character_points (
    character_id                 INT      NOT NULL,
    season_id                    INT      NOT NULL REFERENCES seasons(id),
    total_points                 BIGINT   NOT NULL DEFAULT 0,
    last_updated                 DATETIME NOT NULL DEFAULT GETUTCDATE(),
    intro_mail_sent              BIT      NOT NULL DEFAULT 0,
    leaderboard_reward_delivered BIT      NOT NULL DEFAULT 0,
    CONSTRAINT PK_season_character_points PRIMARY KEY (character_id, season_id)
);

CREATE TABLE season_objective_progress (
    character_id   INT          NOT NULL,
    season_id      INT          NOT NULL REFERENCES seasons(id),
    objective_id   INT          NOT NULL REFERENCES season_objectives(id),
    current_value  BIGINT       NOT NULL DEFAULT 0,
    completed      BIT          NOT NULL DEFAULT 0,
    completed_time DATETIME         NULL,
    bonus_awarded  BIT          NOT NULL DEFAULT 0,
    CONSTRAINT PK_season_objective_progress PRIMARY KEY (character_id, season_id, objective_id)
);

CREATE TABLE season_tier_claims (
    character_id INT      NOT NULL,
    season_id    INT      NOT NULL REFERENCES seasons(id),
    tier_id      INT      NOT NULL REFERENCES season_tiers(id),
    claimed_time DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_season_tier_claims PRIMARY KEY (character_id, season_id, tier_id)
);

-- Indexes for common query patterns
CREATE INDEX IX_season_character_points_season ON season_character_points (season_id, total_points DESC);
CREATE INDEX IX_season_objective_progress_char ON season_objective_progress (character_id, season_id);
CREATE INDEX IX_season_tier_claims_char        ON season_tier_claims (character_id, season_id);
