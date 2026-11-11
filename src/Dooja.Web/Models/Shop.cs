using System.ComponentModel.DataAnnotations;
namespace Dooja.Web.Models;

public static class ShopOrderStates
{
    public const string Pending = "بانتظار التأكيد";
    public const string Confirmed = "مؤكد";
    public const string Processing = "قيد التجهيز";
    public const string Shipped = "تم الشحن";
    public const string Delivered = "تم التوصيل";
    public const string Cancelled = "ملغي";
    public static readonly string[] All = [Pending, Confirmed, Processing, Shipped, Delivered, Cancelled];
    public static bool CanAdmin(string from, string to) => from == to || from switch
    {
        Pending => to is Confirmed or Cancelled,
        Confirmed => to is Processing or Cancelled,
        Processing => to is Shipped or Cancelled,
        Shipped => to is Delivered or Cancelled,
        _ => false
    };
}

public class ProductCategory
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    public string? ImagePath { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
    public bool Archived { get; set; }
    public List<Product> Products { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(4000)] public string Description { get; set; } = "";
    [StringLength(4000)] public string Specs { get; set; } = "";
    [Range(0, 100_000_000)] public long Price { get; set; }
    public long? CompareAtPrice { get; set; }
    [Range(0, 1_000_000)] public int Stock { get; set; }
    public bool Active { get; set; } = true;
    public bool Featured { get; set; }
    public bool Archived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ProductImage> Images { get; set; } = [];
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string Path { get; set; } = "";
    public int SortOrder { get; set; }
}

public class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart? Cart { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Qty { get; set; }
    public long LastSeenPrice { get; set; }
}

public class ShopOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public string Status { get; set; } = ShopOrderStates.Pending;
    [StringLength(100)] public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Governorate { get; set; } = "";
    public string Area { get; set; } = "";
    [StringLength(300)] public string Address { get; set; } = "";
    [StringLength(500)] public string Notes { get; set; } = "";
    public long Subtotal { get; set; }
    public long Discount { get; set; }
    public long DeliveryFee { get; set; }
    public long Total { get; set; }
    public string PayMethod { get; set; } = "";
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ShopOrderItem> Items { get; set; } = [];
    public List<ShopOrderEvent> Events { get; set; } = [];
}

public class ShopOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public ShopOrder? Order { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string Name { get; set; } = "";
    public long UnitPrice { get; set; }
    public int Qty { get; set; }
    public long LineTotal { get; set; }
}

public class ShopOrderEvent
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public ShopOrder? Order { get; set; }
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class InventoryTransaction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Delta { get; set; }
    public int StockAfter { get; set; }
    public string Reason { get; set; } = "";
    public int? OrderId { get; set; }
    public int? ActorUserId { get; set; }
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DeliveryArea
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Governorate { get; set; } = "";
    [Required, StringLength(80)] public string Area { get; set; } = "";
    public long? Fee { get; set; }
    public bool Active { get; set; } = true;
}

public class StoreSetting
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public long DeliveryFee { get; set; }
    public long MinOrder { get; set; }
    public int LowStock { get; set; } = 3;
    public string CancelStatuses { get; set; } = ShopOrderStates.Pending;
}

public record ShopSoldRow(string Name, int Qty, long Total);
public record ShopDayRow(string Label, long Total);

public class PaymentMethod
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [Required, StringLength(80)] public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool IsOnline { get; set; }
}
