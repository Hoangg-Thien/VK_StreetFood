-- Migration: 004_delta_align_existing_db
-- Purpose:
-- - Delta-only patch for EXISTING databases (safe, idempotent).
-- - Keep existing data, avoid destructive operations.
-- - Align important runtime columns/indexes used by current API/Web code.

BEGIN;

-- 1) Ensure EF metadata table exists so history inserts below are safe.
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- 2) VisitLogs: ensure columns required by UsageHistory + analytics endpoints.
ALTER TABLE IF EXISTS "VisitLogs"
    ADD COLUMN IF NOT EXISTS "VisitorLatitude" double precision NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "VisitorLongitude" double precision NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LanguageUsed" character varying(10) NOT NULL DEFAULT 'vi',
    ADD COLUMN IF NOT EXISTS "AudioPlayed" boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone;

-- Backfill VisitLogs from possible legacy snake_case columns if they still exist.
DO $$
BEGIN
    IF to_regclass('public."VisitLogs"') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'VisitLogs' AND column_name = 'latitude'
        ) THEN
            EXECUTE 'UPDATE public."VisitLogs" SET "VisitorLatitude" = latitude WHERE "VisitorLatitude" = 0 AND latitude IS NOT NULL';
        END IF;

        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'VisitLogs' AND column_name = 'longitude'
        ) THEN
            EXECUTE 'UPDATE public."VisitLogs" SET "VisitorLongitude" = longitude WHERE "VisitorLongitude" = 0 AND longitude IS NOT NULL';
        END IF;

        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'VisitLogs' AND column_name = 'language_code'
        ) THEN
            EXECUTE 'UPDATE public."VisitLogs" SET "LanguageUsed" = language_code WHERE ("LanguageUsed" IS NULL OR "LanguageUsed" = '''' OR "LanguageUsed" = ''vi'') AND language_code IS NOT NULL';
        END IF;

        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'VisitLogs' AND column_name = 'created_at'
        ) THEN
            EXECUTE 'UPDATE public."VisitLogs" SET "CreatedAt" = created_at WHERE "CreatedAt" IS NULL';
        END IF;
    END IF;
END $$;

-- 3) Tourists: ensure fields used by current API model exist.
ALTER TABLE IF EXISTS "Tourists"
    ADD COLUMN IF NOT EXISTS "TotalVisits" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LastLocationUpdate" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone;

-- Backfill Tourists.TotalVisits from possible legacy columns.
DO $$
BEGIN
    IF to_regclass('public."Tourists"') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Tourists' AND column_name = 'VisitCount'
        ) THEN
            EXECUTE 'UPDATE public."Tourists" SET "TotalVisits" = GREATEST("TotalVisits", COALESCE("VisitCount", 0))';
        END IF;

        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Tourists' AND column_name = 'visit_count'
        ) THEN
            EXECUTE 'UPDATE public."Tourists" SET "TotalVisits" = GREATEST("TotalVisits", COALESCE(visit_count, 0))';
        END IF;
    END IF;
END $$;

-- 4) Analytics: ensure timestamp + soft-delete columns expected by API exist.
ALTER TABLE IF EXISTS "Analytics"
    ADD COLUMN IF NOT EXISTS "EventTimestamp" timestamp with time zone NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone;

-- 5) Owner workflow default values used by server-side inserts.
DO $$
BEGIN
    IF to_regclass('public."PoiContentChangeRequests"') IS NOT NULL THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'PoiContentChangeRequests' AND column_name = 'Status') THEN
            EXECUTE 'ALTER TABLE public."PoiContentChangeRequests" ALTER COLUMN "Status" SET DEFAULT ''pending''';
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'PoiContentChangeRequests' AND column_name = 'CreatedAt') THEN
            EXECUTE 'ALTER TABLE public."PoiContentChangeRequests" ALTER COLUMN "CreatedAt" SET DEFAULT NOW()';
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'PoiContentChangeRequests' AND column_name = 'IsDeleted') THEN
            EXECUTE 'ALTER TABLE public."PoiContentChangeRequests" ALTER COLUMN "IsDeleted" SET DEFAULT FALSE';
        END IF;
    END IF;

    IF to_regclass('public."PoiOwnerRegistrations"') IS NOT NULL THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'PoiOwnerRegistrations' AND column_name = 'Status') THEN
            EXECUTE 'ALTER TABLE public."PoiOwnerRegistrations" ALTER COLUMN "Status" SET DEFAULT ''pending''';
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'PoiOwnerRegistrations' AND column_name = 'CreatedAt') THEN
            EXECUTE 'ALTER TABLE public."PoiOwnerRegistrations" ALTER COLUMN "CreatedAt" SET DEFAULT NOW()';
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'PoiOwnerRegistrations' AND column_name = 'IsDeleted') THEN
            EXECUTE 'ALTER TABLE public."PoiOwnerRegistrations" ALTER COLUMN "IsDeleted" SET DEFAULT FALSE';
        END IF;
    END IF;
