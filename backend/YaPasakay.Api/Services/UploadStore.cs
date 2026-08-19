namespace YaPasakay.Api.Services;

public class UploadStore(IWebHostEnvironment environment)
{
    private static readonly HashSet<string> Allowed = [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<string?> SaveAsync(IFormFile? file, string folder, string fileName, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = file.ContentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }
        if (!Allowed.Contains(ext))
        {
            throw new InvalidOperationException("Use a JPG, PNG, or WEBP image.");
        }

        var relative = Path.Combine(folder, fileName + ext).Replace('\\', '/');
        var root = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");
        var full = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var stream = File.Create(full);
        await file.CopyToAsync(stream, cancellationToken);
        return relative;
    }
}
