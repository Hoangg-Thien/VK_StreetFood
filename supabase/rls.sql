-- Migration: 002_create_rls
-- Description: Align RLS state with current VK StreetFood server architecture.
--
-- Current production pattern:
-- - API/Web server connects directly to Postgres using trusted server-side credentials.
-- - App tables are managed without RLS policies (rowsecurity = false).
--
-- This script is idempotent and safe to run repeatedly.

DO $$
DECLARE
    t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'Analytics',
        'AudioContents',
        'Categories',
        'Favorites',
        'OpeningHours',
        'PoiContentChangeRequests',
        'PoiOwnerRegistrations',
        'PointOfInterestTag',
        'PointsOfInterest',
        'Products',
        'Ratings',
        'Tags',
        'TourPointsOfInterest',
        'Tourists',
        'Tours',
        'Users',
        'Vendors',
        'VisitLogs',
        '__EFMigrationsHistory'
    ]
    LOOP
        EXECUTE format('ALTER TABLE IF EXISTS %I DISABLE ROW LEVEL SECURITY;', t);
    END LOOP;
END $$;


