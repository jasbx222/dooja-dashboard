using System.Security.Claims;
using Dooja.Web.Data;
using Dooja.Web.Models;
using Dooja.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Dooja.Web.Controllers;

public class ShopProductsController(DoojaDb db, IWebHostEnvironment env) : DashboardControllerBase
{
    public async Task<IActionResult> Index(string? q)
    {
        ViewBag.Q = q;
        var query = db.Products.AsNoTracking().Include(x => x.Category).Include(x => x.Images).Where(x => !x.Archived);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q));
        return View(await query.OrderByDescending(x => x.Id).Take(300).ToListAsync());
    }
    public async Task<IActionResult> Create() { await Categories(); return View("Edit", new Product { Active = true }); }
    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.Products.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        await Categories();
        return View(p);
    }
    [HttpPost]
    public async Task<IActionResult> Save([Bind("Id,CategoryId,Name,Description,Specs,Price,CompareAtPrice,Stock,Active,Featured")] Product input)
    {
        if (!await db.ProductCategories.AnyAsync(x => x.Id == input.CategoryId && !x.Archived)) ModelState.AddModelError("CategoryId", "اختر تصنيفاً صالحاً");
        if (input.CompareAtPrice is long was && was <= input.Price) ModelState.AddModelError("CompareAtPrice", "سعر قبل الخصم يجب أن يكون أعلى من سعر البيع");
        if (input.Price < 0 || input.Stock < 0) ModelState.AddModelError("", "السعر والمخزون لا يكونان سالبين");
        if (!ModelState.IsValid) { await Categories(); return View("Edit", input); }
        var actor = Actor();
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == input.Id && !x.Archived);
        var previous = product?.Stock ?? 0;
        if (product == null)
        {
            product = new Product();
            db.Products.Add(product);
        }
        product.CategoryId = input.CategoryId;
        product.Name = input.Name.Trim();
        product.Description = input.Description?.Trim() ?? "";
        product.Specs = input.Specs?.Trim() ?? "";
        product.Price = input.Price;
        product.CompareAtPrice = input.CompareAtPrice;
        product.Stock = input.Stock;
        product.Active = input.Active;
        product.Featured = input.Featured;
        var delta = product.Stock - previous;
        if (delta != 0)
            db.InventoryTransactions.Add(new InventoryTransaction { Product = product, Delta = delta, StockAfter = product.Stock, Reason = product.Id == 0 ? "رصيد افتتاحي" : "تعديل من بطاقة المنتج", Note = "حفظ المنتج", ActorUserId = actor });
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ المنتج";
        return RedirectToAction(nameof(Edit), new { id = product.Id });
    }
    [HttpPost]
    public async Task<IActionResult> Upload(int id, List<IFormFile> files)
    {
        var product = await db.Products.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id && !x.Archived);
        if (product == null) return NotFound();
        if (files.Count == 0) { TempData["Error"] = "اختر صورة"; return RedirectToAction(nameof(Edit), new { id }); }
        if (product.Images.Count + files.Count > 8) { TempData["Error"] = "الحد ثماني صور للمنتج"; return RedirectToAction(nameof(Edit), new { id }); }
        var order = product.Images.Select(x => x.SortOrder).DefaultIfEmpty(-1).Max();
        foreach (var file in files)
        {
            var (path, error) = await ImageStore.Save(env, file, "products");
            if (error != null) { TempData["Error"] = error; return RedirectToAction(nameof(Edit), new { id }); }
            product.Images.Add(new ProductImage { Path = path!, SortOrder = ++order });
        }
        await db.SaveChangesAsync();
        TempData["Success"] = "تم رفع الصور";
        return RedirectToAction(nameof(Edit), new { id });
    }
    [HttpPost]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var image = await db.ProductImages.FirstOrDefaultAsync(x => x.Id == id);
        if (image == null) return NotFound();
        ImageStore.Delete(env, image.Path);
        db.ProductImages.Remove(image);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حذف الصورة";
        return RedirectToAction(nameof(Edit), new { id = image.ProductId });
    }
    [HttpPost]
    public async Task<IActionResult> Archive(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound();
        product.Archived = true;
        product.Active = false;
        await db.SaveChangesAsync();
        TempData["Success"] = "تمت أرشفة المنتج. الطلبات السابقة تبقى محفوظة";
        return RedirectToAction(nameof(Index));
    }
    async Task Categories() => ViewBag.Categories = new SelectList(await db.ProductCategories.Where(x => !x.Archived).OrderBy(x => x.SortOrder).ToListAsync(), "Id", "Name");
    int Actor() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public class ShopCategoriesController(DoojaDb db, IWebHostEnvironment env) : DashboardControllerBase
{
    public async Task<IActionResult> Index() => View(await db.ProductCategories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync());
    public IActionResult Create() => View("Edit", new ProductCategory { Active = true });
    public async Task<IActionResult> Edit(int id) { var c = await db.ProductCategories.FindAsync(id); return c == null ? NotFound() : View(c); }
    [HttpPost]
    public async Task<IActionResult> Save([Bind("Id,Name,SortOrder,Active")] ProductCategory input)
    {
        if (!ModelState.IsValid) return View("Edit", input);
        var row = await db.ProductCategories.FirstOrDefaultAsync(x => x.Id == input.Id);
        if (row == null) { row = new ProductCategory(); db.ProductCategories.Add(row); }
        row.Name = input.Name.Trim();
        row.SortOrder = input.SortOrder;
        row.Active = input.Active;
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ التصنيف";
        return RedirectToAction(nameof(Edit), new { id = row.Id });
    }
    [HttpPost]
    public async Task<IActionResult> Upload(int id, IFormFile? file)
    {
        var row = await db.ProductCategories.FindAsync(id);
        if (row == null || file == null) return NotFound();
        var (path, error) = await ImageStore.Save(env, file, "categories");
        if (error != null) { TempData["Error"] = error; return RedirectToAction(nameof(Edit), new { id }); }
        ImageStore.Delete(env, row.ImagePath);
        row.ImagePath = path;
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ صورة التصنيف";
        return RedirectToAction(nameof(Edit), new { id });
    }
    [HttpPost]
    public async Task<IActionResult> Archive(int id)
    {
        var row = await db.ProductCategories.FindAsync(id);
        if (row == null) return NotFound();
        row.Archived = true;
        row.Active = false;
        await db.SaveChangesAsync();
        TempData["Success"] = "تمت أرشفة التصنيف";
        return RedirectToAction(nameof(Index));
    }
}

