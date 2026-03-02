-- Seed: 003_seed_pois
-- Description: 12 POIs thực tế của Phố Ẩm thực Vĩnh Khánh, Quận 4, TP.HCM
-- Nguồn: Time Out 2025, Michelin Guide 2024, Google Maps

-- ── Categories ─────────────────────────────────────────────────
INSERT INTO categories (name, description, icon_url, display_order) VALUES
  ('Ốc & Hải sản',  'Các món ốc và hải sản',       '🦞', 1),
  ('Lẩu & Nướng',   'Các món lẩu và nướng',         '🍲', 2),
  ('Món chính',     'Các món ăn chính',              '🍜', 3),
  ('Đặc sản',       'Đặc sản vùng miền',             '⭐', 4)
ON CONFLICT (name) DO NOTHING;

-- ── Tags ───────────────────────────────────────────────────────
INSERT INTO tags (name, color_code) VALUES
  ('Michelin',      '#DC2626'),
  ('Phổ biến',      '#3B82F6'),
  ('Giá rẻ',        '#F59E0B'),
  ('Mở cửa đêm',   '#8B5CF6'),
  ('Đặc sản',       '#EF4444')
ON CONFLICT (name) DO NOTHING;

-- ── POIs: 12 địa điểm ──────────────────────────────────────────
-- 1. Cổng chào
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Cổng chào Phố Ẩm thực Vĩnh Khánh',
  'Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – "thiên đường không ngủ" của Quận 4. Được Time Out vinh danh là một trong những đường phố thú vị nhất thế giới năm 2025.',
  10.761905898335831, 106.70222716527056,
  'Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-001', '/images/poi/entrance.jpg',
  (SELECT id FROM categories WHERE name = 'Đặc sản'), 0
);

-- 2. Ốc Vũ
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Ốc Vũ',
  'Quán ốc hơn một thập kỷ đỏ lửa tại số 37 Vĩnh Khánh. Nổi tiếng với hơn 50 món biến tấu và nước chấm sốt me "thần thánh" – chua thanh, cay nhẹ, quện chặt vào từng con ốc.',
  10.761518431027818, 106.70271542519974,
  '37 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-002', '/images/poi/oc-vu.jpg',
  (SELECT id FROM categories WHERE name = 'Ốc & Hải sản'), 4.5
);

-- 3. Ốc Thảo
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Ốc Thảo',
  'Không gian rộng rãi, thoáng đãng tại số 383 Vĩnh Khánh. Triết lý tôn vinh vị ngọt tự nhiên của nguyên liệu. Ốc len xào dừa được đánh giá là cực phẩm.',
  10.761795162597451, 106.70239298897182,
  '383 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-003', '/images/poi/oc-thao.jpg',
  (SELECT id FROM categories WHERE name = 'Ốc & Hải sản'), 4.3
);

-- 4. Ốc Sáu Nở
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Ốc Sáu Nở',
  'Hiện thân của văn hóa ốc vỉa hè Sài Gòn nguyên bản tại 128 Vĩnh Khánh. Ốc hương sốt trứng muối vàng ươm, béo bùi – chấm bánh mì giòn tan thì không còn gì bằng.',
  10.761038078500885, 106.70290444809687,
  '128 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-004', '/images/poi/oc-sau-no.jpg',
  (SELECT id FROM categories WHERE name = 'Ốc & Hải sản'), 4.4
);

-- 5. Ốc Oanh (Michelin Bib Gourmand 2024)
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Ốc Oanh',
  'Ngôi sao sáng nhất Vĩnh Khánh – được Michelin Guide trao danh hiệu Bib Gourmand 2024. Hơn 20 năm từ gánh hàng rong vươn lên thành thương hiệu quốc tế. Ốc hương xào bơ tỏi là huyền thoại.',
  10.760848629826567, 106.7032957744219,
  '96 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-005', '/images/poi/oc-oanh.jpg',
  (SELECT id FROM categories WHERE name = 'Ốc & Hải sản'), 4.8
);

