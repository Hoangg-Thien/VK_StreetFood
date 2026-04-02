# Supabase Migration Instructions (Aligned Schema)

Tài liệu này mô tả cách setup schema Supabase theo đúng trạng thái hiện tại của code (PascalCase tables + EF-compatible metadata).

## Thứ tự chạy migration

1. `supabase/migrations/schema.sql`
2. `supabase/migrations/rls.sql`
3. `supabase/migrations/seed_pois.sql` (tuỳ chọn cho môi trường mới)

## Lưu ý quan trọng
- `rls.sql` đặt RLS về trạng thái `DISABLE` cho app tables (phù hợp hiện trạng `rowsecurity=false`).
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
- Chạy lại `schema.sql` để bổ sung/tạo mới các đối tượng còn thiếu.
- Chạy `rls.sql` để đồng bộ trạng thái RLS.
- Chỉ chạy `seed_pois.sql` khi cần dữ liệu nền (hoặc môi trường rỗng).
