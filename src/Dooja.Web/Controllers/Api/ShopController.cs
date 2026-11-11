using Dooja.Web.Data;
using Dooja.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dooja.Web.Controllers.Api;

[Route("api/shop")]
public class ShopController(DoojaDb db, ShopService shop) : ApiBase(db)
{
    public record QtyRequest(int ProductId, int Qty);
    public record MergeRequest(List<QtyRequest>? Items);
    [HttpGet("settings"), AllowAnonymous] public async Task<IActionResult> Settings() => Ok(await shop.PublicSettings());
    [HttpGet("categories"), AllowAnonymous] public async Task<IActionResult> Categories() => Ok(await shop.Categories());
    [HttpGet("products"), AllowAnonymous]
    public async Task<IActionResult> Products(string? q, int? categoryId, long? minPrice, long? maxPrice, string? sort, bool? discounted, int page = 1, int pageSize = 20) =>
        Ok(await shop.Products(q, categoryId, minPrice, maxPrice, sort, discounted, page, pageSize));
    [HttpGet("products/{id:int}"), AllowAnonymous]
    public async Task<IActionResult> Product(int id) { var p = await shop.Product(id); return p == null ? NotFound(new { error = "المنتج غير متاح" }) : Ok(p); }

    [HttpGet("/api/cart")] public async Task<IActionResult> Cart() => Ok(await shop.CartView(UserId));
    [HttpPost("/api/cart/items")]
    public async Task<IActionResult> Add(QtyRequest input)
    {
        var (cart, error) = await shop.Add(UserId, input.ProductId, input.Qty);
        return error == null ? Ok(cart) : BadRequest(new { error });
    }
    [HttpPut("/api/cart/items/{productId:int}")]
    public async Task<IActionResult> Set(int productId, QtyRequest input)
    {
        var (cart, error) = await shop.SetQty(UserId, productId, input.Qty);
        return error == null ? Ok(cart) : BadRequest(new { error });
    }
    [HttpDelete("/api/cart/items/{productId:int}")]
    public async Task<IActionResult> Remove(int productId)
    {
        var (cart, error) = await shop.Remove(UserId, productId);
        return error == null ? Ok(cart) : BadRequest(new { error });
    }
    [HttpPost("/api/cart/merge")]
    public async Task<IActionResult> Merge(MergeRequest input)
    {
        var items = (input.Items ?? []).Select(x => (x.ProductId, x.Qty)).ToList();
        return Ok(await shop.Merge(UserId, items));
    }

    [HttpGet("/api/orders")] public async Task<IActionResult> Orders() => Ok(await shop.MyOrders(UserId));
    [HttpGet("/api/orders/{id:int}")]
    public async Task<IActionResult> Order(int id) { var o = await shop.MyOrder(UserId, id); return o == null ? NotFound(new { error = "الطلب غير موجود" }) : Ok(o); }
    [HttpPost("/api/orders"), EnableRateLimiting("orders")]
    public async Task<IActionResult> Checkout(CheckoutInput input)
    {
        var (order, error, replayed) = await shop.Checkout(UserId, input);
        if (error != null) return BadRequest(new { error });
        return replayed ? Ok(order) : Created($"/api/orders/{order!.GetType().GetProperty("Id")!.GetValue(order)}", order);
    }
    [HttpPost("/api/orders/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var (order, error) = await shop.CancelMine(UserId, id);
        return error == null ? Ok(order) : BadRequest(new { error });
    }
}
