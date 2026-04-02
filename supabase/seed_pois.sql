-- Migration: 003_seed_pois
-- Description: Baseline seed for POI, category, tag and minimal audio rows
-- Notes:
-- - Aligned to current PascalCase schema.
-- - Idempotent for repeated runs.

INSERT INTO "Categories"
	("Id", "Name", "Description", "IconUrl", "DisplayOrder", "IsActive", "CreatedAt", "IsDeleted")
VALUES
	(1, 'Ốc & Hải sản', 'Các món ốc và hải sản', '🦞', 1, TRUE, NOW(), FALSE),
	(2, 'Lẩu & Nướng', 'Các món lẩu và nướng', '🍲', 2, TRUE, NOW(), FALSE),
	(3, 'Món chính', 'Các món ăn chính', '🍜', 3, TRUE, NOW(), FALSE),
	(4, 'Đặc sản', 'Đặc sản nổi bật', '⭐', 4, TRUE, NOW(), FALSE)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Tags"
	("Id", "Name", "ColorCode", "CreatedAt", "IsDeleted")
VALUES
	(1, 'Michelin', '#DC2626', NOW(), FALSE),
	(2, 'Phổ biến', '#3B82F6', NOW(), FALSE),
	(3, 'Giá rẻ', '#F59E0B', NOW(), FALSE),
	(4, 'Mở cửa đêm', '#8B5CF6', NOW(), FALSE),
	(5, 'Đặc sản', '#EF4444', NOW(), FALSE)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "PointsOfInterest"
	("Id", "Name", "Description", "Latitude", "Longitude", "Address", "ImageUrl", "IsActive", "CategoryId", "AverageRating", "TotalRatings", "CreatedAt", "IsDeleted")
VALUES
	(1, 'Cổng chào Phố Ẩm thực Vĩnh Khánh', 'Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – thiên đường ẩm thực đêm của Sài Gòn.', 10.7619058983358, 106.702227165271, 'Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/cong-chao.jpg', TRUE, 4, 0, 0, NOW(), FALSE),
	(2, 'Ốc Vũ', 'Quán ốc lâu năm nổi tiếng với nước chấm sốt me đặc trưng.', 10.7615184310278, 106.7027154252, '37 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/oc-vu.jpg', TRUE, 1, 4.5, 0, NOW(), FALSE),
	(3, 'Ốc Thảo', 'Quán ốc nổi tiếng với món ốc len xào dừa béo ngậy.', 10.7617951625975, 106.702392988972, '383 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/oc-thao.jpg', TRUE, 1, 4.3, 0, NOW(), FALSE),
	(4, 'Ốc Sáu Nở', 'Quán ốc vỉa hè đậm chất Sài Gòn với món ốc hương trứng muối.', 10.7610380785009, 106.702904448097, '128 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/oc-sau-no.jpg', TRUE, 1, 4.4, 0, NOW(), FALSE),
	(5, 'Ốc Oanh', 'Quán ốc nổi tiếng được Michelin Bib Gourmand.', 10.7608486298266, 106.703295774422, '534 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/oc-oanh.jpg', TRUE, 1, 4.8, 0, NOW(), FALSE),
	(6, 'A Fat Hot Pot', 'Nhà hàng lẩu phong cách Hong Kong nổi tiếng với lẩu collagen.', 10.7608069330753, 106.703478752187, '668 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/a-fat.jpg', TRUE, 2, 4.2, 0, NOW(), FALSE),
	(7, 'Chilli Lẩu Nướng Tự Chọn', 'Buffet nướng ngoài trời rất được giới trẻ yêu thích.', 10.7607944319756, 106.703659068107, '232/105 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/chilli.jpg', TRUE, 2, 4.1, 0, NOW(), FALSE),
	(8, 'Alo Quán – Seafood & Beer', 'Quán hải sản hiện đại với không gian chill.', 10.761127163188, 106.704754254081, '333 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/alo-quan.jpg', TRUE, 1, 4.3, 0, NOW(), FALSE),
	(9, 'Ốc Đào 2', 'Quán ốc nổi tiếng với khách du lịch quốc tế.', 10.7613479651701, 106.704967847399, '232/123 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/oc-dao-2.jpg', TRUE, 1, 4.4, 0, NOW(), FALSE),
	(10, 'Lãng Quán', 'Quán nhậu mở cửa đến 4 giờ sáng.', 10.7611499881882, 106.705384011963, '531 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/lang-quan.jpg', TRUE, 2, 4.2, 0, NOW(), FALSE),
	(11, 'Ớt Xiêm Quán', 'Quán nổi tiếng với các món ăn cực cay.', 10.7611852360527, 106.705703610392, '568 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/ot-xiem.jpg', TRUE, 2, 4.3, 0, NOW(), FALSE),
	(12, 'Bún Cá Châu Đốc Dì Tư', 'Quán bún cá miền Tây nổi tiếng.', 10.761123552507, 106.706606909857, '320/79 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM', '/images/poi/bun-ca.jpg', TRUE, 3, 4.5, 0, NOW(), FALSE)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "AudioContents"
	("PointOfInterestId", "LanguageCode", "TextContent", "AudioFileUrl", "DurationSeconds", "IsGenerated", "CreatedAt", "IsDeleted")
SELECT
	p."Id",
	'vi',
	p."Description",
	'/audio/vi/poi_' || p."Id" || '.mp3',
	45,
	FALSE,
	NOW(),
	FALSE
FROM "PointsOfInterest" p
ON CONFLICT ("PointOfInterestId", "LanguageCode") DO NOTHING;

INSERT INTO "AudioContents"
	("PointOfInterestId", "LanguageCode", "TextContent", "AudioFileUrl", "DurationSeconds", "IsGenerated", "CreatedAt", "IsDeleted")
SELECT
	p."Id",
	'en',
	p."Name",
	'/audio/en/poi_' || p."Id" || '.mp3',
	35,
	FALSE,
	NOW(),
	FALSE
FROM "PointsOfInterest" p
ON CONFLICT ("PointOfInterestId", "LanguageCode") DO NOTHING;

INSERT INTO "PointOfInterestTag" ("PointsOfInterestId", "TagsId")
VALUES
	(5, 1),
	(2, 2),
	(5, 2),
	(7, 2),
	(10, 4),
	(4, 3),
	(7, 3)
ON CONFLICT ("PointsOfInterestId", "TagsId") DO NOTHING;

SELECT setval(pg_get_serial_sequence('"Categories"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 0) FROM "Categories"), 1), true);
SELECT setval(pg_get_serial_sequence('"Tags"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 0) FROM "Tags"), 1), true);
SELECT setval(pg_get_serial_sequence('"PointsOfInterest"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 0) FROM "PointsOfInterest"), 1), true);