using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Dooja.Web.Data;
using Dooja.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
var dbProvider = builder.Configuration["Database:Provider"];
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = builder.Configuration.GetConnectionString("Dooja");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = "Data Source=Dooja.db";
builder.Services.AddDbContext<DoojaDb>(o =>
{
    if (string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
        o.UseNpgsql(connectionString);
    else
        o.UseSqlite(connectionString);
});
builder.Services.AddControllersWithViews().AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddProblemDetails();
var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(key)) { if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Set Jwt__Key (at least 32 bytes) in production."); key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)); }
if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Jwt key must be at least 32 bytes.");
builder.Services.AddSingleton(new TokenService(key));
builder.Services.AddSingleton<OtpStore>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ShopService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
 .AddCookie(o => { o.LoginPath = "/Auth/Login"; o.AccessDeniedPath = "/Auth/Denied"; o.Cookie.Name = "Dooja.Dashboard"; o.ExpireTimeSpan = TimeSpan.FromHours(8); o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax; })
 .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = "Dooja", ValidAudience = "Dooja.App", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.FromSeconds(30) });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => { if (builder.Environment.IsDevelopment()) p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); else { var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []; if (origins.Length > 0) p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod(); } }));
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); o.AddPolicy("orders", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
var app = builder.Build();
using (var scope = app.Services.CreateScope()) await Seed.Run(scope.ServiceProvider.GetRequiredService<DoojaDb>(), app.Configuration, app.Environment);
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler(); app.UseHsts(); app.UseHttpsRedirection(); }
var flutterAppRoot = Path.Combine(app.Environment.WebRootPath, "app");
if (Directory.Exists(flutterAppRoot))
{
    var flutterFiles = new PhysicalFileProvider(flutterAppRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = flutterFiles, RequestPath = "/app" });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = flutterFiles, RequestPath = "/app" });
}
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
if (Directory.Exists(flutterAppRoot))
    app.MapFallbackToFile("/app/{**path:nonfile}", "app/index.html");
app.Run();
public partial class Program;
