using Microsoft.AspNetCore.Hosting;

namespace HeartCathAPI.Services;

public class FileService
{
    private readonly IWebHostEnvironment _env;

    public FileService(IWebHostEnvironment env)
    {
        _env = env;
    }

    // حفظ الفيديو
    public async Task<string> SaveFileAsync(IFormFile file, string folder)
    {
        var uploadPath = Path.Combine(_env.WebRootPath, folder);

        if (!Directory.Exists(uploadPath))
            Directory.CreateDirectory(uploadPath);

        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);

        var filePath = Path.Combine(uploadPath, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);

        await file.CopyToAsync(stream);

        return $"{folder}/{fileName}";
    }

    // حفظ صور التحليل
    public async Task<string> SaveAnalysisImageAsync(IFormFile file, int studyId)
    {
        var folder = Path.Combine(_env.WebRootPath, "studies", $"study-{studyId}", "results");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var fileName = Guid.NewGuid() + ".png";

        var path = Path.Combine(folder, fileName);

        using var stream = new FileStream(path, FileMode.Create);

        await file.CopyToAsync(stream);

        return $"studies/study-{studyId}/results/{fileName}";
    }
}