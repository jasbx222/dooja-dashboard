using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Dooja.Web.Data;

public static class ShopSchema
{
    static readonly string[] Tables =
    [
        "ProductCategories", "Products", "ProductImages", "Carts", "CartItems",
        "ShopOrders", "ShopOrderItems", "ShopOrderEvents", "InventoryTransactions",
        "DeliveryAreas", "StoreSettings", "PaymentMethods"
    ];

    public static async Task Apply(DoojaDb db)
    {
        await db.Database.EnsureCreatedAsync();
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();
        var pg = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        if (!pg)
        {
            foreach (var pragmaText in new[] { "PRAGMA busy_timeout = 5000;", "PRAGMA journal_mode = WAL;" })
            {
                await using var pragma = conn.CreateCommand();
                pragma.CommandText = pragmaText;
                try { await pragma.ExecuteNonQueryAsync(); } catch { /* WAL is best-effort on this host. */ }
            }
        }
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = pg
                ? "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"
                : "SELECT name FROM sqlite_master WHERE type = 'table'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) existing.Add(reader.GetString(0));
        }
        if (existing.Contains("Products")) return;
        var script = db.Database.GenerateCreateScript();
        foreach (var statement in Split(script))
        {
            if (!Tables.Any(t => statement.Contains('"' + t + '"', StringComparison.Ordinal))) continue;
            if (!statement.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)) continue;
            await db.Database.ExecuteSqlRawAsync(statement);
        }
    }

    static IEnumerable<string> Split(string script)
    {
        foreach (var part in Regex.Split(script, @";\s*(?:\r?\n|$)"))
        {
            var sql = part.Trim();
            if (sql.Length > 0) yield return sql;
        }
    }
}
