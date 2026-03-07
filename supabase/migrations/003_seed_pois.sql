-- ============================================
-- SEED DATABASE - VĨNH KHÁNH FOOD STREET
-- ============================================

-- -------------------------
-- Categories
-- -------------------------
INSERT INTO categories (id,name,description,icon_url,display_order) VALUES
(1,'Ốc & Hải sản','Các món ốc và hải sản','🦞',1),
(2,'Lẩu & Nướng','Các món lẩu và nướng','🍲',2),
(3,'Món chính','Các món ăn chính','🍜',3),
(4,'Đặc sản','Đặc sản nổi bật','⭐',4)
ON CONFLICT (id) DO NOTHING;


-- -------------------------
-- Tags
-- -------------------------
INSERT INTO tags (id,name,color_code) VALUES
(1,'Michelin','#DC2626'),
(2,'Phổ biến','#3B82F6'),
(3,'Giá rẻ','#F59E0B'),
(4,'Mở cửa đêm','#8B5CF6'),
(5,'Đặc sản','#EF4444')
ON CONFLICT (id) DO NOTHING;


-- ============================================
-- Points Of Interest
-- (Schema đúng với bảng PointsOfInterest)
-- ============================================

INSERT INTO "PointsOfInterest"
("Id","Name","Description","Latitude","Longitude","Address","ImageUrl","IsActive","CategoryId","AverageRating","TotalRatings","CreatedAt","UpdatedAt","IsDeleted","DeletedAt")
VALUES
(1,'Cổng chào Phố Ẩm thực Vĩnh Khánh',
'Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – thiên đường ẩm thực đêm của Sài Gòn.',
10.7619058983358,106.702227165271,
'Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/entrance.jpg',
true,4,0,0,NOW(),NULL,false,NULL),

(2,'Ốc Vũ',
'Quán ốc lâu năm nổi tiếng với nước chấm sốt me đặc trưng.',
10.7615184310278,106.7027154252,
'37 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/oc-vu.jpg',
true,1,4.5,0,NOW(),NULL,false,NULL),

(3,'Ốc Thảo',
'Quán ốc nổi tiếng với món ốc len xào dừa béo ngậy.',
10.7617951625975,106.702392988972,
'383 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/oc-thao.jpg',
true,1,4.3,0,NOW(),NULL,false,NULL),

(4,'Ốc Sáu Nở',
'Quán ốc vỉa hè đậm chất Sài Gòn với món ốc hương trứng muối.',
10.7610380785009,106.702904448097,
'128 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/oc-sau-no.jpg',
true,1,4.4,0,NOW(),NULL,false,NULL),

(5,'Ốc Oanh',
'Quán ốc nổi tiếng được Michelin Bib Gourmand.',
10.7608486298266,106.703295774422,
'534 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/oc-oanh.jpg',
true,1,4.8,0,NOW(),NULL,false,NULL),

(6,'A Fat Hot Pot',
'Nhà hàng lẩu phong cách Hong Kong nổi tiếng với lẩu collagen.',
10.7608069330753,106.703478752187,
'668 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/a-fat.jpg',
true,2,4.2,0,NOW(),NULL,false,NULL),

(7,'Chilli Lẩu Nướng Tự Chọn',
'Buffet nướng ngoài trời rất được giới trẻ yêu thích.',
10.7607944319756,106.703659068107,
'232/105 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/chilli.jpg',
true,2,4.1,0,NOW(),NULL,false,NULL),

(8,'Alo Quán – Seafood & Beer',
'Quán hải sản hiện đại với không gian chill.',
10.761127163188,106.704754254081,
'333 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/alo-quan.jpg',
true,1,4.3,0,NOW(),NULL,false,NULL),

(9,'Ốc Đào 2',
'Quán ốc nổi tiếng với khách du lịch quốc tế.',
10.7613479651701,106.704967847399,
'232/123 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/oc-dao-2.jpg',
true,1,4.4,0,NOW(),NULL,false,NULL),

(10,'Lãng Quán',
'Quán nhậu mở cửa đến 4 giờ sáng.',
10.7611499881882,106.705384011963,
'531 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/lang-quan.jpg',
true,2,4.2,0,NOW(),NULL,false,NULL),

(11,'Ớt Xiêm Quán',
'Quán nổi tiếng với các món ăn cực cay.',
10.7611852360527,106.705703610392,
'568 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/ot-xiem.jpg',
true,2,4.3,0,NOW(),NULL,false,NULL),

(12,'Bún Cá Châu Đốc Dì Tư',
'Quán bún cá miền Tây nổi tiếng.',
10.761123552507,106.706606909857,
'320/79 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM',
'/images/poi/bun-ca.jpg',
true,3,4.5,0,NOW(),NULL,false,NULL);


-- ============================================
-- AUDIO CONTENTS
-- ============================================

INSERT INTO audio_contents
(point_of_interest_id,language_code,text_content,audio_file_url,duration_in_seconds)
SELECT "Id",'vi',"Description",'/audio/vi/poi_'||"Id"||'.mp3',45
FROM "PointsOfInterest";

INSERT INTO audio_contents
(point_of_interest_id,language_code,text_content,audio_file_url,duration_in_seconds)
SELECT "Id",'en',"Name",'/audio/en/poi_'||"Id"||'.mp3',35
FROM "PointsOfInterest";


-- ============================================
-- TAG RELATION
-- ============================================

INSERT INTO poi_tags (poi_id,tag_id) VALUES
(5,1),
(2,2),
(5,2),
(7,2),
(10,4),
(4,3),
(7,3);