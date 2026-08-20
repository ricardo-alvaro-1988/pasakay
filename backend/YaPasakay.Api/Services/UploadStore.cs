namespace YaPasakay.Api.Services;

public class UploadStore(IWebHostEnvironment environment, IConfiguration configuration)
{
    private static readonly HashSet<string> Allowed = [".jpg", ".jpeg", ".png", ".webp"];
    private readonly string root = StoragePaths.UploadRoot(configuration, environment);

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

        var relative = BuildRelativePath(folder, fileName, ext);
        var full = SafeFullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var stream = File.Create(full);
        await file.CopyToAsync(stream, cancellationToken);
        return relative;
    }

    private string SafeFullPath(string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid upload path.");
        }

        return full;
    }

    private static string BuildRelativePath(string folder, string fileName, string extension)
    {
        var cleanFolder = folder.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(cleanFolder) ||
            cleanFolder.Split('/').Any(x => string.IsNullOrWhiteSpace(x) || x is "." or ".."))
        {
            throw new InvalidOperationException("Invalid upload folder.");
        }

        var cleanName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(cleanName) || cleanName is "." or "..")
        {
            throw new InvalidOperationException("Invalid upload file name.");
        }

        return $"{cleanFolder}/{cleanName}{extension}".Replace('\\', '/');
    }
}
