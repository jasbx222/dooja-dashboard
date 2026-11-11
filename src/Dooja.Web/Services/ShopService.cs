using System.Text.RegularExpressions;
using Dooja.Web.Data;
using Dooja.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Dooja.Web.Services;

public class ShopService(DoojaDb db, ILogger<ShopService> log, IConfiguration config)
{
    static readonly Regex Phone = new(@"^07\d{9}$", RegexOptions.Compiled);
    bool OnlineConfigured => !string.IsNullOrWhiteSpace(config["Payment:OnlineProvider"]);

    public async Task EnsureDefaults()
    {
        if (!await db.StoreSettings.AnyAsync())
            db.StoreSettings.Add(new StoreSetting { Id = 1, Enabled = true, DeliveryFee = 0, MinOrder = 0, LowStock = 3, CancelStatuses = ShopOrderStates.Pending });
        if (!await db.PaymentMethods.AnyAsync(x => x.Code == "cod"))
            db.PaymentMethods.Add(new PaymentMethod { Code = "cod", Name = "الدفع عند الاستلام", Enabled = true, IsOnline = false });
        await db.SaveChangesAsync();
    }

    public Task<StoreSetting> Settings() => db.StoreSettings.AsNoTracking().FirstAsync(x => x.Id == 1);

    public async Task<object> PublicSettings()
    {
        var s = await Settings();
        var methods = await db.PaymentMethods.AsNoTracking().Where(x => x.Enabled && !x.IsOnline).OrderBy(x => x.Id).Select(x => new { x.Code, x.Name }).ToListAsync();
        var areas = await db.DeliveryAreas.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Governorate).ThenBy(x => x.Area)
            .Select(x => new { x.Id, x.Governorate, x.Area, x.Fee }).ToListAsync();
        return new { enabled = s.Enabled, deliveryFee = s.DeliveryFee, minOrder = s.MinOrder, paymentMethods = methods, areas };
    }

    public async Task<List<object>> Categories() => await db.ProductCategories.AsNoTracking()
        .Where(x => x.Active && !x.Archived).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
        .Select(x => new { x.Id, x.Name, imageUrl = x.ImagePath, x.SortOrder }).Cast<object>().ToListAsync();

    public async Task<object> Products(string? q, int? categoryId, long? minPrice, long? maxPrice, string? sort, bool? discounted, int page, int pageSize)
    {
        var settings = await Settings();
        page = Math.Clamp(page, 1, 10000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        if (!settings.Enabled) return new { enabled = false, page, pageSize, total = 0, items = Array.Empty<object>() };
        var query = db.Products.AsNoTracking().Include(x => x.Images).Include(x => x.Category)
            .Where(x => x.Active && !x.Archived && x.Category != null && x.Category.Active && !x.Category.Archived);
        if (categoryId is int cid) query = query.Where(x => x.CategoryId == cid);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q));
        if (minPrice is long min) query = query.Where(x => x.Price >= min);
        if (maxPrice is long max) query = query.Where(x => x.Price <= max);
        if (discounted == true) query = query.Where(x => x.CompareAtPrice != null && x.CompareAtPrice > x.Price);
        query = sort switch
        {
            "price_asc" => query.OrderBy(x => x.Price).ThenByDescending(x => x.Id),
            "price_desc" => query.OrderByDescending(x => x.Price).ThenByDescending(x => x.Id),
            "discount" => query.Where(x => x.CompareAtPrice != null && x.CompareAtPrice > x.Price).OrderByDescending(x => x.CompareAtPrice),
            _ => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };
        var total = await query.CountAsync();
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { enabled = true, page, pageSize, total, items = rows.Select(Card) };
    }

    public async Task<object?> Product(int id)
    {
        var settings = await Settings();
        if (!settings.Enabled) return null;
        var p = await db.Products.AsNoTracking().Include(x => x.Images).Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id && x.Active && !x.Archived && x.Category != null && x.Category.Active && !x.Category.Archived);
        return p == null ? null : Detail(p);
    }

    public async Task<(object? Cart, string? Error)> Add(int userId, int productId, int qty)
    {
        if (qty < 1 || qty > 99) return (null, "الكمية غير صالحة");
        var (cart, error) = await Mutate(userId, productId, current => current + qty);
        return (cart, error);
    }

    public Task<(object? Cart, string? Error)> SetQty(int userId, int productId, int qty) =>
        qty == 0 ? Remove(userId, productId) : Mutate(userId, productId, _ => qty);

    public async Task<(object? Cart, string? Error)> Remove(int userId, int productId)
    {
        var cart = await LoadCart(userId, track: true);
        var item = cart.Items.FirstOrDefault(x => x.ProductId == productId);
        if (item != null) db.CartItems.Remove(item);
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (await CartView(userId), null);
    }

    public async Task<object> Merge(int userId, IReadOnlyList<(int ProductId, int Qty)> items)
    {
        var warnings = new List<string>();
        foreach (var (productId, qty) in items.Where(x => x.Qty > 0).Take(50))
        {
            var (_, error) = await Mutate(userId, productId, current => Math.Min(99, current + qty));
            if (error != null) warnings.Add(error);
        }
        var view = await CartView(userId);
        return new { cart = view, warnings };
    }

    public async Task<object> CartView(int userId)
    {
        var cart = await LoadCart(userId, track: true);
        var warnings = new List<string>();
        long subtotal = 0, discount = 0;
        var lines = new List<CartLine>();
        foreach (var item in cart.Items.OrderBy(x => x.Id))
        {
            var p = item.Product;
            if (p == null || p.Archived || !p.Active) { warnings.Add("منتج في السلة لم يعد متاحاً"); continue; }
            var changed = item.LastSeenPrice != 0 && item.LastSeenPrice != p.Price;
            if (changed) warnings.Add($"تغير سعر {p.Name}");
            if (item.Qty > p.Stock) warnings.Add($"الكمية المطلوبة من {p.Name} أكبر من المتوفر ({p.Stock})");
            item.LastSeenPrice = p.Price;
            var line = p.Price * item.Qty;
            subtotal += line;
            if (p.CompareAtPrice is long was && was > p.Price) discount += (was - p.Price) * item.Qty;
            lines.Add(new CartLine(item.Id, p.Id, p.Name, p.Images.OrderBy(i => i.SortOrder).Select(i => i.Path).FirstOrDefault(), item.Qty, p.Price, line, p.Stock, changed, p.Stock > 0));
        }
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new { items = lines, subtotal, discount, count = lines.Sum(x => x.Qty), warnings };
    }

    public async Task<(object? Order, string? Error, bool Replayed)> Checkout(int userId, CheckoutInput input)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try { return await CheckoutOnce(userId, input); }
            catch (Exception ex) when (IsBusy(ex) && attempt < 3)
            {
                log.LogWarning(ex, "Shop checkout busy for user {UserId}, attempt {Attempt}", userId, attempt);
                db.ChangeTracker.Clear();
                await Task.Delay(40 * attempt);
            }
        }
        return (null, "الخادم مشغول، أعد المحاولة", false);
    }

    async Task<(object? Order, string? Error, bool Replayed)> CheckoutOnce(int userId, CheckoutInput input)
    {
        var key = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey.Trim();
        if (key != null && (key.Length < 8 || key.Length > 80)) return (null, "مفتاح منع التكرار غير صالح", false);
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100) return (null, "اكتب اسم المستلم", false);
        if (!Phone.IsMatch(input.Phone ?? "")) return (null, "رقم الهاتف غير صحيح", false);
        if (string.IsNullOrWhiteSpace(input.Address) || input.Address.Trim().Length > 300) return (null, "اكتب عنوان التوصيل", false);
        if (string.IsNullOrWhiteSpace(input.Governorate) || string.IsNullOrWhiteSpace(input.Area)) return (null, "اختر المحافظة والمنطقة", false);
        if ((input.Notes ?? "").Length > 500) return (null, "الملاحظات طويلة", false);

        await using var tx = await db.Database.BeginTransactionAsync();
        if (key != null)
        {
            var prior = await db.ShopOrders.Include(x => x.Items).Include(x => x.Events).FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key);
            if (prior != null) return (OrderDto(prior), null, true);
        }
        var settings = await db.StoreSettings.FirstAsync(x => x.Id == 1);
        if (!settings.Enabled) return (null, "المتجر متوقف حالياً", false);
        var pay = await db.PaymentMethods.FirstOrDefaultAsync(x => x.Code == input.PayMethod && x.Enabled);
        if (pay == null) return (null, "طريقة الدفع غير متاحة", false);
        if (pay.IsOnline && !OnlineConfigured) return (null, "الدفع الإلكتروني غير مربوط بمزوّد حقيقي", false);
        var areas = await db.DeliveryAreas.Where(x => x.Active).ToListAsync();
        DeliveryArea? area = null;
        if (areas.Count > 0)
        {
            area = areas.FirstOrDefault(x => x.Governorate == input.Governorate.Trim() && x.Area == input.Area.Trim());
            if (area == null) return (null, "منطقة التوصيل غير متاحة", false);
        }
        var cart = await LoadCart(userId, track: true);
        var wanted = cart.Items.Where(x => x.Qty > 0).OrderBy(x => x.ProductId).ToList();
        if (wanted.Count == 0) return (null, "السلة فارغة", false);

        var lines = new List<(int ProductId, string Name, long Price, long? Compare, int Qty, int StockAfter)>();
        foreach (var item in wanted)
        {
            var rows = await Decrement(item.ProductId, item.Qty);
            if (rows != 1)
            {
                var name = item.Product?.Name ?? "منتج";
                return (null, $"الكمية المطلوبة من {name} غير متوفرة", false);
            }
            var snap = await db.Products.AsNoTracking().Where(x => x.Id == item.ProductId).Select(x => new { x.Name, x.Price, x.CompareAtPrice, x.Stock, x.Active, x.Archived }).FirstAsync();
            if (!snap.Active || snap.Archived) return (null, "أحد المنتجات لم يعد متاحاً", false);
            lines.Add((item.ProductId, snap.Name, snap.Price, snap.CompareAtPrice, item.Qty, snap.Stock));
        }
        long subtotal = 0, discount = 0;
        foreach (var line in lines)
        {
            subtotal = checked(subtotal + line.Price * line.Qty);
            if (line.Compare is long was && was > line.Price) discount = checked(discount + (was - line.Price) * line.Qty);
        }
        if (settings.MinOrder > 0 && subtotal < settings.MinOrder) return (null, $"الحد الأدنى للطلب {settings.MinOrder} د.ع", false);
        var fee = area?.Fee ?? settings.DeliveryFee;
        var total = checked(subtotal + fee);
        var order = new ShopOrder
        {
            Number = await NextNumber(),
            UserId = userId,
            Status = ShopOrderStates.Pending,
            CustomerName = input.Name.Trim(),
            Phone = (input.Phone ?? "").Trim(),
            Governorate = input.Governorate.Trim(),
            Area = input.Area.Trim(),
            Address = input.Address.Trim(),
            Notes = (input.Notes ?? "").Trim(),
            Subtotal = subtotal,
            Discount = discount,
            DeliveryFee = fee,
            Total = total,
            PayMethod = pay.Name,
            IdempotencyKey = key
        };
        order.Items = lines.Select(x => new ShopOrderItem { ProductId = x.ProductId, Name = x.Name, UnitPrice = x.Price, Qty = x.Qty, LineTotal = checked(x.Price * x.Qty) }).ToList();
        order.Events.Add(new ShopOrderEvent { Status = order.Status, Note = "تم إنشاء الطلب", Actor = "الزبون" });
        db.ShopOrders.Add(order);
        foreach (var line in lines)
            db.InventoryTransactions.Add(new InventoryTransaction { ProductId = line.ProductId, Delta = -line.Qty, StockAfter = line.StockAfter, Reason = "بيع", Note = order.Number, ActorUserId = userId });
        db.CartItems.RemoveRange(cart.Items);
        cart.UpdatedAt = DateTime.UtcNow;
        var admins = await db.Users.Where(x => x.Role == "Admin" && x.Active).Select(x => x.Id).ToListAsync();
        db.Notifications.Add(new Notification { UserId = userId, Icon = "store", Title = "استلمنا طلبك", Body = order.Number });
        foreach (var admin in admins) db.Notifications.Add(new Notification { UserId = admin, Icon = "store", Title = "طلب متجر جديد", Body = order.Number, Fresh = true });
        try
        {
            await db.SaveChangesAsync();
            foreach (var txRow in db.ChangeTracker.Entries<InventoryTransaction>().Where(x => x.Entity.Note == order.Number)) txRow.Entity.OrderId = order.Id;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            log.LogWarning(ex, "Shop checkout conflict for user {UserId}", userId);
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            if (key != null)
            {
                var prior = await db.ShopOrders.AsNoTracking().Include(x => x.Items).Include(x => x.Events).FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key);
                if (prior != null) return (OrderDto(prior), null, true);
            }
            return (null, "تعذر إكمال الطلب، أعد المحاولة", false);
        }
        log.LogInformation("Shop order {Number} created for user {UserId} total {Total}", order.Number, userId, order.Total);
        return (OrderDto(order), null, false);
    }

    public async Task<List<object>> MyOrders(int userId) => (await db.ShopOrders.AsNoTracking().Include(x => x.Items).Include(x => x.Events)
        .Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync()).Select(OrderDto).ToList();

    public async Task<object?> MyOrder(int userId, int id)
    {
        var order = await db.ShopOrders.AsNoTracking().Include(x => x.Items).Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        return order == null ? null : OrderDto(order);
    }

    public async Task<(object? Order, string? Error)> CancelMine(int userId, int id)
    {
        var settings = await Settings();
        var allowed = settings.CancelStatuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return await SetStatus(id, ShopOrderStates.Cancelled, "ألغى الزبون الطلب", "الزبون", userId, onlyUser: userId, allowedFrom: allowed);
    }

    public Task<(object? Order, string? Error)> AdminStatus(int id, string status, string note, string actor, int actorId) =>
        SetStatus(id, status, note, actor, actorId, onlyUser: null, allowedFrom: null);

    public async Task<string?> AdjustStock(int productId, int delta, string note, int actorId)
    {
        if (delta == 0 || string.IsNullOrWhiteSpace(note)) return "اكتب سبب التعديل وكمية مختلفة عن صفر";
        await using var tx = await db.Database.BeginTransactionAsync();
        var rows = delta > 0
            ? await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"Stock\" = \"Stock\" + {delta} WHERE \"Id\" = {productId} AND \"Archived\" = {false}")
            : await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"Stock\" = \"Stock\" + {delta} WHERE \"Id\" = {productId} AND \"Archived\" = {false} AND \"Stock\" >= {-delta}");
        if (rows != 1) return "لا يمكن إنقاص المخزون تحت الصفر";
        var stock = await db.Products.AsNoTracking().Where(x => x.Id == productId).Select(x => x.Stock).FirstAsync();
        db.InventoryTransactions.Add(new InventoryTransaction { ProductId = productId, Delta = delta, StockAfter = stock, Reason = "تعديل يدوي", Note = note.Trim(), ActorUserId = actorId });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return null;
    }

    async Task<(object? Order, string? Error)> SetStatus(int id, string status, string note, string actor, int actorId, int? onlyUser, string[]? allowedFrom)
    {
        if (!ShopOrderStates.All.Contains(status)) return (null, "حالة غير معروفة");
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = await db.ShopOrders.Include(x => x.Items).Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == id && (onlyUser == null || x.UserId == onlyUser));
        if (order == null) return (null, "الطلب غير موجود");
        if (order.Status == status) return (OrderDto(order), null);
        if (onlyUser != null)
        {
            if (status != ShopOrderStates.Cancelled || allowedFrom == null || !allowedFrom.Contains(order.Status)) return (null, "لا يمكن إلغاء الطلب في هذه الحالة");
        }
        else if (!ShopOrderStates.CanAdmin(order.Status, status)) return (null, "لا يمكن نقل الطلب إلى هذه الحالة");
        var previous = order.Status;
        order.Status = status;
        order.Events.Add(new ShopOrderEvent { Status = status, Note = string.IsNullOrWhiteSpace(note) ? status : note.Trim(), Actor = actor });
        if (status == ShopOrderStates.Cancelled && previous != ShopOrderStates.Cancelled)
        {
            foreach (var item in order.Items.Where(x => x.ProductId != null))
            {
                var pid = item.ProductId!.Value;
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"Stock\" = \"Stock\" + {item.Qty} WHERE \"Id\" = {pid}");
                var stock = await db.Products.AsNoTracking().Where(x => x.Id == pid).Select(x => (int?)x.Stock).FirstOrDefaultAsync() ?? item.Qty;
                db.InventoryTransactions.Add(new InventoryTransaction { ProductId = pid, Delta = item.Qty, StockAfter = stock, Reason = "إلغاء طلب", OrderId = order.Id, Note = order.Number, ActorUserId = actorId });
            }
        }
        db.Notifications.Add(new Notification { UserId = order.UserId, Icon = "store", Title = "تحديث طلب " + order.Number, Body = status });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        log.LogInformation("Shop order {Number} status {Status} by {Actor}", order.Number, status, actor);
        return (OrderDto(order), null);
    }

    async Task<(object? Cart, string? Error)> Mutate(int userId, int productId, Func<int, int> nextQty)
    {
        var settings = await Settings();
        if (!settings.Enabled) return (null, "المتجر متوقف حالياً");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId && x.Active && !x.Archived);
        if (product == null) return (null, "المنتج غير متاح");
        var cart = await LoadCart(userId, track: true);
        var item = cart.Items.FirstOrDefault(x => x.ProductId == productId);
        var qty = nextQty(item?.Qty ?? 0);
        if (qty < 1 || qty > 99) return (null, "الكمية غير صالحة");
        if (qty > product.Stock) return (null, $"المتوفر من {product.Name} هو {product.Stock} فقط");
        if (item == null)
        {
            if (cart.Items.Count >= 50) return (null, "السلة ممتلئة");
            item = new CartItem { ProductId = productId, Qty = qty, LastSeenPrice = product.Price };
            cart.Items.Add(item);
        }
        else
        {
            if (item.LastSeenPrice != 0 && item.LastSeenPrice != product.Price) item.LastSeenPrice = product.Price;
            item.Qty = qty;
        }
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (await CartView(userId), null);
    }

    async Task<Cart> LoadCart(int userId, bool track)
    {
        var query = track ? db.Carts.Include(x => x.Items).ThenInclude(x => x.Product!).ThenInclude(p => p.Images) : db.Carts.AsNoTracking();
        var cart = await query.FirstOrDefaultAsync(x => x.UserId == userId);
        if (cart != null) return cart;
        cart = new Cart { UserId = userId };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        return cart;
    }

    Task<int> Decrement(int productId, int qty) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"Stock\" = \"Stock\" - {qty} WHERE \"Id\" = {productId} AND \"Stock\" >= {qty} AND \"Active\" = {true} AND \"Archived\" = {false}");

    async Task<string> NextNumber()
    {
        for (var i = 0; i < 5; i++)
        {
            var number = "SH-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            if (!await db.ShopOrders.AnyAsync(x => x.Number == number)) return number;
        }
        return "SH-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    }

    static bool IsBusy(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("locked", StringComparison.OrdinalIgnoreCase) || text.Contains("busy", StringComparison.OrdinalIgnoreCase) || text.Contains("40001", StringComparison.Ordinal);
    }

    public static object Card(Product p) => new
    {
        p.Id, p.Name, p.Price, compareAtPrice = p.CompareAtPrice, discountPercent = Discount(p), p.Stock,
        available = p.Stock > 0, p.Featured, categoryId = p.CategoryId, category = p.Category?.Name,
        imageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Path).FirstOrDefault()
    };

    public static object Detail(Product p) => new
    {
        p.Id, p.Name, p.Description, p.Specs, p.Price, compareAtPrice = p.CompareAtPrice, discountPercent = Discount(p), p.Stock,
        available = p.Stock > 0, p.Featured, categoryId = p.CategoryId, category = p.Category?.Name,
        imageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Path).FirstOrDefault(),
        images = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Path).ToList()
    };

    public static object OrderDto(ShopOrder o) => new
    {
        o.Id, o.Number, o.Status, createdAt = o.CreatedAt, o.CustomerName, o.Phone, o.Governorate, o.Area, o.Address, o.Notes,
        o.Subtotal, o.Discount, o.DeliveryFee, o.Total, payMethod = o.PayMethod,
        items = o.Items.Select(i => new { i.ProductId, i.Name, i.UnitPrice, i.Qty, i.LineTotal }),
        history = o.Events.OrderBy(e => e.Id).Select(e => new { e.Status, e.Note, e.Actor, e.CreatedAt })
    };

    public static int? Discount(Product p) => p.CompareAtPrice is long was && was > p.Price ? (int)((was - p.Price) * 100 / was) : null;
}

public record CheckoutInput(string Name, string Phone, string Governorate, string Area, string Address, string? Notes, string PayMethod, string? IdempotencyKey);
public record CartLine(int Id, int ProductId, string Name, string? ImageUrl, int Qty, long UnitPrice, long LineTotal, int Stock, bool PriceChanged, bool Available);