public class ShopOrdersController(DoojaDb db, ShopService shop) : DashboardControllerBase
{
    public async Task<IActionResult> Index(string? q, string? status, DateOnly? from, DateOnly? to)
    {
        ViewBag.Q = q; ViewBag.Status = status; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        var query = db.ShopOrders.AsNoTracking().Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Number.Contains(q) || x.CustomerName.Contains(q) || x.Phone.Contains(q));
        if (from is DateOnly f) query = query.Where(x => x.CreatedAt >= Baghdad.StartUtc(f));
        if (to is DateOnly t) query = query.Where(x => x.CreatedAt < Baghdad.StartUtc(t.AddDays(1)));
        ViewBag.Statuses = ShopOrderStates.All;
        return View(await query.OrderByDescending(x => x.CreatedAt).Take(300).ToListAsync());
    }
    public async Task<IActionResult> Detail(int id)
    {
        var order = await db.ShopOrders.Include(x => x.Items).Include(x => x.Events).Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (order == null) return NotFound();
        ViewBag.Statuses = ShopOrderStates.All.Where(x => ShopOrderStates.CanAdmin(order.Status, x) && x != order.Status).ToArray();
        return View(order);
    }
    public async Task<IActionResult> Print(int id)
    {
        var order = await db.ShopOrders.AsNoTracking().Include(x => x.Items).Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == id);
        return order == null ? NotFound() : View(order);
    }
    [HttpPost]
    public async Task<IActionResult> Status(int id, string status, string? note)
    {
        var (_, error) = await shop.AdminStatus(id, status, note ?? "", User.Identity?.Name ?? "مدير", Actor());
        TempData[error == null ? "Success" : "Error"] = error ?? "تم تحديث حالة الطلب";
        return RedirectToAction(nameof(Detail), new { id });
    }
    int Actor() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public class ShopInventoryController(DoojaDb db, ShopService shop) : DashboardControllerBase
{
    public async Task<IActionResult> Index()
    {
        var settings = await db.StoreSettings.AsNoTracking().FirstAsync(x => x.Id == 1);
        ViewBag.Low = settings.LowStock;
        ViewBag.Products = await db.Products.AsNoTracking().Include(x => x.Category).Where(x => !x.Archived).OrderBy(x => x.Stock).ThenBy(x => x.Name).ToListAsync();
        return View(await db.InventoryTransactions.AsNoTracking().Include(x => x.Product).OrderByDescending(x => x.Id).Take(200).ToListAsync());
    }
    [HttpPost]
    public async Task<IActionResult> Adjust(int productId, int delta, string note)
    {
        var error = await shop.AdjustStock(productId, delta, note, int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        TempData[error == null ? "Success" : "Error"] = error ?? "تم تعديل المخزون";
        return RedirectToAction(nameof(Index));
    }
}

public class ShopReportsController(DoojaDb db) : DashboardControllerBase
{
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        var end = to ?? BookingService.Today;
        var start = from ?? end.AddDays(-6);
        if (start > end) (start, end) = (end, start);
        ViewBag.From = start.ToString("yyyy-MM-dd");
        ViewBag.To = end.ToString("yyyy-MM-dd");
        var begin = Baghdad.StartUtc(start);
        var until = Baghdad.StartUtc(end.AddDays(1));
        var orders = await db.ShopOrders.AsNoTracking().Include(x => x.Items).Where(x => x.CreatedAt >= begin && x.CreatedAt < until).ToListAsync();
        var live = orders.Where(x => x.Status != ShopOrderStates.Cancelled).ToList();
        ViewBag.Orders = orders.Count;
        ViewBag.Pending = orders.Count(x => x.Status == ShopOrderStates.Pending);
        ViewBag.Completed = orders.Count(x => x.Status == ShopOrderStates.Delivered);
        ViewBag.Cancelled = orders.Count(x => x.Status == ShopOrderStates.Cancelled);
        ViewBag.Revenue = live.Sum(x => x.Total);
        var grouped = live.SelectMany(x => x.Items).GroupBy(x => x.Name).Select(g => new ShopSoldRow(g.Key, g.Sum(x => x.Qty), g.Sum(x => x.LineTotal))).OrderByDescending(x => x.Qty).ToList();
        ViewBag.Best = grouped.Take(8).ToList();
        ViewBag.Least = grouped.OrderBy(x => x.Qty).Take(8).ToList();
        var days = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(start.AddDays).ToList();
        var series = days.Select(d => new ShopDayRow(d.ToString("MM-dd"), live.Where(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), Baghdad.Zone)) == d).Sum(x => x.Total))).ToList();
        ViewBag.Days = series;
        ViewBag.Peak = Math.Max(1, series.Count == 0 ? 0 : series.Max(x => x.Total));
        return View();
    }
}

