using System.Security.Cryptography;

namespace backend.Services;

public class FileService
{
    private readonly IWebHostEnvironment _env;

    public FileService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<(string filePath, string hash)> SaveFileAsync(IFormFile file)
    {
        var uploadsFolder = Path.Combine(_env.ContentRootPath, "uploads");

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueName = $"{Guid.NewGuid()}_{file.FileName}";
        var fullPath = Path.Combine(uploadsFolder, uniqueName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        using var sha = SHA256.Create();
        await using var fileStream = File.OpenRead(fullPath);
        var hashBytes = await sha.ComputeHashAsync(fileStream);
        var hash = Convert.ToHexString(hashBytes);

        return (fullPath, hash);
    }
}