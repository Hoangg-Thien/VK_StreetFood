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
            new Category { Id = Guid.NewGuid(), Name = "Ốc & Hải sản", Description = "Các món ốc và hải sản", IconUrl = "🦞", DisplayOrder = 1, IsActive = true },
            new Category { Id = Guid.NewGuid(), Name = "Lẩu & Nướng", Description = "Các món lẩu và nướng", IconUrl = "🍲", DisplayOrder = 2, IsActive = true },
            new Category { Id = Guid.NewGuid(), Name = "Món chính", Description = "Các món ăn chính", IconUrl = "🍜", DisplayOrder = 3, IsActive = true },
            new Category { Id = Guid.NewGuid(), Name = "Đặc sản", Description = "Đặc sản vùng miền", IconUrl = "⭐", DisplayOrder = 4, IsActive = true }
        };
        context.Categories.AddRange(categories);

        // Seed Tags
        var tags = new List<Tag>
        {
            new Tag { Id = Guid.NewGuid(), Name = "Michelin", ColorCode = "#DC2626" },
            new Tag { Id = Guid.NewGuid(), Name = "Đặc sản", ColorCode = "#EF4444" },
            new Tag { Id = Guid.NewGuid(), Name = "Phổ biến", ColorCode = "#3B82F6" },
            new Tag { Id = Guid.NewGuid(), Name = "Giá rẻ", ColorCode = "#F59E0B" },
            new Tag { Id = Guid.NewGuid(), Name = "Mở cửa đêm", ColorCode = "#8B5CF6" }
        };
        context.Tags.AddRange(tags);

        await context.SaveChangesAsync();

        // Seed Points of Interest - Real data from Vĩnh Khánh Food Street
        var pois = new List<PointOfInterest>
        {
            // 1. Cổng vào
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Cổng chào Phố Ẩm thực Vĩnh Khánh",
                Description = "Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – 'thiên đường không ngủ' của Quận 4. Được Time Out vinh danh là một trong những đường phố thú vị nhất thế giới năm 2025.",
                Latitude = 10.761905898335831,
                Longitude = 106.70222716527056,
                Address = "Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-ENTRANCE",
                ImageUrl = "/images/poi/entrance.jpg",
                IsActive = true,
                CategoryId = categories[3].Id,
                ViewCount = 0,
                AverageRating = 0,
                TotalRatings = 0
            },
            
            // 2. Ốc Vũ
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Ốc Vũ",
                Description = "Quán ốc nổi tiếng với hơn một thập kỷ hoạt động. Nổi tiếng với nguồn hải sản tươi sống và nước sốt me 'thần thánh' - chua thanh, cay nhẹ, tạo nên bản giao hưởng vị giác khó quên.",
                Latitude = 10.761518431027818,
                Longitude = 106.70271542519974,
                Address = "37 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-OC-VU",
                ImageUrl = "/images/poi/oc-vu.jpg",
                IsActive = true,
                CategoryId = categories[0].Id,
                ViewCount = 0,
                AverageRating = 4.5m,
                TotalRatings = 0
            },
            
            // 3. Ốc Thảo
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Ốc Thảo",
                Description = "Không gian rộng rãi, thoáng đãng với triết lý tôn vinh vị ngọt tự nhiên của nguyên liệu. Ốc len xào dừa được đánh giá là cực phẩm với nước cốt dừa béo ngậy không gây ngán.",
                Latitude = 10.761795162597451,
                Longitude = 106.70239298897182,
                Address = "383 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-OC-THAO",
                ImageUrl = "/images/poi/oc-thao.jpg",
                IsActive = true,
                CategoryId = categories[0].Id,
                ViewCount = 0,
                AverageRating = 4.3m,
                TotalRatings = 0
            },
            
            // 4. Ốc Sáu Nở
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Ốc Sáu Nở",
                Description = "Hiện thân của văn hóa ốc vỉa hè Sài Gòn nguyên bản. Nổi tiếng với ốc hương sốt trứng muối - sốt vàng ươm, béo bùi, mặn ngọt hài hòa, chấm kèm bánh mì giòn tan.",
                Latitude = 10.761038078500885,
                Longitude = 106.70290444809687,
                Address = "128 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-OC-SAU-NO",
                ImageUrl = "/images/poi/oc-sau-no.jpg",
                IsActive = true,
                CategoryId = categories[0].Id,
                ViewCount = 0,
                AverageRating = 4.4m,
                TotalRatings = 0
            },
            
            // 5. Ốc Oanh (Michelin Bib Gourmand)
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Ốc Oanh",
                Description = "Quán ốc vinh dự được Michelin Guide trao tặng danh hiệu Bib Gourmand năm 2024. Hơn 20 năm từ gánh hàng rong vươn lên thành thương hiệu quốc tế. Ốc hương xào bơ tỏi là món làm nên tên tuổi.",
                Latitude = 10.760848629826567,
                Longitude = 106.7032957744219,
                Address = "96 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-OC-OANH",
                ImageUrl = "/images/poi/oc-oanh.jpg",
                IsActive = true,
                CategoryId = categories[0].Id,
                ViewCount = 0,
                AverageRating = 4.8m,
                TotalRatings = 0
            },
            
            // 6. A Fat Hot Pot
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "A Fat Hot Pot",
                Description = "Không gian Hong Kong retro những năm 80-90 với decor điện ảnh TVB, bảng hiệu neon và nhạc Hoa xưa. Nổi tiếng với Lẩu Trường Thọ xanh và Lẩu Collagen - nước dùng thanh ngọt, ninh từ xương.",
                Latitude = 10.760806933075282,
                Longitude = 106.70347875218654,
                Address = "668 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-A-FAT",
                ImageUrl = "/images/poi/a-fat.jpg",
                IsActive = true,
                CategoryId = categories[1].Id,
                ViewCount = 0,
                AverageRating = 4.2m,
                TotalRatings = 0
            },
            
            // 7. Chilli Lẩu Nướng
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Chilli Lẩu Nướng Tự Chọn",
                Description = "Thiên đường dành cho giới trẻ với mô hình buffet linh hoạt. Lẩu Hàu Kimchi trứ danh - sự kết hợp táo bạo giữa kim chi Hàn Quốc và hàu sữa Việt Nam. Giá cả hợp lý.",
                Latitude = 10.760794431975599,
                Longitude = 106.7036590681073,
                Address = "232 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-CHILLI",
                ImageUrl = "/images/poi/chilli.jpg",
                IsActive = true,
                CategoryId = categories[1].Id,
                ViewCount = 0,
                AverageRating = 4.1m,
                TotalRatings = 0
            },
            
            // 8. Alo Quán
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Alo Quán – Seafood & Beer",
                Description = "Không gian mở thoáng đãng, thiết kế trẻ trung hiện đại. Giao thoa thú vị giữa ẩm thực Việt và Thái. Tôm sốt Thái chua cay xé lưỡi, nghêu hấp sả thanh tao. Lý tưởng cho những cuộc vui xuyên đêm.",
                Latitude = 10.761127163188009,
                Longitude = 106.70475425408135,
                Address = "333 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-ALO-QUAN",
                ImageUrl = "/images/poi/alo-quan.jpg",
                IsActive = true,
                CategoryId = categories[0].Id,
                ViewCount = 0,
                AverageRating = 4.3m,
                TotalRatings = 0
            },
            
            // 9. Ốc Đào 2
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Ốc Đào 2",
                Description = "Chi nhánh của thương hiệu Ốc Đào lừng danh. Nghệ thuật chế biến gia vị đỉnh cao. Răng mực xào bơ tỏi giòn sần sật, ốc móng tay xào me chua thanh tinh tế. Tinh tế trong từng loại nước sốt.",
                Latitude = 10.761347965170131,
                Longitude = 106.70496784739889,
                Address = "Hẻm 232 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-OC-DAO-2",
                ImageUrl = "/images/poi/oc-dao-2.jpg",
                IsActive = true,
                CategoryId = categories[0].Id,
                ViewCount = 0,
                AverageRating = 4.4m,
                TotalRatings = 0
            },
            
            // 10. Lãng Quán
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Lãng Quán",
                Description = "Quy mô khủng với hai mặt bằng đối diện, luôn tấp nập khách. Giò heo muối chiên giòn - da giòn rụm, thịt mềm mọng. Mở xuyên đêm đến 4 giờ sáng, cứu cánh cho những 'cú đêm'.",
                Latitude = 10.761149988188182,
                Longitude = 106.70538401196282,
                Address = "Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-LANG-QUAN",
                ImageUrl = "/images/poi/lang-quan.jpg",
                IsActive = true,
                CategoryId = categories[1].Id,
                ViewCount = 0,
                AverageRating = 4.2m,
                TotalRatings = 0
            },
            
            // 11. Ớt Xiêm Quán
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Ớt Xiêm Quán",
                Description = "Trải nghiệm vị giác bùng nổ với các món nướng cay nồng. Ếch nướng muối ớt - thịt chắc nịch, da giòn, thấm đẫm muối ớt cay xè. Chẳng dừng nướng (phần thịt heo quý hiếm) là món mồi được săn đón.",
                Latitude = 10.761185236052697,
                Longitude = 106.70570361039157,
                Address = "568 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-OT-XIEM",
                ImageUrl = "/images/poi/ot-xiem.jpg",
                IsActive = true,
                CategoryId = categories[1].Id,
                ViewCount = 0,
                AverageRating = 4.3m,
                TotalRatings = 0
            },
            
            // 12. Bún Cá Châu Đốc
            new PointOfInterest
            {
                Id = Guid.NewGuid(),
                Name = "Bún Cá Châu Đốc Dì Tư",
                Description = "Nốt kết thanh bình với hương vị miền Tây. Tô bún cá vàng ươm nghệ, nước dùng thanh ngọt từ cá lóc và ngải bún. Bông điên điển tạo vị nhẫn nhẹ giòn giòn. Món giải ngấy hoàn hảo sau hải sản nướng.",
                Latitude = 10.761123552506971,
                Longitude = 106.70660690985743,
                Address = "320/79 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
                QRCode = "VK-BUN-CA",
                ImageUrl = "/images/poi/bun-ca.jpg",
                IsActive = true,
                CategoryId = categories[2].Id,
                ViewCount = 0,
                AverageRating = 4.5m,
                TotalRatings = 0
            }
        };

        context.PointsOfInterest.AddRange(pois);
        await context.SaveChangesAsync();

        // Link tags to POIs
        var michelinTag = tags.First(t => t.Name == "Michelin");
        var popularTag = tags.First(t => t.Name == "Phổ biến");
        var specialtyTag = tags.First(t => t.Name == "Đặc sản");
        var cheapTag = tags.First(t => t.Name == "Giá rẻ");
        var nightTag = tags.First(t => t.Name == "Mở cửa đêm");

        // Ốc Oanh - Michelin
        pois[4].Tags.Add(michelinTag);
        pois[4].Tags.Add(popularTag);

        // Ốc Vũ, Ốc Sáu Nở - Popular
        pois[1].Tags.Add(popularTag);
        pois[3].Tags.Add(popularTag);
        pois[3].Tags.Add(cheapTag);

        // Chilli - Cheap & Popular
        pois[6].Tags.Add(cheapTag);
        pois[6].Tags.Add(popularTag);

        // Lãng Quán - Night
        pois[9].Tags.Add(nightTag);

        // Bún Cá - Specialty
        pois[11].Tags.Add(specialtyTag);

        await context.SaveChangesAsync();

        // Seed Audio Contents for each POI
        var audioContents = new List<AudioContent>();

        foreach (var poi in pois)
        {
            // Vietnamese audio
            audioContents.Add(new AudioContent
            {
                PointOfInterestId = poi.Id,
                LanguageCode = LanguageConstants.Vietnamese,
                TextContent = poi.Description,
                AudioFileUrl = $"/audio/vi/{poi.QRCode}.mp3",
                DurationInSeconds = 30,
                IsGenerated = false
            });

            // English audio  
            audioContents.Add(new AudioContent
            {
                PointOfInterestId = poi.Id,
                LanguageCode = LanguageConstants.English,
                TextContent = $"Welcome to {poi.Name}. {poi.Description}",
                AudioFileUrl = $"/audio/en/{poi.QRCode}.mp3",
                DurationInSeconds = 25,
                IsGenerated = false
            });

            // Korean audio
            audioContents.Add(new AudioContent
            {
                PointOfInterestId = poi.Id,
                LanguageCode = LanguageConstants.Korean,
                TextContent = $"{poi.Name}에 오신 것을 환영합니다",
                AudioFileUrl = $"/audio/ko/{poi.QRCode}.mp3",
                DurationInSeconds = 20,
                IsGenerated = false
            });
        }

        context.AudioContents.AddRange(audioContents);
        await context.SaveChangesAsync();

        // Seed Vendors for top POIs
        var vendors = new List<Vendor>
        {
            new Vendor
            {
                Name = "Ốc Vũ",
                Description = "Quán ốc nổi tiếng với nước sốt me thần thánh",
                ContactPerson = "Anh Vũ",
                PhoneNumber = "0909123456",
                Email = "ocvu@gmail.com",
                PointOfInterestId = pois[1].Id,
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
                Email = "ocanh@gmail.com",
                PointOfInterestId = pois[4].Id,
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
                PointOfInterestId = pois[5].Id,
                ImageUrl = "/images/vendor/a-fat.jpg",
                IsActive = true,
                AverageRating = 4.2m,
                TotalReviews = 89
            }
        };

        context.Vendors.AddRange(vendors);
        await context.SaveChangesAsync();

        // Seed Products
        var products = new List<Product>
        {
            // Ốc Vũ products
            new Product { Name = "Ốc hương rang muối ớt", Description = "Món đặc trưng của quán", Price = 50000, VendorId = vendors[0].Id, IsAvailable = true, ImageUrl = "/images/products/oc-huong.jpg" },
            new Product { Name = "Sò điệp nướng mỡ hành", Description = "Tươi ngon mỗi ngày", Price = 80000, VendorId = vendors[0].Id, IsAvailable = true, ImageUrl = "/images/products/so-diep.jpg" },
            new Product { Name = "Nghêu hấp sả", Description = "Thanh ngọt tự nhiên", Price = 45000, VendorId = vendors[0].Id, IsAvailable = true },
            
            // Ốc Oanh products (Michelin)
            new Product { Name = "Ốc hương xào bơ tỏi", Description = "Món làm nên tên tuổi - Michelin recommended", Price = 70000, VendorId = vendors[1].Id, IsAvailable = true, ImageUrl = "/images/products/oc-bo-toi.jpg" },
            new Product { Name = "Càng ghẹ rang muối", Description = "Tươi sống mỗi ngày", Price = 150000, VendorId = vendors[1].Id, IsAvailable = true },
            new Product { Name = "Ốc len xào dừa", Description = "Đặc sản miền Tây", Price = 55000, VendorId = vendors[1].Id, IsAvailable = true },
            
            // A Fat Hot Pot products
            new Product { Name = "Lẩu Trường Thọ (xanh)", Description = "Signature hotpot", Price = 250000, VendorId = vendors[2].Id, IsAvailable = true, ImageUrl = "/images/products/lau-xanh.jpg" },
            new Product { Name = "Lẩu Collagen", Description = "Bổ dưỡng, đẹp da", Price = 280000, VendorId = vendors[2].Id, IsAvailable = true },
            new Product { Name = "Combo hải sản tươi", Description = "Tự chọn topping", Price = 350000, VendorId = vendors[2].Id, IsAvailable = true }
        };

        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        // Seed Opening Hours for vendors
        var openingHours = new List<OpeningHours>();
        foreach (var vendor in vendors)
        {
            for (int day = 0; day <= 6; day++)
            {
                openingHours.Add(new OpeningHours
                {
                    VendorId = vendor.Id,
                    DayOfWeek = day,
                    OpenTime = new TimeSpan(15, 0, 0), // 3 PM
                    CloseTime = new TimeSpan(23, 0, 0), // 11 PM
                    IsClosed = false
                });
            }
        }

        context.OpeningHours.AddRange(openingHours);
        await context.SaveChangesAsync();
    }
}