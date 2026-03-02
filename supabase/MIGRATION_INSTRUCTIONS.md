# Supabase Migration Instructions – VK Street Food

## Yêu cầu

- Tài khoản Supabase tại https://supabase.com
- Project đã tạo sẵn (lấy URL + anon key vào `appsettings.json`)

---

## Bước 1 – Mở SQL Editor

1. Vào **Supabase Dashboard → SQL Editor**
2. Click **New query** cho mỗi migration

---

## Bước 2 – Chạy migrations theo thứ tự

### 001 – Tạo schema (tables, indexes, triggers)

```
supabase/migrations/001_create_schema.sql
```

→ Copy nội dung → Paste → **Run**  
→ Kết quả: "Success. No rows returned"

### 002 – Row Level Security policies

```
supabase/migrations/002_create_rls.sql
```

→ Copy nội dung → Paste → **Run**

### 003 – Seed dữ liệu 12 POIs

```
supabase/migrations/003_seed_pois.sql
```

→ Copy nội dung → Paste → **Run**  
→ Kết quả: "12 rows inserted" (cho mỗi bảng)

---

## Bước 3 – Verify

```sql
-- Kiểm tra tables đã tạo
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;

-- Kiểm tra POIs
SELECT id, name, latitude, longitude, average_rating
FROM points_of_interest
ORDER BY id;

-- Kiểm tra audio contents
SELECT p.name, a.language_code, a.duration_in_seconds
FROM audio_contents a
JOIN points_of_interest p ON p.id = a.point_of_interest_id
ORDER BY p.id, a.language_code;

-- Kiểm tra RLS enabled
SELECT tablename, rowsecurity
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY tablename;
```

---

## Bước 4 – Cấu hình connection string

Trong `src/Server/VK.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<your-password>;SSL Mode=Require"
  }
}
```

Trong `src/Server/VK.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<your-password>;SSL Mode=Require"
  }
}
```

---

## Lưu ý

| Thứ tự | File                    | Mô tả                                |
| ------ | ----------------------- | ------------------------------------ |
| 1      | `001_create_schema.sql` | Tạo tất cả tables, indexes, triggers |
| 2      | `002_create_rls.sql`    | Bật RLS + policies cho mobile app    |
| 3      | `003_seed_pois.sql`     | 12 POIs thực tế + audio vi/en/ko     |

- **Không chạy lại 003** nếu data đã có (sẽ bị duplicate key error)
- **service_role key** bypass RLS hoàn toàn – chỉ dùng cho server-side
- **anon key** chỉ có read POI, insert visit/favorite/rating
