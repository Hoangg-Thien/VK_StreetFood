-- Migration: drop Products table
-- Purpose: remove menu item domain linked to Vendors.

DROP TABLE IF EXISTS "Products" CASCADE;