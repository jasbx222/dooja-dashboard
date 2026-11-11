using Dooja.Web.Models;
using Dooja.Web.Services;
using Microsoft.AspNetCore.Identity;
namespace Dooja.Web.Data;
public static class Seed
{
    public static async Task Run(DoojaDb db, IConfiguration config, IWebHostEnvironment env)
    {
        await ShopSchema.Apply(db);
        await new ShopService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ShopService>.Instance, config).EnsureDefaults();
        if (!db.Users.Any(x => x.Role == "Admin"))
        {
            var password = config["Admin:Password"];
            if (string.IsNullOrWhiteSpace(password)) { if (!env.IsDevelopment()) throw new InvalidOperationException("Set Admin__Password before first production startup."); password = "Dooja.Dev!2026"; }
            var admin = new AppUser { Phone = "07700000000", Name = "مدير ضوجة", Role = "Admin" };
            admin.PasswordHash = new PasswordHasher<AppUser>().HashPassword(admin, password); db.Users.Add(admin); await db.SaveChangesAsync();
        }
        if (!env.IsDevelopment() || !config.GetValue("Seed:Demo", false)) return;
        if (db.Places.Any()) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var customer = new AppUser { Phone = "07700000001", Name = "حسين علي", Area = "المنصور", Birth = "12 / 05 / 2001", Points = 1240 };
        var owner = new AppUser { Phone = "07700000002", Name = "أبو علي", Role = "Owner", Area = "المنصور" };
        db.Users.AddRange(customer, owner); await db.SaveChangesAsync();
        db.Places.AddRange(new Place[]{
new Place{Id="p1",ImageKey="padel",Name="ملعب بادل لاونج",Type="بادل",Area="المنصور",Dist="1.5 كم",Rating=4.8,ReviewCount=312,Price=20000,Unit="ساعة",Kind="slots",Category="بادل",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p2",ImageKey="football",Name="ملعب الشعب الخماسي",Type="ملاعب",Area="الشعب",Dist="4.2 كم",Rating=4.5,ReviewCount=188,Price=30000,Unit="ساعة",Kind="open",Category="ملاعب",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p3",ImageKey="billiard",Name="قصر البليارد",Type="بليارد",Area="الكرادة",Dist="2.5 كم",Rating=4.6,ReviewCount=140,Price=8000,Unit="ساعة",Kind="full",Category="بليارد",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p4",ImageKey="gaming",Name="Game Zone",Type="ألعاب",Area="زيونة",Dist="3.1 كم",Rating=4.7,ReviewCount=265,Price=5000,Unit="ساعة",Kind="open",Category="ألعاب إلكترونية",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p5",ImageKey="pool",Name="منتجع الريم",Type="منتجعات",Area="الجادرية",Dist="6 كم",Rating=4.9,ReviewCount=540,Price=25000,Unit="شخص",Kind="open",Category="منتزهات ومنتجعات",Featured=true,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p6",ImageKey="cafe",Name="كافيه ليالي",Type="كافيه",Area="عرصات الهندية",Dist="3.8 كم",Rating=4.7,ReviewCount=410,Price=6000,Unit="شخص",Kind="open",Category="كافيهات",Featured=true,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p7",ImageKey="food",Name="مطعم بيت بغداد",Type="مطاعم",Area="الكرادة",Dist="2.9 كم",Rating=4.6,ReviewCount=820,Price=18000,Unit="شخص",Kind="open",Category="مطاعم",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p8",ImageKey="padel",Name="بادل كلوب الجادرية",Type="بادل",Area="الجادرية",Dist="3.4 كم",Rating=4.7,ReviewCount=96,Price=25000,Unit="ساعة",Kind="open",Category="بادل",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p9",ImageKey="football",Name="نادي الزوراء للبادل",Type="بادل",Area="الحارثية",Dist="5.0 كم",Rating=4.4,ReviewCount=61,Price=18000,Unit="ساعة",Kind="slots",Category="بادل",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p10",ImageKey="pool",Name="مسبح الأمواج",Type="مسابح",Area="اليرموك",Dist="4.6 كم",Rating=4.5,ReviewCount=133,Price=10000,Unit="شخص",Kind="open",Category="مسابح",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p11",ImageKey="friends",Name="مهرجان ليالي بغداد",Type="فعاليات",Area="الزوراء",Dist="5.5 كم",Rating=4.8,ReviewCount=77,Price=15000,Unit="تذكرة",Kind="slots",Category="فعاليات",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400},
new Place{Id="p12",ImageKey="gaming",Name="كيدز لاند",Type="أنشطة عائلية",Area="المنصور",Dist="1.9 كم",Rating=4.6,ReviewCount=58,Price=7000,Unit="طفل",Kind="open",Category="أنشطة عائلية",Featured=false,OwnerUserId=2,Phone="07705551234",Followers=2400}
 });
        foreach (var p in db.ChangeTracker.Entries<Place>()) p.Entity.OwnerUserId = owner.Id;
        await db.SaveChangesAsync();
        db.Offers.Add(new Offer { Id = "o1", PlaceId = "p2", ImageKey = "football", Title = "ملعب الشعب الخماسي", Disc = "خصم 25%", NewPrice = 30000, OldPrice = 40000, ExpiryText = "ينتهي 5 تشرين الثاني", Category = "ملاعب", Area = "الشعب", Active = true });
        db.Offers.Add(new Offer { Id = "o2", PlaceId = "p7", ImageKey = "food", Title = "مطعم بيت بغداد", Disc = "عرض 1+1", NewPrice = 18000, OldPrice = 36000, ExpiryText = "ينتهي بعد 3 أيام", Category = "مطاعم", Area = "الكرادة", Active = true });
        db.Offers.Add(new Offer { Id = "o3", PlaceId = "p4", ImageKey = "gaming", Title = "Game Zone", Disc = "ساعة مجانية", NewPrice = 15000, OldPrice = 20000, ExpiryText = "كل خميس", Category = "ألعاب", Area = "زيونة", Active = true });
        db.Offers.Add(new Offer { Id = "m1", PlaceId = "p1", ImageKey = "padel", Title = "خصم 25% بعد 12 بالليل", Disc = "فعّال", NewPrice = 15000, OldPrice = 20000, ExpiryText = "ينتهي بعد 4 أيام", Category = "بادل", Area = "المنصور", Views = 2140, Clicks = 310, Books = 42, Active = true });
        db.Offers.Add(new Offer { Id = "m2", PlaceId = "p1", ImageKey = "friends", Title = "باقة الأصحاب", Disc = "فعّال", NewPrice = 30000, OldPrice = 35000, ExpiryText = "ينتهي بعد 9 أيام", Category = "بادل", Area = "المنصور", Views = 980, Clicks = 120, Books = 11, Active = true });
        db.Offers.Add(new Offer { Id = "m3", PlaceId = "p1", ImageKey = "food", Title = "ساعة مجانية للطلاب", Disc = "متوقف", NewPrice = 0, OldPrice = 20000, ExpiryText = "وقفته يوم الخميس", Category = "بادل", Area = "المنصور", Views = 640, Clicks = 58, Books = 6, Active = false });
        db.Bookings.Add(new Booking { Code = "DJ-4821", PlaceId = "p1", UserId = customer.Id, Day = new DateOnly(2026, 10, 31), Time = "20:00", Duration = "ساعتين", Hours = 2, People = 4, Total = 31500, PayMethod = "كاش بالمكان", Status = "مؤكد", Rated = false });
        db.Bookings.Add(new Booking { Code = "DJ-4790", PlaceId = "p3", UserId = customer.Id, Day = new DateOnly(2026, 11, 1), Time = "21:00", Duration = "ساعتين", Hours = 2, People = 4, Total = 31500, PayMethod = "كاش بالمكان", Status = "بالانتظار", Rated = false });
        db.Bookings.Add(new Booking { Code = "DJ-4802", PlaceId = "p5", UserId = customer.Id, Day = new DateOnly(2026, 11, 6), Time = "10:00", Duration = "3 ساعات", Hours = 3, People = 4, Total = 31500, PayMethod = "زين كاش", Status = "بانتظار القبول", Rated = false });
        db.Bookings.Add(new Booking { Code = "DJ-4610", PlaceId = "p4", UserId = customer.Id, Day = new DateOnly(2026, 10, 22), Time = "19:00", Duration = "ساعة", Hours = 1, People = 3, Total = 15000, PayMethod = "كاش بالمكان", Status = "مكتمل", Rated = false });
        db.Bookings.Add(new Booking { Code = "DJ-4555", PlaceId = "p2", UserId = customer.Id, Day = new DateOnly(2026, 10, 16), Time = "18:00", Duration = "ساعتين", Hours = 2, People = 10, Total = 60000, PayMethod = "بطاقة", Status = "مكتمل", Rated = true });
        db.Reviews.Add(new Review { PlaceId = "p1", Name = "علي حسين", Ago = "قبل يومين", Stars = 5, Text = "الأرضية ممتازة والإضاءة قوية، الحجز من التطبيق كان سهل جداً.", HasPhotos = true, Reply = "تسلم علي، ننتظرك مرة لخ!" });
        db.Reviews.Add(new Review { PlaceId = "p1", Name = "زينب كريم", Ago = "قبل أسبوع", Stars = 4, Text = "مكان مرتب، بس الموقف يزدحم بالليل. الأسعار مناسبة.", HasPhotos = false, Reply = null });
        db.Reviews.Add(new Review { PlaceId = "p1", Name = "سجاد فلاح", Ago = "قبل 3 أيام", Stars = 5, Text = "تجربة حلوة والتعامل راقي، أكيد نرجع.", HasPhotos = false, Reply = null });
        db.Reviews.Add(new Review { PlaceId = "p1", Name = "نور الهدى", Ago = "قبل أسبوع", Stars = 4, Text = "المكان نظيف ومرتب، بس الزحمة بالويكند.", HasPhotos = false, Reply = null });
        db.Reviews.Add(new Review { PlaceId = "p1", Name = "علي حسين", Ago = "قبل يومين", Stars = 3, Text = "المكان حلو بس الإضاءة بالملعب الثاني ضعيفة شوية.", HasPhotos = false, Reply = null });
        db.Reviews.Add(new Review { PlaceId = "p1", Name = "سارة محمد", Ago = "قبل يومين", Stars = 5, Text = "أفضل ملعب بادل ببغداد، الحجز كان سهل جداً", HasPhotos = false, Reply = "تسلمين سارة، ننتظرك مرة ثانية" });

