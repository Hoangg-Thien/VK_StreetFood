# Supabase Migration Instructions (Aligned Schema)

Tài liệu này mô tả cách setup schema Supabase theo đúng trạng thái hiện tại của code (PascalCase tables + EF-compatible metadata).

## Thứ tự chạy migration

1. `supabase/migrations/001_create_schema.sql`
2. `supabase/migrations/002_create_rls.sql`
3. `supabase/migrations/003_seed_pois.sql` (tuỳ chọn cho môi trường mới)

## Lưu ý quan trọng

- Bộ script này đã được căn chỉnh theo schema thực tế bạn trích xuất từ Supabase:
  - Table names: `Analytics`, `VisitLogs`, `PointsOfInterest`, ...
  - Không dùng bộ snake_case cũ (`points_of_interest`, `visit_logs`, ...).
- `002_create_rls.sql` đặt RLS về trạng thái `DISABLE` cho app tables (phù hợp hiện trạng `rowsecurity=false`).
- `003_seed_pois.sql` là idempotent, có thể chạy lại an toàn.

## Verify sau khi chạy

```sql
-- 1) Danh sach app tables
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN (
    'Analytics','AudioContents','Categories','Favorites','OpeningHours',
    'PoiContentChangeRequests','PoiOwnerRegistrations','PointOfInterestTag',
    'PointsOfInterest','Products','Ratings','Tags','TourPointsOfInterest',
    'Tourists','Tours','Users','Vendors','VisitLogs','__EFMigrationsHistory'
  )
ORDER BY table_name;

-- 2) RLS status (kỳ vọng: false)
SELECT tablename, rowsecurity
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN (
    'Analytics','AudioContents','Categories','Favorites','OpeningHours',
    'PoiContentChangeRequests','PoiOwnerRegistrations','PointOfInterestTag',
    'PointsOfInterest','Products','Ratings','Tags','TourPointsOfInterest',
    'Tourists','Tours','Users','Vendors','VisitLogs','__EFMigrationsHistory'
  )
ORDER BY tablename;

-- 3) Sanity check dữ liệu seed
SELECT COUNT(*) AS poi_count FROM "PointsOfInterest";
SELECT COUNT(*) AS category_count FROM "Categories";
SELECT COUNT(*) AS tag_count FROM "Tags";
```

## Nếu đã có database cũ

- Không cần drop database.
- Chạy lại `001_create_schema.sql` để bổ sung/tạo mới các đối tượng còn thiếu.
- Chạy `002_create_rls.sql` để đồng bộ trạng thái RLS.
- Chỉ chạy `003_seed_pois.sql` khi cần dữ liệu nền (hoặc môi trường rỗng).
