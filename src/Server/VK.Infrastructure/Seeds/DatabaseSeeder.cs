using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Shared.Constants;

namespace VK.Infrastructure.Seeds;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(VKStreetFoodDbContext context)
    {
        if (context.PointsOfInterest.Any())
        {
            return; // Database has been seeded
        }

        // Seed Categories
        var categories = new List<Category>
        {
            new Category { Name = "á»c & Háº£i sáº£n", Description = "CÃ¡c mÃ³n á»‘c vÃ  háº£i sáº£n", IconUrl = "ðŸ¦ž", DisplayOrder = 1, IsActive = true },
            new Category { Name = "Láº©u & NÆ°á»›ng", Description = "CÃ¡c mÃ³n láº©u vÃ  nÆ°á»›ng", IconUrl = "ðŸ²", DisplayOrder = 2, IsActive = true },
            new Category { Name = "MÃ³n chÃ­nh", Description = "CÃ¡c mÃ³n Äƒn chÃ­nh", IconUrl = "ðŸœ", DisplayOrder = 3, IsActive = true },
            new Category { Name = "Äáº·c sáº£n", Description = "Äáº·c sáº£n vÃ¹ng miá»n", IconUrl = "â­", DisplayOrder = 4, IsActive = true }
        };
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        // Seed Tags
        var tags = new List<Tag>
        {
            new Tag { Name = "Michelin", ColorCode = "#DC2626" },
            new Tag { Name = "Äáº·c sáº£n", ColorCode = "#EF4444" },
            new Tag { Name = "Phá»• biáº¿n", ColorCode = "#3B82F6" },
            new Tag { Name = "GiÃ¡ ráº»", ColorCode = "#F59E0B" },
            new Tag { Name = "Má»Ÿ cá»­a Ä‘Ãªm", ColorCode = "#8B5CF6" }
        };
        context.Tags.AddRange(tags);
        await context.SaveChangesAsync();

        // Seed Points of Interest - Real data from VÄ©nh KhÃ¡nh Food Street
        var pois = new List<PointOfInterest>
        {
            // 1. Cá»•ng vÃ o
            new PointOfInterest
            {
                Name = "Cá»•ng chÃ o Phá»‘ áº¨m thá»±c VÄ©nh KhÃ¡nh",
                Description = "ChÃ o má»«ng báº¡n Ä‘áº¿n vá»›i Phá»‘ áº¨m thá»±c VÄ©nh KhÃ¡nh â€“ 'thiÃªn Ä‘Æ°á»ng khÃ´ng ngá»§' cá»§a Quáº­n 4. ÄÆ°á»£c Time Out vinh danh lÃ  má»™t trong nhá»¯ng Ä‘Æ°á»ng phá»‘ thÃº vá»‹ nháº¥t tháº¿ giá»›i nÄƒm 2025.",
                Latitude = 10.761905898335831,
                Longitude = 106.70222716527056,
                Address = "VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/entrance.jpg",
                IsActive = true,
                CategoryId = 4, // Äáº·c sáº£n
                AverageRating = 0,
                TotalRatings = 0
            },
            
            // 2. á»c VÅ©
            new PointOfInterest
            {
                Name = "á»c VÅ©",
                Description = "QuÃ¡n á»‘c ná»•i tiáº¿ng vá»›i hÆ¡n má»™t tháº­p ká»· hoáº¡t Ä‘á»™ng. Ná»•i tiáº¿ng vá»›i nguá»“n háº£i sáº£n tÆ°Æ¡i sá»‘ng vÃ  nÆ°á»›c sá»‘t me 'tháº§n thÃ¡nh' - chua thanh, cay nháº¹, táº¡o nÃªn báº£n giao hÆ°á»Ÿng vá»‹ giÃ¡c khÃ³ quÃªn.",
                Latitude = 10.761518431027818,
                Longitude = 106.70271542519974,
                Address = "37 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/oc-vu.jpg",
                IsActive = true,
                CategoryId = 1, // á»c & Háº£i sáº£n

                AverageRating = 4.5m,
                TotalRatings = 0
            },
            
            // 3. á»c Tháº£o
            new PointOfInterest
            {
                Name = "á»c Tháº£o",
                Description = "KhÃ´ng gian rá»™ng rÃ£i, thoÃ¡ng Ä‘Ã£ng vá»›i triáº¿t lÃ½ tÃ´n vinh vá»‹ ngá»t tá»± nhiÃªn cá»§a nguyÃªn liá»‡u. á»c len xÃ o dá»«a Ä‘Æ°á»£c Ä‘Ã¡nh giÃ¡ lÃ  cá»±c pháº©m vá»›i nÆ°á»›c cá»‘t dá»«a bÃ©o ngáº­y khÃ´ng gÃ¢y ngÃ¡n.",
                Latitude = 10.761795162597451,
                Longitude = 106.70239298897182,
                Address = "383 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/oc-thao.jpg",
                IsActive = true,
                CategoryId = 1,

                AverageRating = 4.3m,
                TotalRatings = 0
            },
            
            // 4. á»c SÃ¡u Ná»Ÿ
            new PointOfInterest
            {
                Name = "á»c SÃ¡u Ná»Ÿ",
                Description = "Hiá»‡n thÃ¢n cá»§a vÄƒn hÃ³a á»‘c vá»‰a hÃ¨ SÃ i GÃ²n nguyÃªn báº£n. Ná»•i tiáº¿ng vá»›i á»‘c hÆ°Æ¡ng sá»‘t trá»©ng muá»‘i - sá»‘t vÃ ng Æ°Æ¡m, bÃ©o bÃ¹i, máº·n ngá»t hÃ i hÃ²a, cháº¥m kÃ¨m bÃ¡nh mÃ¬ giÃ²n tan.",
                Latitude = 10.761038078500885,
                Longitude = 106.70290444809687,
                Address = "128 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/oc-sau-no.jpg",
                IsActive = true,
                CategoryId = 1,

                AverageRating = 4.4m,
                TotalRatings = 0
            },
            
            // 5. á»c Oanh (Michelin Bib Gourmand)
            new PointOfInterest
            {
                Name = "á»c Oanh",
                Description = "QuÃ¡n á»‘c vinh dá»± Ä‘Æ°á»£c Michelin Guide trao táº·ng danh hiá»‡u Bib Gourmand nÄƒm 2024. HÆ¡n 20 nÄƒm tá»« gÃ¡nh hÃ ng rong vÆ°Æ¡n lÃªn thÃ nh thÆ°Æ¡ng hiá»‡u quá»‘c táº¿. á»c hÆ°Æ¡ng xÃ o bÆ¡ tá»i lÃ  mÃ³n lÃ m nÃªn tÃªn tuá»•i.",
                Latitude = 10.760848629826567,
                Longitude = 106.7032957744219,
                Address = "96 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/oc-oanh.jpg",
                IsActive = true,
                CategoryId = 1,

                AverageRating = 4.8m,
                TotalRatings = 0
            },
            
            // 6. A Fat Hot Pot
            new PointOfInterest
            {
                Name = "A Fat Hot Pot",
                Description = "KhÃ´ng gian Hong Kong retro nhá»¯ng nÄƒm 80-90 vá»›i decor Ä‘iá»‡n áº£nh TVB, báº£ng hiá»‡u neon vÃ  nháº¡c Hoa xÆ°a. Ná»•i tiáº¿ng vá»›i Láº©u TrÆ°á»ng Thá» xanh vÃ  Láº©u Collagen - nÆ°á»›c dÃ¹ng thanh ngá»t, ninh tá»« xÆ°Æ¡ng.",
                Latitude = 10.760806933075282,
                Longitude = 106.70347875218654,
                Address = "668 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/a-fat.jpg",
                IsActive = true,
                CategoryId = 2, // Láº©u & NÆ°á»›ng

                AverageRating = 4.2m,
                TotalRatings = 0
            },
            
            // 7. Chilli Láº©u NÆ°á»›ng
            new PointOfInterest
            {
                Name = "Chilli Láº©u NÆ°á»›ng Tá»± Chá»n",
                Description = "ThiÃªn Ä‘Æ°á»ng dÃ nh cho giá»›i tráº» vá»›i mÃ´ hÃ¬nh buffet linh hoáº¡t. Láº©u HÃ u Kimchi trá»© danh - sá»± káº¿t há»£p tÃ¡o báº¡o giá»¯a kim chi HÃ n Quá»‘c vÃ  hÃ u sá»¯a Viá»‡t Nam. GiÃ¡ cáº£ há»£p lÃ½.",
                Latitude = 10.760794431975599,
                Longitude = 106.7036590681073,
                Address = "232 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/chilli.jpg",
                IsActive = true,
                CategoryId = 2,

                AverageRating = 4.1m,
                TotalRatings = 0
            },
            
            // 8. Alo QuÃ¡n
            new PointOfInterest
            {
                Name = "Alo QuÃ¡n â€“ Seafood & Beer",
                Description = "KhÃ´ng gian má»Ÿ thoÃ¡ng Ä‘Ã£ng, thiáº¿t káº¿ tráº» trung hiá»‡n Ä‘áº¡i. Giao thoa thÃº vá»‹ giá»¯a áº©m thá»±c Viá»‡t vÃ  ThÃ¡i. TÃ´m sá»‘t ThÃ¡i chua cay xÃ© lÆ°á»¡i, nghÃªu háº¥p sáº£ thanh tao. LÃ½ tÆ°á»Ÿng cho nhá»¯ng cuá»™c vui xuyÃªn Ä‘Ãªm.",
                Latitude = 10.761127163188009,
                Longitude = 106.70475425408135,
                Address = "333 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/alo-quan.jpg",
                IsActive = true,
                CategoryId = 1,

                AverageRating = 4.3m,
                TotalRatings = 0
            },
            
            // 9. á»c ÄÃ o 2
            new PointOfInterest
            {
                Name = "á»c ÄÃ o 2",
                Description = "Chi nhÃ¡nh cá»§a thÆ°Æ¡ng hiá»‡u á»c ÄÃ o lá»«ng danh. Nghá»‡ thuáº­t cháº¿ biáº¿n gia vá»‹ Ä‘á»‰nh cao. RÄƒng má»±c xÃ o bÆ¡ tá»i giÃ²n sáº§n sáº­t, á»‘c mÃ³ng tay xÃ o me chua thanh tinh táº¿. Tinh táº¿ trong tá»«ng loáº¡i nÆ°á»›c sá»‘t.",
                Latitude = 10.761347965170131,
                Longitude = 106.70496784739889,
                Address = "Háº»m 232 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/oc-dao-2.jpg",
                IsActive = true,
                CategoryId = 1,

                AverageRating = 4.4m,
                TotalRatings = 0
            },
            
            // 10. LÃ£ng QuÃ¡n
            new PointOfInterest
            {
                Name = "LÃ£ng QuÃ¡n",
                Description = "Quy mÃ´ khá»§ng vá»›i hai máº·t báº±ng Ä‘á»‘i diá»‡n, luÃ´n táº¥p náº­p khÃ¡ch. GiÃ² heo muá»‘i chiÃªn giÃ²n - da giÃ²n rá»¥m, thá»‹t má»m má»ng. Má»Ÿ xuyÃªn Ä‘Ãªm Ä‘áº¿n 4 giá» sÃ¡ng, cá»©u cÃ¡nh cho nhá»¯ng 'cÃº Ä‘Ãªm'.",
                Latitude = 10.761149988188182,
                Longitude = 106.70538401196282,
                Address = "VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/lang-quan.jpg",
                IsActive = true,
                CategoryId = 2,

                AverageRating = 4.2m,
                TotalRatings = 0
            },
            
            // 11. á»št XiÃªm QuÃ¡n
            new PointOfInterest
            {
                Name = "á»št XiÃªm QuÃ¡n",
                Description = "Tráº£i nghiá»‡m vá»‹ giÃ¡c bÃ¹ng ná»• vá»›i cÃ¡c mÃ³n nÆ°á»›ng cay ná»“ng. áº¾ch nÆ°á»›ng muá»‘i á»›t - thá»‹t cháº¯c ná»‹ch, da giÃ²n, tháº¥m Ä‘áº«m muá»‘i á»›t cay xÃ¨. Cháº³ng dá»«ng nÆ°á»›ng (pháº§n thá»‹t heo quÃ½ hiáº¿m) lÃ  mÃ³n má»“i Ä‘Æ°á»£c sÄƒn Ä‘Ã³n.",
                Latitude = 10.761185236052697,
                Longitude = 106.70570361039157,
                Address = "568 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/ot-xiem.jpg",
                IsActive = true,
                CategoryId = 2,

                AverageRating = 4.3m,
                TotalRatings = 0
            },
            
            // 12. BÃºn CÃ¡ ChÃ¢u Äá»‘c
            new PointOfInterest
            {
                Name = "BÃºn CÃ¡ ChÃ¢u Äá»‘c DÃ¬ TÆ°",
                Description = "Ná»‘t káº¿t thanh bÃ¬nh vá»›i hÆ°Æ¡ng vá»‹ miá»n TÃ¢y. TÃ´ bÃºn cÃ¡ vÃ ng Æ°Æ¡m nghá»‡, nÆ°á»›c dÃ¹ng thanh ngá»t tá»« cÃ¡ lÃ³c vÃ  ngáº£i bÃºn. BÃ´ng Ä‘iÃªn Ä‘iá»ƒn táº¡o vá»‹ nháº«n nháº¹ giÃ²n giÃ²n. MÃ³n giáº£i ngáº¥y hoÃ n háº£o sau háº£i sáº£n nÆ°á»›ng.",
                Latitude = 10.761123552506971,
                Longitude = 106.70660690985743,
                Address = "320/79 VÄ©nh KhÃ¡nh, PhÆ°á»ng 9, Quáº­n 4, TP.HCM",                ImageUrl = "/images/poi/bun-ca.jpg",
                IsActive = true,
                CategoryId = 3, // MÃ³n chÃ­nh

                AverageRating = 4.5m,
                TotalRatings = 0
            }
        };

        context.PointsOfInterest.AddRange(pois);
        await context.SaveChangesAsync();

        // Link tags to POIs (after IDs are generated)
        // á»c Oanh (ID 5) - Michelin + Popular
        pois[4].Tags.Add(tags[0]); // Michelin
        pois[4].Tags.Add(tags[2]); // Phá»• biáº¿n

        // á»c VÅ© (ID 2) - Popular
        pois[1].Tags.Add(tags[2]); // Phá»• biáº¿n

        // á»c SÃ¡u Ná»Ÿ (ID 4) - Popular + Cheap
        pois[3].Tags.Add(tags[2]); // Phá»• biáº¿n
        pois[3].Tags.Add(tags[3]); // GiÃ¡ ráº»

        // Chilli (ID 7) - Cheap + Popular
        pois[6].Tags.Add(tags[3]); // GiÃ¡ ráº»
        pois[6].Tags.Add(tags[2]); // Phá»• biáº¿n

        // LÃ£ng QuÃ¡n (ID 10) - Night
        pois[9].Tags.Add(tags[4]); // Má»Ÿ cá»­a Ä‘Ãªm

        // BÃºn CÃ¡ (ID 12) - Specialty
        pois[11].Tags.Add(tags[1]); // Äáº·c sáº£n

        await context.SaveChangesAsync();

        // Seed Audio Contents for each POI
        foreach (var poi in pois)
        {
            context.AudioContents.AddRange(new[]
            {
                new AudioContent
                {
                    PointOfInterestId = poi.Id,
                    LanguageCode = LanguageConstants.Vietnamese,
                    TextContent = $"{poi.Name}. {poi.Description}"
                },
                new AudioContent
                {
                    PointOfInterestId = poi.Id,
                    LanguageCode = LanguageConstants.English,
                    TextContent = $"Welcome to {poi.Name}. {poi.Description}"
                },
                new AudioContent
                {
                    PointOfInterestId = poi.Id,
                    LanguageCode = LanguageConstants.Korean,
                    TextContent = $"{poi.Name}ì— ì˜¤ì‹  ê²ƒì„ í™˜ì˜í•©ë‹ˆë‹¤. {poi.Description}"
                }
            });
        }
        await context.SaveChangesAsync();

        // Seed Vendors
        var vendors = new List<Vendor>
        {
            new Vendor
            {
                Name = "á»c VÅ©",
                Description = "QuÃ¡n á»‘c ná»•i tiáº¿ng vá»›i nÆ°á»›c sá»‘t me tháº§n thÃ¡nh",
                ContactPerson = "Anh VÅ©",
                PhoneNumber = "0909123456",
                Email = "ocvu@gmail.com",
                PointOfInterestId = 2, // á»c VÅ©
                ImageUrl = "/images/vendor/oc-vu.jpg",
                IsActive = true,
                AverageRating = 4.5m,
                TotalReviews = 120
            },
            new Vendor
            {
                Name = "á»c Oanh",
                Description = "Vinh dá»± Michelin Bib Gourmand 2024",
                ContactPerson = "Chá»‹ Oanh",
                PhoneNumber = "0918234567",
                Email = "ocanh@gmail.com",
                PointOfInterestId = 5, // á»c Oanh
                ImageUrl = "/images/vendor/oc-oanh.jpg",
                IsActive = true,
                AverageRating = 4.8m,
                TotalReviews = 450
            },
            new Vendor
            {
                Name = "A Fat Hot Pot",
                Description = "Láº©u phong cÃ¡ch Hong Kong retro",
                ContactPerson = "Manager",
                PhoneNumber = "0927345678",
                Email = "afathotpot@gmail.com",
                PointOfInterestId = 6, // A Fat
                ImageUrl = "/images/vendor/a-fat.jpg",
                IsActive = true,
                AverageRating = 4.2m,
                TotalReviews = 89
            }
        };

        context.Vendors.AddRange(vendors);
        await context.SaveChangesAsync();

        // Seed Products
        context.Products.AddRange(new[]
        {
            // á»c VÅ© products (VendorId = 1)
            new Product { Name = "á»c hÆ°Æ¡ng rang muá»‘i á»›t", Description = "MÃ³n Ä‘áº·c trÆ°ng cá»§a quÃ¡n", Price = 50000, VendorId = 1, IsAvailable = true, ImageUrl = "/images/products/oc-huong.jpg" },
            new Product { Name = "SÃ² Ä‘iá»‡p nÆ°á»›ng má»¡ hÃ nh", Description = "TÆ°Æ¡i ngon má»—i ngÃ y", Price = 80000, VendorId = 1, IsAvailable = true, ImageUrl = "/images/products/so-diep.jpg" },
            new Product { Name = "NghÃªu háº¥p sáº£", Description = "Thanh ngá»t tá»± nhiÃªn", Price = 45000, VendorId = 1, IsAvailable = true },
            
            // á»c Oanh products (VendorId = 2)
            new Product { Name = "á»c hÆ°Æ¡ng xÃ o bÆ¡ tá»i", Description = "MÃ³n lÃ m nÃªn tÃªn tuá»•i - Michelin recommended", Price = 70000, VendorId = 2, IsAvailable = true, ImageUrl = "/images/products/oc-bo-toi.jpg" },
            new Product { Name = "CÃ ng gháº¹ rang muá»‘i", Description = "TÆ°Æ¡i sá»‘ng má»—i ngÃ y", Price = 150000, VendorId = 2, IsAvailable = true },
            new Product { Name = "á»c len xÃ o dá»«a", Description = "Äáº·c sáº£n miá»n TÃ¢y", Price = 55000, VendorId = 2, IsAvailable = true },
            
            // A Fat products (VendorId = 3)
            new Product { Name = "Láº©u TrÆ°á»ng Thá» (xanh)", Description = "Signature hotpot", Price = 250000, VendorId = 3, IsAvailable = true, ImageUrl = "/images/products/lau-xanh.jpg" },
            new Product { Name = "Láº©u Collagen", Description = "Bá»• dÆ°á»¡ng, Ä‘áº¹p da", Price = 280000, VendorId = 3, IsAvailable = true },
            new Product { Name = "Combo háº£i sáº£n tÆ°Æ¡i", Description = "Tá»± chá»n topping", Price = 350000, VendorId = 3, IsAvailable = true }
        });
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
                    OpenTime = new TimeSpan(15, 0, 0), // 3 PM
                    CloseTime = new TimeSpan(23, 0, 0), // 11 PM
                    IsClosed = false
                });
            }
        }
        await context.SaveChangesAsync();
    }
}