public class ShopSettingsController(DoojaDb db) : DashboardControllerBase
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Areas = await db.DeliveryAreas.AsNoTracking().OrderBy(x => x.Governorate).ThenBy(x => x.Area).ToListAsync();
        ViewBag.Methods = await db.PaymentMethods.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        ViewBag.Online = false;
        return View(await db.StoreSettings.FirstAsync(x => x.Id == 1));
    }
    [HttpPost]
    public async Task<IActionResult> Save(bool enabled, long deliveryFee, long minOrder, int lowStock, string[]? cancelStatuses)
    {
        if (deliveryFee < 0 || minOrder < 0 || lowStock < 0) { TempData["Error"] = "القيم لا تكون سالبة"; return RedirectToAction(nameof(Index)); }
        var allowed = (cancelStatuses ?? []).Where(ShopOrderStates.All.Contains).Distinct().ToArray();
        var row = await db.StoreSettings.FirstAsync(x => x.Id == 1);
        row.Enabled = enabled;
        row.DeliveryFee = deliveryFee;
        row.MinOrder = minOrder;
        row.LowStock = lowStock;
        row.CancelStatuses = allowed.Length == 0 ? ShopOrderStates.Pending : string.Join(',', allowed);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ إعدادات المتجر";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> AddArea(string governorate, string area, long? fee)
    {
        if (string.IsNullOrWhiteSpace(governorate) || string.IsNullOrWhiteSpace(area)) { TempData["Error"] = "اكتب المحافظة والمنطقة"; return RedirectToAction(nameof(Index)); }
        if (fee is < 0) { TempData["Error"] = "أجرة التوصيل غير صالحة"; return RedirectToAction(nameof(Index)); }
        db.DeliveryAreas.Add(new DeliveryArea { Governorate = governorate.Trim(), Area = area.Trim(), Fee = fee, Active = true });
        await db.SaveChangesAsync();
        TempData["Success"] = "تمت إضافة منطقة التوصيل";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> ToggleArea(int id)
    {
        var area = await db.DeliveryAreas.FindAsync(id);
        if (area == null) return NotFound();
        area.Active = !area.Active;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> TogglePay(int id)
    {
        var method = await db.PaymentMethods.FindAsync(id);
        if (method == null) return NotFound();
        if (method.IsOnline) { TempData["Error"] = "الدفع الإلكتروني غير مربوط بمزوّد دفع حقيقي، لذلك لا يُفعَّل"; return RedirectToAction(nameof(Index)); }
        method.Enabled = !method.Enabled;
        await db.SaveChangesAsync();
        TempData["Success"] = method.Enabled ? "تم تفعيل طريقة الدفع" : "تم إيقاف طريقة الدفع";
        return RedirectToAction(nameof(Index));
    }
}

static class Baghdad
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Baghdad");
    public static DateTime StartUtc(DateOnly day) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone);
}