END $$;

-- 5.1) Tour translations table for multilingual tour content.
CREATE TABLE IF NOT EXISTS "TourTranslations" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "TourId" integer NOT NULL,
    "LanguageCode" character varying(10) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000) NOT NULL DEFAULT '',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeletedAt" timestamp with time zone NULL,
    CONSTRAINT "FK_TourTranslations_Tours_TourId"
        FOREIGN KEY ("TourId") REFERENCES "Tours"("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_TourTranslations_TourId_LanguageCode"
    ON "TourTranslations" ("TourId", "LanguageCode");

INSERT INTO "TourTranslations"
    ("TourId", "LanguageCode", "Name", "Description", "CreatedAt", "IsDeleted")
SELECT t."Id", 'vi', t."Name", t."Description", NOW(), FALSE
FROM "Tours" t
LEFT JOIN "TourTranslations" tr
    ON tr."TourId" = t."Id" AND LOWER(tr."LanguageCode") = 'vi'
WHERE tr."Id" IS NULL;

INSERT INTO "TourTranslations"
    ("TourId", "LanguageCode", "Name", "Description", "CreatedAt", "IsDeleted")
SELECT t."Id", 'en', t."Name", t."Description", NOW(), FALSE
FROM "Tours" t
LEFT JOIN "TourTranslations" tr
    ON tr."TourId" = t."Id" AND LOWER(tr."LanguageCode") = 'en'
WHERE tr."Id" IS NULL;

INSERT INTO "TourTranslations"
    ("TourId", "LanguageCode", "Name", "Description", "CreatedAt", "IsDeleted")
SELECT t."Id", 'ko', t."Name", t."Description", NOW(), FALSE
FROM "Tours" t
LEFT JOIN "TourTranslations" tr
    ON tr."TourId" = t."Id" AND LOWER(tr."LanguageCode") = 'ko'
WHERE tr."Id" IS NULL;

-- 6) Helpful indexes (only when target table exists).
DO $$
BEGIN
    IF to_regclass('public."VisitLogs"') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS "IX_VisitLogs_PointOfInterestId" ON public."VisitLogs" ("PointOfInterestId")';
        EXECUTE 'CREATE INDEX IF NOT EXISTS "IX_VisitLogs_TouristId" ON public."VisitLogs" ("TouristId")';
        EXECUTE 'CREATE INDEX IF NOT EXISTS "IX_VisitLogs_VisitedAt" ON public."VisitLogs" ("VisitedAt")';
    END IF;

    IF to_regclass('public."Analytics"') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS "IX_Analytics_PointOfInterestId_EventTimestamp" ON public."Analytics" ("PointOfInterestId", "EventTimestamp")';
        EXECUTE 'CREATE INDEX IF NOT EXISTS "IX_Analytics_TouristId" ON public."Analytics" ("TouristId")';
    END IF;

    IF to_regclass('public."AudioContents"') IS NOT NULL THEN
        EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS "IX_AudioContents_PointOfInterestId_LanguageCode" ON public."AudioContents" ("PointOfInterestId", "LanguageCode")';
    END IF;

    IF to_regclass('public."Tourists"') IS NOT NULL THEN
        EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tourists_DeviceId" ON public."Tourists" ("DeviceId")';
    END IF;
END $$;

-- 7) Keep EF migration history coherent for SQL-initialized environments.
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260224144649_InitialCreateWithIntIds', '10.0.3')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260224151409_AddTouristTotalVisitsAndVisitLogDuration', '10.0.3')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260305145533_DropQRCodeAndCleanup', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260316134007_AddAudioFileUrlAndDuration', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260325012431_AddTourAndTourPointOfInterest', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260325022649_SyncTourModelAfterWebCrud', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
