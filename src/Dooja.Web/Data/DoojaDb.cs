using Dooja.Web.Models;
using Microsoft.EntityFrameworkCore;
namespace Dooja.Web.Data;
public class DoojaDb(DbContextOptions<DoojaDb> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>(); public DbSet<Place> Places => Set<Place>();
    public DbSet<Booking> Bookings => Set<Booking>(); public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Review> Reviews => Set<Review>(); public DbSet<Reel> Reels => Set<Reel>();
    public DbSet<ReelComment> ReelComments => Set<ReelComment>(); public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Follow> Follows => Set<Follow>(); public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OwnerService> OwnerServices => Set<OwnerService>(); public DbSet<ClosedSlot> ClosedSlots => Set<ClosedSlot>();
    public DbSet<ReelLike> ReelLikes => Set<ReelLike>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<ShopOrder> ShopOrders => Set<ShopOrder>();
    public DbSet<ShopOrderItem> ShopOrderItems => Set<ShopOrderItem>();
    public DbSet<ShopOrderEvent> ShopOrderEvents => Set<ShopOrderEvent>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<DeliveryArea> DeliveryAreas => Set<DeliveryArea>();
    public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(x => x.Phone).IsUnique(); b.Entity<Booking>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Review>().HasIndex(x => x.BookingId).IsUnique();
        b.Entity<Favorite>().HasKey(x => new { x.UserId, x.PlaceId }); b.Entity<Follow>().HasKey(x => new { x.UserId, x.PlaceId });
        b.Entity<ReelLike>().HasKey(x => new { x.UserId, x.ReelId });
        b.Entity<ClosedSlot>().HasIndex(x => new { x.PlaceId, x.Day, x.Time }).IsUnique();
        b.Entity<Booking>().HasIndex(x => new { x.PlaceId, x.Day, x.Time });
        b.Entity<StoreSetting>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<Cart>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<CartItem>().HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
        b.Entity<ShopOrder>().HasIndex(x => x.Number).IsUnique();
        b.Entity<ShopOrder>().HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        b.Entity<ShopOrder>().HasIndex(x => x.CreatedAt);
        b.Entity<PaymentMethod>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Product>().HasIndex(x => x.CategoryId);
        foreach (var rel in b.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys())) rel.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
