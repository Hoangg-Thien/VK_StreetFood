-- Migration: 001_create_schema
-- Description: Create all tables for VK Street Food Tour
-- Project: VK_StreetFood (Vĩnh Khánh Food Street, Quận 4, TP.HCM)

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================
-- CATEGORIES
-- ============================================================
CREATE TABLE categories (
  id          SERIAL PRIMARY KEY,
  name        TEXT NOT NULL UNIQUE,
  description TEXT,
  icon_url    TEXT,
  display_order INTEGER NOT NULL DEFAULT 0,
  is_active   BOOLEAN NOT NULL DEFAULT TRUE,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- TAGS
-- ============================================================
CREATE TABLE tags (
  id         UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  name       TEXT NOT NULL UNIQUE,
  color_code TEXT NOT NULL DEFAULT '#3B82F6'
);

-- ============================================================
-- POINTS OF INTEREST
-- ============================================================
CREATE TABLE points_of_interest (
  id            SERIAL PRIMARY KEY,
  name          TEXT NOT NULL,
  description   TEXT,
  latitude      DOUBLE PRECISION NOT NULL,
  longitude     DOUBLE PRECISION NOT NULL,
  address       TEXT,
  image_url     TEXT,
  category_id   INTEGER REFERENCES categories(id) ON DELETE SET NULL,
  average_rating NUMERIC(3,2) NOT NULL DEFAULT 0,
  total_ratings  INTEGER NOT NULL DEFAULT 0,
  view_count     INTEGER NOT NULL DEFAULT 0,
  is_active      BOOLEAN NOT NULL DEFAULT TRUE,
  is_deleted     BOOLEAN NOT NULL DEFAULT FALSE,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  CONSTRAINT poi_lat_check CHECK (latitude BETWEEN 10.750 AND 10.770),
  CONSTRAINT poi_lng_check CHECK (longitude BETWEEN 106.695 AND 106.715)
);

-- ============================================================
-- POI TAGS (many-to-many)
-- ============================================================
CREATE TABLE poi_tags (
  poi_id INTEGER REFERENCES points_of_interest(id) ON DELETE CASCADE,
  tag_id UUID    REFERENCES tags(id)               ON DELETE CASCADE,
  PRIMARY KEY (poi_id, tag_id)
);

-- ============================================================
-- AUDIO CONTENTS
-- ============================================================
CREATE TABLE audio_contents (
  id                  SERIAL PRIMARY KEY,
  point_of_interest_id INTEGER NOT NULL REFERENCES points_of_interest(id) ON DELETE CASCADE,
  language_code       VARCHAR(5) NOT NULL CHECK (language_code IN ('vi', 'en', 'ko', 'ja', 'fr', 'zh')),
  text_content        TEXT,
  audio_file_url      TEXT,
  duration_in_seconds INTEGER,
  is_generated        BOOLEAN NOT NULL DEFAULT FALSE,
  is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  UNIQUE (point_of_interest_id, language_code)
);

-- ============================================================
-- TOURISTS (anonymous device-based)
-- ============================================================
CREATE TABLE tourists (
  id                  SERIAL PRIMARY KEY,
  device_id           TEXT NOT NULL UNIQUE,
  preferred_language  VARCHAR(5) NOT NULL DEFAULT 'vi',
  last_latitude       DOUBLE PRECISION,
  last_longitude      DOUBLE PRECISION,
  visit_count         INTEGER NOT NULL DEFAULT 0,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- VISIT LOGS
-- ============================================================
CREATE TABLE visit_logs (
  id                   SERIAL PRIMARY KEY,
  tourist_id           INTEGER REFERENCES tourists(id) ON DELETE SET NULL,
  point_of_interest_id INTEGER NOT NULL REFERENCES points_of_interest(id) ON DELETE CASCADE,
  trigger_method       VARCHAR(20) NOT NULL CHECK (trigger_method IN ('geofence', 'qr_code', 'manual', 'auto')),
  latitude             DOUBLE PRECISION,
  longitude            DOUBLE PRECISION,
  visited_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- FAVORITES
-- ============================================================
CREATE TABLE favorites (
  tourist_id           INTEGER NOT NULL REFERENCES tourists(id) ON DELETE CASCADE,
  point_of_interest_id INTEGER NOT NULL REFERENCES points_of_interest(id) ON DELETE CASCADE,
  created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (tourist_id, point_of_interest_id)
);

-- ============================================================
-- RATINGS
-- ============================================================
CREATE TABLE ratings (
  id                   SERIAL PRIMARY KEY,
  tourist_id           INTEGER REFERENCES tourists(id) ON DELETE SET NULL,
  point_of_interest_id INTEGER NOT NULL REFERENCES points_of_interest(id) ON DELETE CASCADE,
  rating               SMALLINT NOT NULL CHECK (rating BETWEEN 1 AND 5),
  comment              TEXT,
  created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (tourist_id, point_of_interest_id)
);

-- ============================================================
-- ANALYTICS EVENTS
-- ============================================================
CREATE TABLE analytics_events (
  id                   UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  tourist_id           INTEGER REFERENCES tourists(id) ON DELETE SET NULL,
  point_of_interest_id INTEGER REFERENCES points_of_interest(id) ON DELETE SET NULL,
  event_type           VARCHAR(30) NOT NULL CHECK (
    event_type IN ('view', 'audio_play', 'audio_complete', 'qr_scan', 'favorite_add', 'favorite_remove', 'rating_submit')
  ),
  language_code        VARCHAR(5),
  duration_seconds     INTEGER,
  created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- VENDORS
-- ============================================================
CREATE TABLE vendors (
  id                   SERIAL PRIMARY KEY,
  point_of_interest_id INTEGER NOT NULL REFERENCES points_of_interest(id) ON DELETE CASCADE,
  name                 TEXT NOT NULL,
  description          TEXT,
  phone_number         TEXT,
  image_url            TEXT,
  average_rating       NUMERIC(3,2) NOT NULL DEFAULT 0,
  is_active            BOOLEAN NOT NULL DEFAULT TRUE,
  created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- INDEXES
-- ============================================================
CREATE INDEX idx_poi_category      ON points_of_interest(category_id) WHERE NOT is_deleted;
CREATE INDEX idx_poi_location      ON points_of_interest(latitude, longitude) WHERE is_active AND NOT is_deleted;
CREATE INDEX idx_audio_poi_lang    ON audio_contents(point_of_interest_id, language_code) WHERE NOT is_deleted;
CREATE INDEX idx_visits_tourist    ON visit_logs(tourist_id);
CREATE INDEX idx_visits_poi        ON visit_logs(point_of_interest_id);
CREATE INDEX idx_visits_time       ON visit_logs(visited_at DESC);
CREATE INDEX idx_analytics_poi     ON analytics_events(point_of_interest_id) WHERE point_of_interest_id IS NOT NULL;
CREATE INDEX idx_analytics_type    ON analytics_events(event_type);
CREATE INDEX idx_analytics_time    ON analytics_events(created_at DESC);
CREATE INDEX idx_favorites_tourist ON favorites(tourist_id);

-- ============================================================
-- AUTO-UPDATE updated_at TRIGGER
-- ============================================================
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_poi_updated_at
  BEFORE UPDATE ON points_of_interest
  FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_audio_updated_at
  BEFORE UPDATE ON audio_contents
  FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_tourist_updated_at
  BEFORE UPDATE ON tourists
  FOR EACH ROW EXECUTE FUNCTION update_updated_at();