-- 6. A Fat Hot Pot
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'A Fat Hot Pot',
  'Không gian Hong Kong retro những năm 80-90 với decor điện ảnh TVB, bảng hiệu neon. Nổi tiếng với Lẩu Trường Thọ xanh và Lẩu Collagen – nước dùng thanh ngọt ninh từ xương.',
  10.760806933075282, 106.70347875218654,
  '668 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-006', '/images/poi/a-fat.jpg',
  (SELECT id FROM categories WHERE name = 'Lẩu & Nướng'), 4.2
);

-- 7. Chilli Lẩu Nướng Tự Chọn
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Chilli Lẩu Nướng Tự Chọn',
  'Thiên đường cho giới trẻ với mô hình buffet linh hoạt. Lẩu Hàu Kimchi trứ danh – sự kết hợp táo bạo giữa kim chi Hàn Quốc và hàu sữa Việt Nam.',
  10.760794431975599, 106.7036590681073,
  '232 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-007', '/images/poi/chilli.jpg',
  (SELECT id FROM categories WHERE name = 'Lẩu & Nướng'), 4.1
);

-- 8. Alo Quán – Seafood & Beer
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Alo Quán – Seafood & Beer',
  'Không gian mở thoáng đãng, giao thoa ẩm thực Việt và Thái. Tôm sốt Thái chua cay xé lưỡi, nghêu hấp sả thanh tao. Lý tưởng cho cuộc vui xuyên đêm.',
  10.761127163188009, 106.70475425408135,
  '333 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-008', '/images/poi/alo-quan.jpg',
  (SELECT id FROM categories WHERE name = 'Ốc & Hải sản'), 4.3
);

-- 9. Ốc Đào 2
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Ốc Đào 2',
  'Chi nhánh của thương hiệu Ốc Đào lừng danh. Nghệ thuật chế biến gia vị đỉnh cao. Răng mực xào bơ tỏi giòn sần sật, ốc móng tay xào me chua thanh tinh tế.',
  10.761347965170131, 106.70496784739889,
  'Hẻm 232 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-009', '/images/poi/oc-dao-2.jpg',
  (SELECT id FROM categories WHERE name = 'Ốc & Hải sản'), 4.4
);

-- 10. Lãng Quán
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Lãng Quán',
  'Quy mô khủng với hai mặt bằng đối diện, luôn tấp nập khách. Giò heo muối chiên giòn – da giòn rụm, thịt mềm mọng. Mở xuyên đêm đến 4 giờ sáng.',
  10.761149988188182, 106.70538401196282,
  'Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-010', '/images/poi/lang-quan.jpg',
  (SELECT id FROM categories WHERE name = 'Lẩu & Nướng'), 4.2
);

-- 11. Ớt Xiêm Quán
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Ớt Xiêm Quán',
  'Trải nghiệm vị giác bùng nổ với các món nướng cay nồng. Ếch nướng muối ớt – thịt chắc nịch, da giòn. Chẳng dừng nướng là món mồi được săn đón nhất.',
  10.761185236052697, 106.70570361039157,
  '568 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-011', '/images/poi/ot-xiem.jpg',
  (SELECT id FROM categories WHERE name = 'Lẩu & Nướng'), 4.3
);

-- 12. Bún Cá Châu Đốc Dì Tư
INSERT INTO points_of_interest
  (name, description, latitude, longitude, address, qr_code, image_url, category_id, average_rating)
VALUES (
  'Bún Cá Châu Đốc Dì Tư',
  'Nốt kết thanh bình với hương vị miền Tây. Tô bún cá vàng ươm nghệ, nước dùng thanh ngọt từ cá lóc và ngải bún. Bông điên điển tạo vị nhẫn nhẹ giòn giòn.',
  10.761123552506971, 106.70660690985743,
  '320/79 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
  'VK-012', '/images/poi/bun-ca.jpg',
  (SELECT id FROM categories WHERE name = 'Món chính'), 4.5
);

-- ── Audio contents (vi + en + ko cho mỗi POI) ──────────────────
INSERT INTO audio_contents (point_of_interest_id, language_code, text_content, audio_file_url, duration_in_seconds)
SELECT id, 'vi', description, '/audio/vi/' || qr_code || '.mp3', 45
FROM points_of_interest;

