﻿using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Shared.Constants;

namespace VK.Infrastructure.Seeds;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(VKStreetFoodDbContext context)
    {
        // Patch: fix ImageUrl paths that were stored without /images/poi/ prefix
        await PatchImageUrlsAsync(context);

        if (context.PointsOfInterest.Any())
        {
            await EnsurePoiTranslationsAsync(context);
            await EnsureBaselineToursAsync(context);
            await EnsureTourTranslationsAsync(context);
            await EnsureBaselineVendorsAsync(context);
            await EnsureBaselineOwnerUsersAsync(context);
            return; // Database has been seeded
        }

        // Seed Categories
        var categories = new List<Category>
        {
            new Category { Name = "Ốc & Hải sản", Description = "Các món ốc và hải sản", IconUrl = "🦞", DisplayOrder = 1, IsActive = true },
            new Category { Name = "Lẩu & Nướng", Description = "Các món lẩu và nướng", IconUrl = "🍲", DisplayOrder = 2, IsActive = true },
            new Category { Name = "Món chính", Description = "Các món ăn chính", IconUrl = "🍜", DisplayOrder = 3, IsActive = true },
            new Category { Name = "Đặc sản", Description = "Đặc sản nổi bật", IconUrl = "⭐", DisplayOrder = 4, IsActive = true }
        };
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        // Seed Tags
        var tags = new List<Tag>
        {
            new Tag { Name = "Michelin", ColorCode = "#DC2626" },
            new Tag { Name = "Đặc sản", ColorCode = "#EF4444" },
            new Tag { Name = "Phổ biến", ColorCode = "#3B82F6" },
            new Tag { Name = "Giá rẻ", ColorCode = "#F59E0B" },
            new Tag { Name = "Mở cửa đêm", ColorCode = "#8B5CF6" }
        };
        context.Tags.AddRange(tags);
        await context.SaveChangesAsync();

        // Seed Points of Interest - Real data from Vĩnh Khánh Food Street
        var pois = new List<PointOfInterest>
        {
            // 1. Cổng vào
            new PointOfInterest
            {
                Name = "Cổng chào Phố Ẩm thực Vĩnh Khánh",
                Description = "Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – thiên đường ẩm thực đêm của Sài Gòn.",
                Latitude = 10.7619058983358,
                Longitude = 106.702227165271,
                Address = "Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/cong-chao.jpg",
                IsActive = true,
                CategoryId = 4,
                AverageRating = 0,
                TotalRatings = 0
            },

            // 2. Ốc Vũ
            new PointOfInterest
            {
                Name = "Ốc Vũ",
                Description = "Quán ốc lâu năm nổi tiếng với nước chấm sốt me đặc trưng.",
                Latitude = 10.7615184310278,
                Longitude = 106.7027154252,
                Address = "37 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/oc-vu.jpg",
                IsActive = true,
                CategoryId = 1,
                AverageRating = 4.5m,
                TotalRatings = 0
            },

            // 3. Ốc Thảo
            new PointOfInterest
            {
                Name = "Ốc Thảo",
                Description = "Quán ốc nổi tiếng với món ốc len xào dừa béo ngậy.",
                Latitude = 10.7617951625975,
                Longitude = 106.702392988972,
                Address = "383 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/oc-thao.jpg",
                IsActive = true,
                CategoryId = 1,
                AverageRating = 4.3m,
                TotalRatings = 0
            },

            // 4. Ốc Sáu Nở
            new PointOfInterest
            {
                Name = "Ốc Sáu Nở",
                Description = "Quán ốc vỉa hè đậm chất Sài Gòn với món ốc hương trứng muối.",
                Latitude = 10.7610380785009,
                Longitude = 106.702904448097,
                Address = "128 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/oc-sau-no.jpg",
                IsActive = true,
                CategoryId = 1,
                AverageRating = 4.4m,
                TotalRatings = 0
            },

            // 5. Ốc Oanh (Michelin Bib Gourmand)
            new PointOfInterest
            {
                Name = "Ốc Oanh",
                Description = "Quán ốc nổi tiếng được Michelin Bib Gourmand.",
                Latitude = 10.7608486298266,
                Longitude = 106.703295774422,
                Address = "534 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/oc-oanh.jpg",
                IsActive = true,
                CategoryId = 1,
                AverageRating = 4.8m,
                TotalRatings = 0
            },

            // 6. A Fat Hot Pot
            new PointOfInterest
            {
                Name = "A Fat Hot Pot",
                Description = "Nhà hàng lẩu phong cách Hong Kong nổi tiếng với lẩu collagen.",
                Latitude = 10.7608069330753,
                Longitude = 106.703478752187,
                Address = "668 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/a-fat.jpg",
                IsActive = true,
                CategoryId = 2,
                AverageRating = 4.2m,
                TotalRatings = 0
            },

            // 7. Chilli Lẩu Nướng
            new PointOfInterest
            {
                Name = "Chilli Lẩu Nướng Tự Chọn",
                Description = "Buffet nướng ngoài trời rất được giới trẻ yêu thích.",
                Latitude = 10.7607944319756,
                Longitude = 106.703659068107,
                Address = "232/105 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/chilli.jpg",
                IsActive = true,
                CategoryId = 2,
                AverageRating = 4.1m,
                TotalRatings = 0
            },

            // 8. Alo Quán
            new PointOfInterest
            {
                Name = "Alo Quán – Seafood & Beer",
                Description = "Quán hải sản hiện đại với không gian chill.",
                Latitude = 10.761127163188,
                Longitude = 106.704754254081,
                Address = "333 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/alo-quan.jpg",
                IsActive = true,
                CategoryId = 1,
                AverageRating = 4.3m,
                TotalRatings = 0
            },

            // 9. Ốc Đào 2
            new PointOfInterest
            {
                Name = "Ốc Đào 2",
                Description = "Quán ốc nổi tiếng với khách du lịch quốc tế.",
                Latitude = 10.7613479651701,
                Longitude = 106.704967847399,
                Address = "232/123 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/oc-dao-2.jpg",
                IsActive = true,
                CategoryId = 1,
                AverageRating = 4.4m,
                TotalRatings = 0
            },

            // 10. Lãng Quán
            new PointOfInterest
            {
                Name = "Lãng Quán",
                Description = "Quán nhậu mở cửa đến 4 giờ sáng.",
                Latitude = 10.7611499881882,
                Longitude = 106.705384011963,
                Address = "531 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/lang-quan.jpg",
                IsActive = true,
                CategoryId = 2,
                AverageRating = 4.2m,
                TotalRatings = 0
            },

            // 11. Ớt Xiêm Quán
            new PointOfInterest
            {
                Name = "Ớt Xiêm Quán",
                Description = "Quán nổi tiếng với các món ăn cực cay.",
                Latitude = 10.7611852360527,
                Longitude = 106.705703610392,
                Address = "568 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/ot-xiem.jpg",
                IsActive = true,
                CategoryId = 2,
                AverageRating = 4.3m,
                TotalRatings = 0
            },

            // 12. Bún Cá Châu Đốc
            new PointOfInterest
            {
                Name = "Bún Cá Châu Đốc Dì Tư",
                Description = "Quán bún cá miền Tây nổi tiếng.",
                Latitude = 10.761123552507,
                Longitude = 106.706606909857,
                Address = "320/79 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                ImageUrl = "/images/poi/bun-ca.jpg",
                IsActive = true,
                CategoryId = 3,
                AverageRating = 4.5m,
                TotalRatings = 0
            }
        };

        context.PointsOfInterest.AddRange(pois);
        await context.SaveChangesAsync();
        await EnsurePoiTranslationsAsync(context);

        // Link tags to POIs (after IDs are generated)
        pois[4].Tags.Add(tags[0]); // Ốc Oanh - Michelin
        pois[4].Tags.Add(tags[2]); // Ốc Oanh - Phổ biến
        pois[1].Tags.Add(tags[2]); // Ốc Vũ - Phổ biến
        pois[3].Tags.Add(tags[2]); // Ốc Sáu Nở - Phổ biến
        pois[3].Tags.Add(tags[3]); // Ốc Sáu Nở - Giá rẻ
        pois[6].Tags.Add(tags[3]); // Chilli - Giá rẻ
        pois[6].Tags.Add(tags[2]); // Chilli - Phổ biến
        pois[9].Tags.Add(tags[4]); // Lãng Quán - Mở cửa đêm
        pois[11].Tags.Add(tags[1]); // Bún Cá - Đặc sản
        await context.SaveChangesAsync();

        // Seed Audio Contents – specific narration per POI per language
        // POI 1
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[0].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – \"thiên đường không ngủ\" của Quận 4. Ngay khi bước qua cổng chào này, bạn sẽ cảm nhận được nhịp sống hối hả, rực rỡ và đầy mê hoặc của Sài Gòn về đêm. Được tạp chí danh tiếng Time Out vinh danh là một trong những đường phố thú vị nhất thế giới năm 2025, Vĩnh Khánh không chỉ là nơi để ăn, mà là một sân khấu văn hóa sống động. Dưới ánh đèn neon chớp nháy và làn khói bếp thơm lừng mùi bơ tỏi, hành phi, hàng chục quán ốc, lẩu, nướng đang chờ bạn khám phá. Hãy hít một hơi thật sâu, để mùi hương của biển cả và than hoa dẫn lối bạn vào cuộc hành trình vị giác đầy kịch tính này." },
            new AudioContent { PointOfInterestId = pois[0].Id, LanguageCode = LanguageConstants.English,    TextContent = "Welcome to Vinh Khanh Food Street – the \"sleepless paradise\" of District 4. As soon as you step through the entrance, you'll feel the bustling, vibrant, and captivating rhythm of Saigon at night. Honored by the prestigious Time Out magazine as one of the world's most exciting streets in 2025, Vinh Khanh is not just a place to eat, but a lively cultural stage. Under the flashing neon lights and the fragrant smoke of garlic butter and fried onions, dozens of snail, hot pot, and barbecue restaurants await your discovery. Take a deep breath, and let the scent of the sea and charcoal guide you on this dramatic culinary journey." },
            new AudioContent { PointOfInterestId = pois[0].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "4군에 위치한 \"잠 못 이루는 천국\", 빈칸 푸드 스트리트에 오신 것을 환영합니다. 입구에 들어서는 순간, 밤의 사이공이 선사하는 활기차고 매혹적인 분위기를 온몸으로 느낄 수 있습니다. 권위 있는 타임아웃 매거진이 선정한 2025년 세계에서 가장 흥미로운 거리 중 하나인 빈칸은 단순한 먹거리를 넘어 생동감 넘치는 문화의 장입니다. 번쩍이는 네온사인과 마늘 버터, 볶은 양파의 향긋한 연기 아래, 수많은 달팽이 요리, 훠궈, 바비큐 전문점들이 여러분의 미식 여행을 기다립니다. 심호흡을 하고 바다와 숯불 향에 이끌려 이 환상적인 미식 여행을 떠나보세요." }
        });
        // POI 2
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[1].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Ngay tại số 37 Vĩnh Khánh là Ốc Vũ, một cái tên được ví như \"biểu tượng bình dân\" của con phố này với hơn một thập kỷ đỏ lửa. Không hào nhoáng, cầu kỳ, Ốc Vũ chinh phục thực khách bằng sự chân thành trong từng món ăn. Nổi tiếng với nguồn hải sản tươi rói được tuyển chọn mỗi sáng từ chợ đầu mối Bình Điền, quán sở hữu thực đơn khổng lồ với hơn 50 món biến tấu. Điều khiến người ta nhớ mãi về Ốc Vũ chính là thứ nước chấm sốt me \"thần thánh\" – chua thanh, cay nhẹ, quện chặt vào từng con ốc, tạo nên một bản giao hưởng vị giác khó quên. Đây là điểm đến lý tưởng để bạn cảm nhận không khí nhậu đúng chất Sài Gòn: ồn ào, náo nhiệt và cực kỳ sảng khoái." },
            new AudioContent { PointOfInterestId = pois[1].Id, LanguageCode = LanguageConstants.English,    TextContent = "Located at 37 Vinh Khanh Street is Oc Vu, a name considered an \"icon of the street's affordable food scene,\" boasting over a decade of success. Without being flashy or extravagant, Oc Vu wins over diners with the sincerity in every dish. Famous for its fresh seafood, carefully selected every morning from the Binh Dien wholesale market, the restaurant has a huge menu with over 50 variations. What people remember most about Oc Vu is its \"magical\" tamarind dipping sauce – tangy, mildly spicy, and perfectly complementing each snail, creating an unforgettable symphony of flavors. This is the ideal destination to experience the authentic Saigon drinking atmosphere: noisy, lively, and incredibly refreshing." },
            new AudioContent { PointOfInterestId = pois[1].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 거리 37번지에 위치한 옥부는 10년 넘게 사랑받으며 이 거리의 저렴한 음식 문화를 대표하는 곳으로 자리매김했습니다. 화려함이나 사치스러움 없이, 옥부는 모든 요리에 담긴 정성으로 손님들의 마음을 사로잡습니다. 빈디엔 도매시장에서 매일 아침 엄선한 신선한 해산물로 유명한 이곳은 50가지가 넘는 다양한 메뉴를 자랑합니다. 옥부에서 가장 기억에 남는 것은 바로 '마법의' 타마린드 소스입니다. 새콤달콤하면서도 은은하게 매콤한 이 소스는 달팽이 요리와 완벽한 조화를 이루며 잊을 수 없는 풍미의 향연을 선사합니다. 시끌벅적하고 활기 넘치면서도 시원한 사이공의 진정한 술자리 분위기를 경험하기에 더할 나위 없이 좋은 곳입니다." }
        });
        // POI 3
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[2].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Bước đến số 383, bạn sẽ tìm thấy Ốc Thảo – một nốt trầm tinh tế giữa bản nhạc rock sôi động của Vĩnh Khánh. Khác với vẻ xô bồ thường thấy, Ốc Thảo chú trọng vào không gian rộng rãi, thoáng đãng và sạch sẽ, mang đến cảm giác thư thái hơn cho thực khách. Triết lý ẩm thực tại đây là tôn vinh vị ngọt tự nhiên của nguyên liệu. Món \"Ốc len xào dừa\" ở đây được đánh giá là cực phẩm với phần nước cốt dừa béo ngậy, đậm đà nhưng không hề gây ngán, hay món \"Sò điệp nướng mỡ hành\" với từng thớ thịt sò trắng ngần, mọng nước, thơm lừng mùi hành phi giòn rụm. Quán thậm chí mở cả ngày 24/7, hiếm có trên phố ốc." },
            new AudioContent { PointOfInterestId = pois[2].Id, LanguageCode = LanguageConstants.English,    TextContent = "Stepping into the restaurant at 383, you'll find Oc Thao – a subtle, tranquil note amidst the vibrant rock music of Vinh Khanh. Unlike the usual bustling atmosphere, Oc Thao prioritizes a spacious, airy, and clean environment, offering diners a more relaxing experience. Their culinary philosophy is to celebrate the natural sweetness of the ingredients. Their \"Stir-fried Snails with Coconut Milk\" is considered a delicacy, with its rich, creamy coconut sauce that's flavorful without being overwhelming, and their \"Grilled Scallops with Onion Oil\" features succulent, white scallop meat, fragrant with crispy fried onions. The restaurant is even open 24/7, a rare find on this street known for its snail dishes." },
            new AudioContent { PointOfInterestId = pois[2].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 거리 383번지에 위치한 레스토랑 '옥 타오(Oc Thao)'에 들어서면, 활기 넘치는 록 음악이 울려 퍼지는 거리 한가운데 은은하고 평온한 분위기가 감돕니다. 북적거리는 분위기와는 달리, 옥 타오는 넓고 쾌적하며 깨끗한 환경을 조성하여 손님들에게 편안한 식사를 선사합니다. 식재료 본연의 단맛을 살리는 것이 옥 타오의 요리 철학입니다. 특히 '코코넛 밀크 달팽이 볶음'은 풍부하면서도 과하지 않은 코코넛 소스가 일품이며, '양파 기름에 구운 가리비'는 쫄깃한 흰 가리비 살과 바삭하게 튀긴 양파의 향긋함이 어우러져 일품입니다. 달팽이 요리로 유명한 이 거리에서 보기 드문 24시간 영업이라는 점도 매력적입니다." }
        });
        // POI 4
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[3].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Tọa lạc tại số 128 Vĩnh Khánh, Ốc Sáu Nở là hiện thân của văn hóa ốc vỉa hè Sài Gòn nguyên bản. Những chiếc bàn nhựa thấp, tiếng cười nói rôm rả, tiếng vỏ ốc lách cách tạo nên một bầu không khí không thể trộn lẫn. Sáu Nở nổi tiếng với các món ốc được tẩm ướp đậm đà, \"bắt mồi\" cực đỉnh. Đặc biệt, món Ốc hương sốt trứng muối ở đây là một huyền thoại: sốt trứng muối vàng ươm, béo bùi, mặn ngọt hài hòa, chấm kèm một mẩu bánh mì giòn tan thì không còn gì bằng. Thực khách quốc tế cũng ưa thích nơi đây vì có menu song ngữ và nhân viên thân thiện." },
            new AudioContent { PointOfInterestId = pois[3].Id, LanguageCode = LanguageConstants.English,    TextContent = "Located at 128 Vinh Khanh Street, Oc Sau No embodies the authentic Saigon street food culture. Low plastic tables, lively conversations, and the clinking of snail shells create an unmistakable atmosphere. Sau No is famous for its richly marinated snail dishes that are incredibly appetizing. In particular, their salted egg yolk sauce snails are legendary: the golden, rich, and perfectly balanced sweet and salty sauce, served with a crispy piece of bread, is simply irresistible. International diners also love this place for its bilingual menu and friendly staff." },
            new AudioContent { PointOfInterestId = pois[3].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 거리 128번지에 위치한 옥사우노는 진정한 사이공 길거리 음식 문화를 느낄 수 있는 곳입니다. 낮은 플라스틱 테이블, 활기찬 대화, 달팽이 껍데기가 부딪히는 소리가 어우러져 독특한 분위기를 자아냅니다. 사우노는 풍부한 양념에 재운 달팽이 요리로 유명하며, 특히 소금에 절인 계란 노른자 소스를 곁들인 달팽이 요리는 일품입니다. 황금빛을 띠는 진하고 달콤짭짤한 소스는 바삭한 빵과 함께 제공되어 그 맛을 잊을 수 없게 합니다. 또한, 다국어 메뉴와 친절한 직원 덕분에 외국인 손님들에게도 인기가 많습니다." }
        });
        // POI 5
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[4].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Đây chính là \"ngôi sao sáng nhất\" của bầu trời Vĩnh Khánh – Ốc Oanh, quán ốc vinh dự được Michelin Guide trao tặng danh hiệu Bib Gourmand năm 2024. Với lịch sử hơn 20 năm, từ một gánh hàng rong nhỏ bé vươn lên thành thương hiệu quốc tế, Ốc Oanh là minh chứng cho sức hấp dẫn của ẩm thực Việt. Không gian cực kỳ rộng, có thể phục vụ hàng trăm thực khách cùng lúc, hải sản luôn được nhập tươi liên tục trong ngày. Món ăn làm nên tên tuổi của quán là \"Ốc hương xào bơ tỏi\": những con ốc to, chắc thịt ngập trong sốt bơ tỏi vàng óng, thơm nức mũi, chấm cùng bánh mì là sự kết hợp hoàn hảo." },
            new AudioContent { PointOfInterestId = pois[4].Id, LanguageCode = LanguageConstants.English,    TextContent = "This is the \"brightest star\" in Vinh Khanh's culinary scene – Oanh Snail Restaurant, proudly awarded the Michelin Guide's Bib Gourmand title in 2024. With a history spanning over 20 years, from a small street vendor to an international brand, Oanh Snail Restaurant is a testament to the allure of Vietnamese cuisine. The spacious setting can accommodate hundreds of diners simultaneously, and the seafood is continuously replenished with fresh ingredients throughout the day. The signature dish is \"Stir-fried fragrant snails with butter and garlic\": large, meaty snails immersed in a golden, fragrant butter and garlic sauce, a perfect combination when dipped with bread." },
            new AudioContent { PointOfInterestId = pois[4].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 지역 미식계의 \"가장 빛나는 별\"이라 불리는 오안 달팽이 레스토랑은 2024년 미슐랭 가이드 빕 구르망에 선정되는 영예를 안았습니다. 작은 노점상에서 시작해 20년 넘게 세계적인 브랜드로 성장한 오안 달팽이 레스토랑은 베트남 요리의 매력을 증명하는 곳입니다. 넓은 공간은 수백 명의 손님을 동시에 수용할 수 있으며, 신선한 해산물이 하루 종일 끊임없이 제공됩니다. 대표 메뉴는 \"버터 마늘 향 달팽이 볶음\"으로, 큼직하고 살이 통통한 달팽이가 황금빛 버터 마늘 소스에 푹 담겨 나와 빵에 찍어 먹으면 완벽한 조화를 이룹니다." }
        });
        // POI 6
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[5].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Tại số 668 Vĩnh Khánh, A Fat Hot Pot mở ra một cánh cửa thời gian, đưa bạn lạc vào không gian Hong Kong những năm 80-90 đầy hoài niệm. Với decor mang đậm chất điện ảnh TVB, những bảng hiệu neon rực rỡ và nhạc Hoa xưa cũ, quán tạo nên một trải nghiệm thị giác thú vị. \"Ngôi sao\" thực sự nằm ở nồi lẩu. A Fat nổi tiếng với Lẩu Trường Thọ xanh độc đáo và Lẩu Collagen trứ danh – nước dùng thanh ngọt không bột ngọt, bổ dưỡng, được ninh kỹ từ xương. Mô hình lẩu tự chọn với quầy sốt pha chế theo sở thích giúp bạn làm chủ bữa tiệc vị giác của mình. Thực khách đặc biệt ưa thích thịt bò Wagyu giá phải chăng tại đây." },
            new AudioContent { PointOfInterestId = pois[5].Id, LanguageCode = LanguageConstants.English,    TextContent = "Located at 668 Vinh Khanh Street, A Fat Hot Pot opens a door to time, transporting you to the nostalgic atmosphere of Hong Kong in the 80s and 90s. With its TVB-inspired decor, vibrant neon signs, and old-school Chinese music, the restaurant creates a delightful visual experience. The real \"star\" lies in the hot pot. A Fat is famous for its unique Green Longevity Hot Pot and its renowned Collagen Hot Pot – a sweet, nutritious broth simmered from bones, free of MSG. The self-service hot pot model with a sauce bar allows you to take control of your culinary feast. Diners particularly appreciate the reasonably priced Wagyu beef here." },
            new AudioContent { PointOfInterestId = pois[5].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 거리 668번지에 위치한 A Fat Hot Pot은 마치 시간 여행을 떠난 듯한 느낌을 선사하며, 80년대와 90년대 홍콩의 향수를 불러일으킵니다. TVB 방송국을 연상시키는 인테리어, 화려한 네온사인, 그리고 정겨운 옛 중국 음악이 어우러져 시각적으로도 즐거운 경험을 선사합니다. 하지만 진정한 주인공은 바로 훠궈입니다. A Fat은 독특한 녹장 훠궈와 뼈를 푹 끓여 만든 달콤하고 영양 가득한 콜라겐 훠궈로 유명하며, MSG는 전혀 첨가하지 않았습니다. 셀프 서비스 방식의 소스 바를 통해 원하는 소스를 직접 골라 먹을 수 있어 더욱 풍성한 식사를 즐길 수 있습니다. 특히 합리적인 가격의 와규는 많은 사람들에게 사랑받는 메뉴입니다." }
        });
        // POI 7
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[6].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Chilli tại 232 Vĩnh Khánh là thiên đường dành cho giới trẻ và những tín đồ mê đồ nướng tự chọn. Quán mang đến sự phóng khoáng, tự do với mô hình BBQ ngoài trời ngay trên vỉa hè – bạn tự tay nướng thịt trên bếp than ngay bàn ăn, tạo nên trải nghiệm cực kỳ sôi động. Thực đơn là một cuộc diễu hành của protein: từ bò Mỹ cuộn nấm, ba chỉ heo sốt BBQ đến các loại hải sản tươi rói. Đừng bỏ qua món \"Lẩu Hàu Kimchi\" trứ danh – sự kết hợp táo bạo giữa vị chua cay của kim chi Hàn Quốc và vị ngọt béo mọng nước của hàu sữa Việt Nam." },
            new AudioContent { PointOfInterestId = pois[6].Id, LanguageCode = LanguageConstants.English,    TextContent = "Chilli at 232 Vinh Khanh is a paradise for young people and barbecue enthusiasts. The restaurant offers a relaxed and free atmosphere with its outdoor BBQ on the sidewalk – you grill your own meat on a charcoal grill right at your table, creating an incredibly lively experience. The menu is a parade of protein: from American beef rolls with mushrooms and BBQ pork belly to a variety of fresh seafood. Don't miss the famous \"Kimchi Oyster Hot Pot\" – a bold combination of the sour and spicy flavor of Korean kimchi and the sweet, juicy taste of Vietnamese oysters." },
            new AudioContent { PointOfInterestId = pois[6].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 232번지에 위치한 칠리(Chilli)는 젊은이들과 바비큐 애호가들에게 천국과 같은 곳입니다. 레스토랑은 야외 바비큐 시설을 갖추고 있어 편안하고 자유로운 분위기를 자랑합니다. 테이블 바로 앞에서 숯불 그릴에 직접 고기를 구워 먹는 생동감 넘치는 경험을 즐길 수 있습니다. 메뉴는 버섯을 곁들인 아메리칸 비프 롤과 바비큐 삼겹살부터 신선한 해산물까지 다채로운 단백질 요리로 가득합니다. 특히 한국 김치의 새콤달콤한 맛과 베트남 굴의 달콤하고 즙이 많은 식감이 어우러진 시그니처 메뉴인 \"김치 굴 전골\"은 절대 놓치지 마세요." }
        });
        // POI 8
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[7].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Tọa lạc tại 333 Vĩnh Khánh, Alo Quán mang đến một làn gió hiện đại và \"chill\" hơn cho con phố. Không gian mở thoáng đãng, thiết kế trẻ trung với bảng neon bắt mắt, nơi đây phù hợp cho những cuộc vui xuyên đêm. Thực đơn của Alo Quán đa dạng đến bất ngờ: từ ốc ếch cho đến ốc, ếch, cá, tôm đủ kiểu chế biến. Nhân viên nói tiếng Anh tốt, thân thiện – điểm cộng lớn cho khách quốc tế. Lưu ý: bảng Google Maps có thể chưa chính xác hoàn toàn, cứ tìm biển neon Alo Quán là thấy." },
            new AudioContent { PointOfInterestId = pois[7].Id, LanguageCode = LanguageConstants.English,    TextContent = "Located at 333 Vinh Khanh Street, Alo Quan brings a modern and \"chill\" vibe to the street. With its open, airy space and youthful design featuring eye-catching neon signs, it's perfect for late-night parties. Alo Quan's menu is surprisingly diverse: from snails and frogs to various other snails, frogs, fish, and shrimp prepared in different ways. The staff speak good English and are friendly – a big plus for international customers. Note: Google Maps directions may not be entirely accurate; just search for the Alo Quan neon sign." },
            new AudioContent { PointOfInterestId = pois[7].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 거리 333번지에 위치한 알로 콴(Alo Quan)은 거리에 모던하고 편안한 분위기를 선사합니다. 탁 트인 공간과 눈길을 사로잡는 네온사인으로 꾸며진 젊고 감각적인 디자인은 늦은 밤 파티를 즐기기에 제격입니다. 알로 콴의 메뉴는 달팽이와 개구리를 비롯해 다양한 생선과 새우를 여러 가지 방식으로 조리한 요리들로 구성되어 있어 놀라울 정도로 다채롭습니다. 영어를 유창하게 구사하는 친절한 직원들은 외국인 손님들에게 큰 장점입니다. 참고: 구글 지도 길찾기 정보가 정확하지 않을 수 있으니, 알로 콴 네온사인을 검색해 보세요." }
        });
        // POI 9
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[8].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Ốc Đào 2 là một trong những điểm đến được khách quốc tế yêu thích nhất trên phố Vĩnh Khánh, với gần 800 đánh giá trên Google. Điểm mạnh của quán: menu có ảnh minh họa to rõ ràng, dễ gọi dù không biết tiếng Việt. Nhân viên nhiệt tình hướng dẫn, không ngại bóc ốc giúp khách. Thực đơn phong phú từ ốc đến mì xào hải sản – đặc biệt món \"Mì xào ốc dao\" bùi béo, đậm đà được nhiều thực khách gọi là không thể thiếu để \"lót bụng\" sau một buổi ốc. Đây cũng là quán hoạt động đến tận 1 giờ sáng, phù hợp với lịch trình khám phá về đêm." },
            new AudioContent { PointOfInterestId = pois[8].Id, LanguageCode = LanguageConstants.English,    TextContent = "Oc Dao 2 is one of the most popular destinations for international tourists on Vinh Khanh Street, with nearly 800 reviews on Google. The restaurant's strengths include: a menu with large, clear photos, making it easy to order even without knowing Vietnamese; enthusiastic staff who are willing to help peel snails; a diverse menu ranging from snails to seafood stir-fried noodles – especially the rich and flavorful \"Stir-fried Noodles with Snails\" which many customers consider a must-try after a snail meal. It also stays open until 1 AM, making it ideal for nighttime exploration." },
            new AudioContent { PointOfInterestId = pois[8].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "Oc Dao 2는 Vinh Khanh 거리에서 외국인 관광객들에게 가장 인기 있는 곳 중 하나로, 구글에 800개에 가까운 리뷰가 있습니다. 이 식당의 장점은 다음과 같습니다. 크고 선명한 사진이 있는 메뉴판 덕분에 베트남어를 몰라도 쉽게 주문할 수 있습니다. 달팽이 껍질을 벗겨주는 데 도움을 주는 친절한 직원. 달팽이 요리부터 해산물 볶음면까지 다양한 메뉴. 특히 진하고 풍미 가득한 \"달팽이 볶음면\"은 많은 손님들이 달팽이 요리 후 꼭 먹어봐야 할 메뉴로 꼽습니다. 또한 새벽 1시까지 영업하기 때문에 밤에도 방문하기 좋습니다." }
        });
        // POI 10
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[9].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Lãng Restaurant tại 531 Vĩnh Khánh là một trong những địa điểm mở cửa muộn nhất phố – đến tận 4 giờ sáng – trở thành điểm đến yêu thích của những \"cú đêm\" Sài Gòn. Khác với hình dung về quán nhậu bình thường, Lãng gây bất ngờ với thực đơn phong phú và sáng tạo, đặc biệt là món gà: \"Gà nướng cơm trúc\" giòn rụm với cơm trúc thơm lừng được khách khen nức nở. Quán còn phục vụ nhạc sống vào một số buổi tối, tạo không khí vô cùng sảng khoái. Đây là nơi để vừa ăn ngon, vừa nhâm nhi bia lạnh và ngắm nhìn phố đêm tấp nập." },
            new AudioContent { PointOfInterestId = pois[9].Id, LanguageCode = LanguageConstants.English,    TextContent = "Lãng Restaurant at 531 Vĩnh Khánh Street is one of the latest-opening spots on the street – until 4 AM – making it a favorite destination for Saigon's night owls. Unlike typical pubs, Lãng surprises with its diverse and creative menu, especially its chicken dish: the crispy grilled chicken with fragrant bamboo rice is highly praised by customers. The restaurant also features live music on some evenings, creating a very relaxing atmosphere. It's the perfect place to enjoy delicious food, sip on cold beer, and watch the bustling night street." },
            new AudioContent { PointOfInterestId = pois[9].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 거리 531번지에 위치한 랑 레스토랑은 새벽 4시까지 영업하는 곳으로, 밤늦게까지 즐기는 사이공 사람들에게 인기 만점입니다. 일반적인 술집과는 달리, 랑 레스토랑은 다채롭고 창의적인 메뉴로 놀라움을 선사하는데, 특히 바삭하게 구운 닭고기와 향긋한 죽순밥이 손님들에게 큰 인기를 얻고 있습니다. 또한, 저녁에는 라이브 음악 공연도 있어 편안한 분위기를 즐길 수 있습니다. 맛있는 음식을 즐기고 시원한 맥주를 마시며 활기 넘치는 밤거리를 구경하기에 더할 나위 없이 좋은 곳입니다." }
        });
        // POI 11
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[10].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Ớt Xiêm Quán tại số 568 Vĩnh Khánh nổi tiếng với các món ăn cay nồng đích thực, đúng như cái tên của mình. Đây là thiên đường dành cho những tín đồ của vị cay, với menu đa dạng từ ốc, hải sản đến các món nướng và xào, tất cả đều có thể được tùy chỉnh độ cay theo sở thích – kể cả level \"ớt tử thần\" thử thách dành cho người dũng cảm. Không gian quán nhộn nhịp, đầy năng lượng. Món \"Ốc hương xào ớt hiểm\" và \"Tôm sú nướng ớt\" là những lựa chọn không thể bỏ qua." },
            new AudioContent { PointOfInterestId = pois[10].Id, LanguageCode = LanguageConstants.English,    TextContent = "At 568 Vinh Khanh Street, Ot Xiem Quan is famous for its authentically spicy dishes, living up to its name. It's a paradise for spicy food lovers, with a diverse menu ranging from snails and seafood to grilled and stir-fried dishes, all customizable to your liking – including a challenging \"death chili\" level for the brave. The restaurant has a lively and energetic atmosphere. The \"Stir-fried fragrant snails with chili\" and \"Grilled prawns with chili\" are must-try dishes." },
            new AudioContent { PointOfInterestId = pois[10].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "빈칸 거리 568번지에 위치한 오트 시엠 콴(Ot Xiem Quan)은 이름에 걸맞게 정통 매운 요리로 유명합니다. 매운 음식을 좋아하는 사람들에게는 천국과도 같은 곳으로, 달팽이와 해산물부터 구이와 볶음 요리까지 다양한 메뉴를 제공하며, 모든 요리는 취향에 따라 조절할 수 있습니다. 특히 용감한 사람들을 위한 '죽음의 고추' 단계도 준비되어 있습니다. 활기차고 생동감 넘치는 분위기의 이 식당에서 꼭 맛보아야 할 메뉴는 '향긋한 고추 달팽이 볶음'과 '고추 새우 구이'입니다." }
        });
        // POI 12
        context.AudioContents.AddRange(new[]
        {
            new AudioContent { PointOfInterestId = pois[11].Id, LanguageCode = LanguageConstants.Vietnamese, TextContent = "Bún Cá Châu Đốc Dì Tư tại số 75 Vĩnh Khánh là điểm dừng chân hoàn hảo để bắt đầu hoặc kết thúc hành trình khám phá phố đêm. Tô bún cá với đầu cá lóc to, nhiều thịt, nước dùng ngọt thanh từ cá tươi nấu cùng sả, nghệ và gia vị đặc trưng miền Tây Nam Bộ. Quán mở từ 6 giờ sáng – lý tưởng cho bữa sáng sau đêm vui chơi thâu đêm, hoặc ghé vào buổi chiều trước khi phố ốc bắt đầu nhộn nhịp. Giá cả siêu bình dân, chỉ 70.000đ cho một tô đầy ắp." },
            new AudioContent { PointOfInterestId = pois[11].Id, LanguageCode = LanguageConstants.English,    TextContent = "Aunt Tư's Fish Noodle Soup at 75 Vinh Khanh Street is the perfect stop to start or end your nighttime exploration. The bowl of fish noodle soup features a large, meaty snakehead fish head, and a sweet, savory broth made from fresh fish cooked with lemongrass, turmeric, and characteristic spices of the Southwestern region of Vietnam. The restaurant opens at 6 AM – ideal for breakfast after a night out, or a visit in the afternoon before the seafood street gets busy. The price is incredibly affordable, only 70,000 VND for a generous bowl." },
            new AudioContent { PointOfInterestId = pois[11].Id, LanguageCode = LanguageConstants.Korean,     TextContent = "차우독의 밤거리를 탐험하다 시작하거나 마무리하기에 완벽한 곳은 빈칸 거리 75번지에 있는 아주머니의 생선 국수집입니다. 이곳의 생선 국수에는 살이 통통한 큰 가물치 머리와 신선한 생선을 레몬그라스, 강황, 그리고 베트남 남서부 지역 특유의 향신료로 끓여낸 달콤하고 감칠맛 나는 국물이 들어 있습니다. 이 식당은 오전 6시에 문을 열기 때문에 밤늦게까지 놀다가 아침 식사를 하거나, 해산물 거리가 붐비기 전 오후에 방문하기에 좋습니다. 가격도 매우 저렴해서 푸짐한 국수 한 그릇에 단돈 7만 동입니다." }
        });
        await context.SaveChangesAsync();

        // Seed Vendors
        var vendors = new List<Vendor>
        {
            new Vendor
            {
                Name = "Ốc Vũ",
                Description = "Quán ốc nổi tiếng với nước sốt me thần thánh",
                ContactPerson = "Anh Vũ",
                PhoneNumber = "0909123456",
                Email = "ocvu@gmail.com",
                PointOfInterestId = 2,
                ImageUrl = "/images/vendor/oc-vu.jpg",
                IsActive = true,
                AverageRating = 4.5m,
                TotalReviews = 120
            },
            new Vendor
            {
                Name = "Ốc Oanh",
                Description = "Vinh dự Michelin Bib Gourmand 2024",
                ContactPerson = "Chị Oanh",
                PhoneNumber = "0918234567",
                Email = "ocoanh@gmail.com",
                PointOfInterestId = 5,
                ImageUrl = "/images/vendor/oc-oanh.jpg",
                IsActive = true,
                AverageRating = 4.8m,
                TotalReviews = 450
            },
            new Vendor
            {
                Name = "A Fat Hot Pot",
                Description = "Lẩu phong cách Hong Kong retro",
                ContactPerson = "Manager",
                PhoneNumber = "0927345678",
                Email = "afathotpot@gmail.com",
                PointOfInterestId = 6,
                ImageUrl = "/images/vendor/a-fat.jpg",
                IsActive = true,
                AverageRating = 4.2m,
                TotalReviews = 89
            }
        };

        context.Vendors.AddRange(vendors);
        await context.SaveChangesAsync();

        // Seed Opening Hours
        foreach (var vendor in vendors)
        {
            for (int day = 0; day <= 6; day++)
            {
                context.OpeningHours.Add(new OpeningHours
                {
                    VendorId = vendor.Id,
                    DayOfWeek = day,
                    OpenTime = new TimeSpan(15, 0, 0),
                    CloseTime = new TimeSpan(23, 0, 0),
                    IsClosed = false
                });
            }
        }
        await context.SaveChangesAsync();

        await EnsureBaselineToursAsync(context);
        await EnsureTourTranslationsAsync(context);
        await EnsureBaselineVendorsAsync(context);
        await EnsureBaselineOwnerUsersAsync(context);
    }

    /// <summary>
    /// One-time patch: POIs seeded before had only filenames (e.g. "oc-vu.jpg") instead of
    /// full relative paths ("/images/poi/oc-vu.jpg"). Fix any rows that are missing the prefix.
    /// </summary>
    private static async Task PatchImageUrlsAsync(VKStreetFoodDbContext context)
    {
        var poisToFix = await context.PointsOfInterest
            .Where(p => p.ImageUrl != null && !p.ImageUrl.StartsWith("/") && !p.ImageUrl.StartsWith("http"))
            .ToListAsync();

        foreach (var poi in poisToFix)
            poi.ImageUrl = "/images/poi/" + poi.ImageUrl;

        if (poisToFix.Count > 0)
            await context.SaveChangesAsync();
    }

    /// <summary>
    /// Backfill default translation rows for existing POIs.
    /// Vietnamese row is canonical source; missing en/ko rows are created from vi/base text.
    /// </summary>
    private static async Task EnsurePoiTranslationsAsync(VKStreetFoodDbContext context)
    {
        var existingTranslations = await context.PointOfInterestTranslations
            .Where(t => !t.IsDeleted)
            .ToListAsync();

        var pois = await context.PointsOfInterest
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        foreach (var poi in pois)
        {
            var poiTranslations = existingTranslations
                .Where(t => t.PointOfInterestId == poi.Id)
                .ToList();

            var viTranslation = poiTranslations.FirstOrDefault(t =>
                string.Equals(t.LanguageCode, LanguageConstants.Vietnamese, StringComparison.OrdinalIgnoreCase));

            if (viTranslation == null)
            {
                viTranslation = new PointOfInterestTranslation
                {
                    PointOfInterestId = poi.Id,
                    LanguageCode = LanguageConstants.Vietnamese,
                    Name = poi.Name,
                    Description = poi.Description,
                    Address = poi.Address
                };

                context.PointOfInterestTranslations.Add(viTranslation);
                poiTranslations.Add(viTranslation);
                existingTranslations.Add(viTranslation);
            }

            foreach (var lang in LanguageConstants.SupportedLanguages)
            {
                if (poiTranslations.Any(t => string.Equals(t.LanguageCode, lang, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var defaultName = !string.IsNullOrWhiteSpace(viTranslation.Name) ? viTranslation.Name : poi.Name;
                var defaultDescription = !string.IsNullOrWhiteSpace(viTranslation.Description) ? viTranslation.Description : poi.Description;
                var defaultAddress = !string.IsNullOrWhiteSpace(viTranslation.Address) ? viTranslation.Address : poi.Address;

                var newTranslation = new PointOfInterestTranslation
                {
                    PointOfInterestId = poi.Id,
                    LanguageCode = lang,
                    Name = defaultName,
                    Description = defaultDescription,
                    Address = defaultAddress
                };

                context.PointOfInterestTranslations.Add(newTranslation);
                poiTranslations.Add(newTranslation);
                existingTranslations.Add(newTranslation);
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Backfill default translation rows for existing tours.
    /// Vietnamese row is canonical source; missing en/ko rows are created from vi/base text.
    /// </summary>
    private static async Task EnsureTourTranslationsAsync(VKStreetFoodDbContext context)
    {
        var existingTranslations = await context.TourTranslations
            .Where(t => !t.IsDeleted)
            .ToListAsync();

        var tours = await context.Tours
            .Where(t => !t.IsDeleted)
            .ToListAsync();

        foreach (var tour in tours)
        {
            var tourTranslations = existingTranslations
                .Where(t => t.TourId == tour.Id)
                .ToList();

            var viTranslation = tourTranslations.FirstOrDefault(t =>
                string.Equals(t.LanguageCode, LanguageConstants.Vietnamese, StringComparison.OrdinalIgnoreCase));

            if (viTranslation == null)
            {
                viTranslation = new TourTranslation
                {
                    TourId = tour.Id,
                    LanguageCode = LanguageConstants.Vietnamese,
                    Name = tour.Name,
                    Description = tour.Description
                };

                context.TourTranslations.Add(viTranslation);
                tourTranslations.Add(viTranslation);
                existingTranslations.Add(viTranslation);
            }

            foreach (var lang in LanguageConstants.SupportedLanguages)
            {
                if (tourTranslations.Any(t => string.Equals(t.LanguageCode, lang, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var defaultName = !string.IsNullOrWhiteSpace(viTranslation.Name) ? viTranslation.Name : tour.Name;
                var defaultDescription = !string.IsNullOrWhiteSpace(viTranslation.Description) ? viTranslation.Description : tour.Description;

                var newTranslation = new TourTranslation
                {
                    TourId = tour.Id,
                    LanguageCode = lang,
                    Name = defaultName,
                    Description = defaultDescription
                };

                context.TourTranslations.Add(newTranslation);
                tourTranslations.Add(newTranslation);
                existingTranslations.Add(newTranslation);
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Ensure 11 food stalls (POI id 2..12) always have vendor records.
    /// Existing vendors are preserved; missing ones are created with default placeholders.
    /// </summary>
    private static async Task EnsureBaselineVendorsAsync(VKStreetFoodDbContext context)
    {
        var baselinePois = await context.PointsOfInterest
            .Where(p => !p.IsDeleted && p.IsActive && p.Id >= 2 && p.Id <= 12)
            .OrderBy(p => p.Id)
            .ToListAsync();

        if (baselinePois.Count == 0)
            return;

        var existingVendorPoiIds = await context.Vendors
            .Where(v => !v.IsDeleted)
            .Select(v => v.PointOfInterestId)
            .ToListAsync();

        var missing = baselinePois
            .Where(p => !existingVendorPoiIds.Contains(p.Id))
            .ToList();

        if (missing.Count == 0)
            return;

        foreach (var poi in missing)
        {
            context.Vendors.Add(new Vendor
            {
                Name = poi.Name,
                Description = poi.Description,
                ContactPerson = "Chủ quán",
                PhoneNumber = "0900000000",
                Email = $"owner-poi-{poi.Id}@vkstreetfood.vn",
                PointOfInterestId = poi.Id,
                ImageUrl = poi.ImageUrl,
                IsActive = true,
                AverageRating = poi.AverageRating,
                TotalReviews = poi.TotalRatings
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Ensure baseline owner accounts exist for existing 11 vendors (POI id 2..12).
    /// These accounts are pre-verified so current shops can login immediately.
    /// </summary>
    private static async Task EnsureBaselineOwnerUsersAsync(VKStreetFoodDbContext context)
    {
        var baselineVendors = await context.Vendors
            .Where(v => !v.IsDeleted && v.PointOfInterestId >= 2 && v.PointOfInterestId <= 12)
            .OrderBy(v => v.PointOfInterestId)
            .ToListAsync();

        if (baselineVendors.Count == 0)
            return;

        var existingUsers = await context.Users
            .Where(u => !u.IsDeleted)
            .ToListAsync();

        var defaultPasswordHash = HashPassword("Owner@2026");

        foreach (var vendor in baselineVendors)
        {
            var defaultEmail = $"owner-poi-{vendor.PointOfInterestId}@vkstreetfood.vn";

            var owner = existingUsers.FirstOrDefault(u =>
                u.VendorId == vendor.Id ||
                string.Equals(u.Email, defaultEmail, StringComparison.OrdinalIgnoreCase));

            if (owner == null)
            {
                owner = new User
                {
                    Email = defaultEmail,
                    FullName = string.IsNullOrWhiteSpace(vendor.ContactPerson) ? $"Owner {vendor.Name}" : vendor.ContactPerson,
                    Role = "poi_owner",
                    VendorId = vendor.Id,
                    IsVerified = true,
                    PasswordHash = defaultPasswordHash,
                    LastLoginAt = null
                };

                context.Users.Add(owner);
                existingUsers.Add(owner);
            }
            else
            {
                owner.Role = "poi_owner";
                owner.VendorId = vendor.Id;
                owner.IsVerified = true;

                if (string.IsNullOrWhiteSpace(owner.PasswordHash))
                    owner.PasswordHash = defaultPasswordHash;
            }
        }

        await context.SaveChangesAsync();

        var existingRegs = await context.PoiOwnerRegistrations
            .Where(r => !r.IsDeleted)
            .ToListAsync();

        foreach (var vendor in baselineVendors)
        {
            var owner = await context.Users
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.VendorId == vendor.Id && u.Role == "poi_owner");

            if (owner == null)
                continue;

            var hasApprovedReg = existingRegs.Any(r =>
                r.UserId == owner.Id &&
                r.VendorId == vendor.Id &&
                r.Status == "approved");

            if (hasApprovedReg)
                continue;

            context.PoiOwnerRegistrations.Add(new PoiOwnerRegistration
            {
                UserId = owner.Id,
                VendorId = vendor.Id,
                PointOfInterestId = vendor.PointOfInterestId,
                ShopName = vendor.Name,
                ShopAddress = null,
                ContactPhone = vendor.PhoneNumber,
                Notes = "Baseline auto-approved for existing shop",
                Status = "approved",
                ReviewedAt = DateTime.UtcNow,
                ReviewNote = "Auto-approved baseline owner"
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Ensure baseline tours always exist for admin tour management page.
    /// Idempotent by tour name (case-insensitive), does not duplicate records.
    /// </summary>
    private static async Task EnsureBaselineToursAsync(VKStreetFoodDbContext context)
    {
        var pois = await context.PointsOfInterest
            .Where(p => !p.IsDeleted && p.IsActive)
            .ToListAsync();

        if (pois.Count == 0)
            return;

        var poiByName = pois
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var baselineTours = new[]
        {
            new
            {
                Name = "Tour ẩm thực buổi sáng",
                Description = "Khám phá các món ăn sáng đặc trưng tại khu Vĩnh Khánh.",
                Emoji = "🌅",
                EstimatedDurationMinutes = 120,
                Status = "active",
                PoiNames = new[]
                {
                    "Cổng chào Phố Ẩm thực Vĩnh Khánh",
                    "Bún Cá Châu Đốc Dì Tư",
                    "Ốc Vũ",
                    "Ốc Thảo"
                }
            },
            new
            {
                Name = "Tour đường phố ban đêm",
                Description = "Trải nghiệm ẩm thực phố đêm sôi động và đặc sắc.",
                Emoji = "🌙",
                EstimatedDurationMinutes = 180,
                Status = "active",
                PoiNames = new[]
                {
                    "Cổng chào Phố Ẩm thực Vĩnh Khánh",
                    "Ốc Oanh",
                    "A Fat Hot Pot",
                    "Alo Quán – Seafood & Beer",
                    "Lãng Quán",
                    "Ớt Xiêm Quán"
                }
            },
            new
            {
                Name = "Tour hải sản tươi sống",
                Description = "Thưởng thức hải sản tươi ngon tại các quán nổi bật.",
                Emoji = "🦞",
                EstimatedDurationMinutes = 90,
                Status = "draft",
                PoiNames = new[]
                {
                    "Ốc Vũ",
                    "Ốc Oanh",
                    "Ốc Đào 2",
                    "Alo Quán – Seafood & Beer"
                }
            },
            new
            {
                Name = "Tour lẩu & nướng",
                Description = "Hành trình cho tín đồ lẩu và nướng tại Vĩnh Khánh.",
                Emoji = "🍲",
                EstimatedDurationMinutes = 150,
                Status = "draft",
                PoiNames = new[]
                {
                    "A Fat Hot Pot",
                    "Chilli Lẩu Nướng Tự Chọn",
                    "Lãng Quán",
                    "Ớt Xiêm Quán"
                }
            }
        };

        var existingTours = await context.Tours
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        foreach (var template in baselineTours)
        {
            var existed = existingTours.Any(t =>
                string.Equals(t.Name?.Trim(), template.Name, StringComparison.OrdinalIgnoreCase));

            if (existed)
                continue;

            var tour = new Tour
            {
                Name = template.Name,
                Description = template.Description,
                Emoji = template.Emoji,
                EstimatedDurationMinutes = template.EstimatedDurationMinutes,
                Status = template.Status
            };

            context.Tours.Add(tour);
            await context.SaveChangesAsync();

            var sortOrder = 1;
            foreach (var poiName in template.PoiNames)
            {
                if (!poiByName.TryGetValue(poiName, out var poi))
                    continue;

                context.TourPointsOfInterest.Add(new TourPointOfInterest
                {
                    TourId = tour.Id,
                    PointOfInterestId = poi.Id,
                    SortOrder = sortOrder++
                });
            }

            await context.SaveChangesAsync();
        }
    }

    private static string HashPassword(string plainText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToHexString(bytes);
    }
}
