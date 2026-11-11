namespace Dooja.Web.Services;

public static class ImageStore
{
    public static async Task<(string? RelativePath, string? Error)> Save(IWebHostEnvironment env, IFormFile file, string folder)
    {
        if (folder.Contains("..", StringComparison.Ordinal) || folder.Contains('/', StringComparison.Ordinal) || folder.Contains('\\', StringComparison.Ordinal))
            return (null, "مسار الحفظ غير صالح");
        if (file.Length <= 0 || file.Length > 4 * 1024 * 1024) return (null, "حجم الصورة يجب أن يكون أقل من 4 ميغابايت");
        await using var stream = file.OpenReadStream();
        var header = new byte[16];
        var read = await stream.ReadAsync(header);
        var ext = Sniff(header.AsSpan(0, read));
        if (ext == null) return (null, "الصورة يجب أن تكون JPEG أو PNG أو WEBP");
        var dir = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + ext;
        await using (var output = File.Create(Path.Combine(dir, name)))
        {
            stream.Position = 0;
            await stream.CopyToAsync(output);
        }
        return ($"/uploads/{folder}/{name}", null);
    }

    public static void Delete(IWebHostEnvironment env, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || !relative.StartsWith("/uploads/", StringComparison.Ordinal)) return;
        var full = Path.GetFullPath(Path.Combine(env.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads"));
        if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full)) return;
        File.Delete(full);
    }

    static string? Sniff(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h.Length >= 12 && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F' && h[8] == (byte)'W' && h[9] == (byte)'E' && h[10] == (byte)'B' && h[11] == (byte)'P') return ".webp";
        return null;
    }
}