INSERT INTO audio_contents (point_of_interest_id, language_code, text_content, audio_file_url, duration_in_seconds)
SELECT p.id, 'en',
  CASE p.qr_code
    WHEN 'VK-001' THEN 'Welcome to Vinh Khanh Food Street – Saigon''s sleepless paradise. Named one of the world''s coolest streets by Time Out 2025.'
    WHEN 'VK-002' THEN 'Vu Snails has been a beloved institution on this street for over a decade. Over 50 snail dishes and a legendary tamarind dipping sauce.'
    WHEN 'VK-003' THEN 'Thao Snails champions the natural sweetness of fresh ingredients. Their coconut-braised mud creeper snails are considered a masterpiece.'
    WHEN 'VK-004' THEN 'Sau No Snails embodies original Saigon sidewalk culture. The salted egg yolk sweet snails here are legendary.'
    WHEN 'VK-005' THEN 'The brightest star of Vinh Khanh – Oanh Snails earned the Michelin Bib Gourmand in 2024. Their garlic butter sweet snails are unmissable.'
    WHEN 'VK-006' THEN 'A Fat Hot Pot transports you to 1980s Hong Kong with TVB-style decor and neon signs. Their Collagen Hot Pot is rich and nourishing.'
    WHEN 'VK-007' THEN 'Chilli BBQ is paradise for young diners. Their Kimchi Oyster Hotpot boldly combines Korean kimchi with Vietnamese milk oysters.'
    WHEN 'VK-008' THEN 'Alo Quan blends Vietnamese and Thai cuisine in an airy open setting. Perfect for all-night gatherings with great Thai-style shrimp.'
    WHEN 'VK-009' THEN 'Oc Dao 2 brings masterful seasoning to every dish. Stir-fried squid teeth with garlic butter and tamarind razor clams are crowd favourites.'
    WHEN 'VK-010' THEN 'Lang Quan operates two facing outlets and never sleeps – open until 4 AM. Their crispy salted pork knuckle is the signature dish.'
    WHEN 'VK-011' THEN 'Ot Xiem Quan delivers explosive spicy flavours. Salt-chili grilled frog and rare grilled pork neck are the most sought-after dishes.'
    WHEN 'VK-012' THEN 'A peaceful finish with Mekong Delta flavours. The turmeric fish noodle soup with dien dien flowers is the perfect palate cleanser.'
    ELSE description
  END,
  '/audio/en/' || p.qr_code || '.mp3', 35
FROM points_of_interest p;

INSERT INTO audio_contents (point_of_interest_id, language_code, text_content, audio_file_url, duration_in_seconds)
SELECT p.id, 'ko',
  CASE p.qr_code
    WHEN 'VK-001' THEN '빈칸 푸드 스트리트에 오신 것을 환영합니다. 타임아웃이 2025년 세계에서 가장 멋진 거리 중 하나로 선정한 곳입니다.'
    WHEN 'VK-005' THEN '빈칸 거리의 빛나는 별 – 2024년 미슐랭 빕 구르망을 받은 오안 스네일즈. 마늘 버터 볶음 달팽이는 꼭 맛봐야 합니다.'
    ELSE p.name || '에 오신 것을 환영합니다. ' || SUBSTRING(p.description, 1, 80)
  END,
  '/audio/ko/' || p.qr_code || '.mp3', 30
FROM points_of_interest p;

-- ── Tags ───────────────────────────────────────────────────────
INSERT INTO poi_tags (poi_id, tag_id)
SELECT p.id, t.id FROM points_of_interest p, tags t
WHERE p.qr_code = 'VK-005' AND t.name = 'Michelin';

INSERT INTO poi_tags (poi_id, tag_id)
SELECT p.id, t.id FROM points_of_interest p, tags t
WHERE p.qr_code IN ('VK-002','VK-005','VK-007','VK-010') AND t.name = 'Phổ biến';

INSERT INTO poi_tags (poi_id, tag_id)
SELECT p.id, t.id FROM points_of_interest p, tags t
WHERE p.qr_code IN ('VK-004','VK-007') AND t.name = 'Giá rẻ';

INSERT INTO poi_tags (poi_id, tag_id)
SELECT p.id, t.id FROM points_of_interest p, tags t
WHERE p.qr_code = 'VK-010' AND t.name = 'Mở cửa đêm';
