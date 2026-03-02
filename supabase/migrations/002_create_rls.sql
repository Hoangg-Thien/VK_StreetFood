-- Migration: 002_create_rls
-- Description: Row Level Security policies for public read, authenticated write

-- Enable RLS on all tables
ALTER TABLE categories            ENABLE ROW LEVEL SECURITY;
ALTER TABLE tags                  ENABLE ROW LEVEL SECURITY;
ALTER TABLE points_of_interest    ENABLE ROW LEVEL SECURITY;
ALTER TABLE poi_tags              ENABLE ROW LEVEL SECURITY;
ALTER TABLE audio_contents        ENABLE ROW LEVEL SECURITY;
ALTER TABLE tourists              ENABLE ROW LEVEL SECURITY;
ALTER TABLE visit_logs            ENABLE ROW LEVEL SECURITY;
ALTER TABLE favorites             ENABLE ROW LEVEL SECURITY;
ALTER TABLE ratings               ENABLE ROW LEVEL SECURITY;
ALTER TABLE analytics_events      ENABLE ROW LEVEL SECURITY;
ALTER TABLE vendors               ENABLE ROW LEVEL SECURITY;

-- ── PUBLIC READ (mobile app không cần auth để xem POI) ─────────
CREATE POLICY "public_read_categories"
  ON categories FOR SELECT USING (TRUE);

CREATE POLICY "public_read_tags"
  ON tags FOR SELECT USING (TRUE);

CREATE POLICY "public_read_poi"
  ON points_of_interest FOR SELECT
  USING (is_active = TRUE AND is_deleted = FALSE);

CREATE POLICY "public_read_poi_tags"
  ON poi_tags FOR SELECT USING (TRUE);

CREATE POLICY "public_read_audio"
  ON audio_contents FOR SELECT
  USING (is_deleted = FALSE);

CREATE POLICY "public_read_vendors"
  ON vendors FOR SELECT USING (is_active = TRUE);

-- ── ANONYMOUS WRITE (mobile app ghi log, visit, rating) ────────
-- Tourist: insert only (device registers itself)
CREATE POLICY "anon_insert_tourist"
  ON tourists FOR INSERT WITH CHECK (TRUE);

CREATE POLICY "anon_update_tourist"
  ON tourists FOR UPDATE USING (TRUE);

-- Visit logs: insert only
CREATE POLICY "anon_insert_visit"
  ON visit_logs FOR INSERT WITH CHECK (TRUE);

-- Favorites: insert/delete by tourist
CREATE POLICY "anon_manage_favorites"
  ON favorites FOR ALL USING (TRUE);

-- Ratings: insert/update
CREATE POLICY "anon_manage_ratings"
  ON ratings FOR ALL USING (TRUE);

-- Analytics: insert only
CREATE POLICY "anon_insert_analytics"
  ON analytics_events FOR INSERT WITH CHECK (TRUE);

-- ── ADMIN FULL ACCESS (dùng service_role key) ──────────────────
-- service_role bypasses RLS automatically in Supabase
-- Nếu dùng anon key cho admin, thêm policy sau:
--
-- CREATE POLICY "admin_all_poi"
--   ON points_of_interest FOR ALL
--   USING (auth.jwt() ->> 'role' = 'admin');