        db.Favorites.AddRange(new Favorite { UserId = customer.Id, PlaceId = "p1" }, new Favorite { UserId = customer.Id, PlaceId = "p5" });
        db.Follows.AddRange(new Follow { UserId = customer.Id, PlaceId = "p4" }, new Follow { UserId = customer.Id, PlaceId = "p6" });
        db.OwnerServices.AddRange(new OwnerService { PlaceId = "p1", Name = "ساعة ملعب", Price = 20000, Unit = "ساعة" }, new OwnerService { PlaceId = "p1", Name = "باقة ساعتين", Price = 35000, Unit = "باقة" }, new OwnerService { PlaceId = "p1", Name = "تأجير مضارب", Price = 3000, Unit = "مضرب" });
        db.ClosedSlots.Add(new ClosedSlot { PlaceId = "p1", Day = Dooja.Web.Services.BookingService.Today, Time = "22:00" });
        var reelp1 = new Reel { ImageKey = "reel", PlaceId = "p1", Caption = "سهرة بادل بالمنصور، الإضاءة الجديدة تجنن", OfferText = "خصم 10% قبل 9 بالليل", Likes = 1200 }; db.Reels.Add(reelp1); await db.SaveChangesAsync();
        db.ReelComments.Add(new ReelComment { ReelId = reelp1.Id, Name = "حيدر", Text = "وين بالضبط؟ قرب الساعة؟", Likes = 12 });
        db.ReelComments.Add(new ReelComment { ReelId = reelp1.Id, Name = "مريم", Text = "يكدرون بنات يحجزون بأوقات خاصة؟", Likes = 8 });
        db.ReelComments.Add(new ReelComment { ReelId = reelp1.Id, Name = "ملعب بادل لاونج", Text = "اي عدنا أوقات عائلية كل جمعة الصبح", Likes = 20 });
        var reelp4 = new Reel { ImageKey = "gaming", PlaceId = "p4", Caption = "بطولة FIFA كل خميس، الجائزة 100 ألف", OfferText = "ساعة مجانية كل خميس", Likes = 8100 }; db.Reels.Add(reelp4); await db.SaveChangesAsync();
        db.ReelComments.Add(new ReelComment { ReelId = reelp4.Id, Name = "كرار", Text = "شلون أسجل بالبطولة؟", Likes = 4 });
        var reelp6 = new Reel { ImageKey = "cafe", PlaceId = "p6", Caption = "جلسة مسائية على الشط ويا أطيب شاي", OfferText = "خصم 15% للطلاب", Likes = 5600 }; db.Reels.Add(reelp6); await db.SaveChangesAsync();
        db.ReelComments.Add(new ReelComment { ReelId = reelp6.Id, Name = "سارة", Text = "المكان يجنن 😍", Likes = 9 });
        var requestUsers = new[] { new AppUser { Phone = "07700000003", Name = "مصطفى كريم" }, new AppUser { Phone = "07700000004", Name = "علي ناظم" }, new AppUser { Phone = "07700000005", Name = "زيد حيدر" }, new AppUser { Phone = "07700000006", Name = "عباس محمد" } }; db.Users.AddRange(requestUsers); await db.SaveChangesAsync();
        db.Bookings.Add(new Booking { Code = "DJ-OWNER-1", PlaceId = "p1", UserId = customer.Id, Day = Dooja.Web.Services.BookingService.Today, Time = "20:00", Duration = "السبت 8:00 م · ساعتين · 4 أشخاص", People = 4, Total = 31500, PayMethod = "زين كاش · مدفوع", Status = "بانتظار القبول" });
        db.Bookings.Add(new Booking { Code = "DJ-OWNER-2", PlaceId = "p1", UserId = requestUsers[0].Id, Day = Dooja.Web.Services.BookingService.Today.AddDays(1), Time = "21:00", Duration = "الأحد 10:00 م · ساعة", People = 4, Total = 20000, PayMethod = "نقداً بالمكان", Status = "بانتظار القبول" });
        db.Bookings.Add(new Booking { Code = "DJ-OWNER-3", PlaceId = "p1", UserId = requestUsers[1].Id, Day = Dooja.Web.Services.BookingService.Today.AddDays(2), Time = "22:00", Duration = "الاثنين 9:00 م · ساعتين · 6 أشخاص", People = 4, Total = 35000, PayMethod = "نقداً بالمكان", Status = "بانتظار القبول" });
        db.Bookings.Add(new Booking { Code = "DJ-OWNER-4", PlaceId = "p1", UserId = requestUsers[2].Id, Day = Dooja.Web.Services.BookingService.Today.AddDays(3), Time = "23:00", Duration = "الأحد 11:00 م · ساعة", People = 4, Total = 20000, PayMethod = "بطاقة · مدفوع", Status = "مؤكد" });
        db.Bookings.Add(new Booking { Code = "DJ-OWNER-5", PlaceId = "p1", UserId = requestUsers[3].Id, Day = Dooja.Web.Services.BookingService.Today.AddDays(4), Time = "20:00", Duration = "الخميس 7:00 م · ساعتين", People = 4, Total = 35000, PayMethod = "نقداً بالمكان", Status = "مكتمل" });
        db.Notifications.Add(new Notification { UserId = customer.Id, Icon = "calendar-check", Title = "تم قبول حجزك", Body = "ملعب بادل لاونج · السبت 8:00 م", Fresh = true });
        db.Notifications.Add(new Notification { UserId = customer.Id, Icon = "hourglass", Title = "صار عندك مكان", Body = "انلغى حجز بقصر البليارد، أكّد خلال 10 دقايق", Fresh = true });
        db.Notifications.Add(new Notification { UserId = customer.Id, Icon = "badge-percent", Title = "عرض جديد من Game Zone", Body = "ساعة مجانية كل خميس", Fresh = false });
        db.Notifications.Add(new Notification { UserId = customer.Id, Icon = "trophy", Title = "قربت تخلص التحدي", Body = "باقي حجز واحد وتاخذ 150 نقطة", Fresh = false });
        db.Notifications.Add(new Notification { UserId = customer.Id, Icon = "message-circle", Title = "رد على تقييمك", Body = "ملعب بادل لاونج رد على تقييمك", Fresh = false });
        db.Notifications.Add(new Notification { UserId = owner.Id, Icon = "calendar-check", Title = "طلب حجز جديد", Body = "حسين علي · السبت 8:00 م · 4 أشخاص", Fresh = true });
        db.Notifications.Add(new Notification { UserId = owner.Id, Icon = "star", Title = "تقييم جديد 3 نجوم", Body = "علي حسين كتب تقييم، رد عليه", Fresh = true });
        db.Notifications.Add(new Notification { UserId = owner.Id, Icon = "circle-check", Title = "تفعّل مشروعك", Body = "ملعب بادل لاونج صار يظهر للناس", Fresh = false });
        db.Notifications.Add(new Notification { UserId = owner.Id, Icon = "trending-up", Title = "عرضك ماشي زين", Body = "خصم 25% جاب 42 حجز هالأسبوع", Fresh = false });
        db.Notifications.Add(new Notification { UserId = owner.Id, Icon = "hourglass", Title = "العرض قرب يخلص", Body = "باقة الأصحاب تنتهي بعد يومين", Fresh = false });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
