using HeartCathAPI.Data;
using HeartCathAPI.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;

namespace HeartCathAPI.Areas.Doctor.Controllers
{
    [Area("Doctor")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize]
    public class ToolsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly string _pythonBaseUrl;

        public ToolsController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _pythonBaseUrl = (configuration["PythonApi:BaseUrl"] ?? "http://localhost:7860").TrimEnd('/');
        }

        private static readonly HttpClient HttpClientStatic = new() { Timeout = TimeSpan.FromMinutes(10) };

        [HttpPost("convert-dicom/{studyId}")]
        public async Task<IActionResult> ConvertDicom(int studyId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var study = await _context.Studies
                .FirstOrDefaultAsync(s => s.Id == studyId && s.UserId == userId).ConfigureAwait(false);

            if (study == null)
                return NotFound("Study not found");

            if (study.FileType != StudyFileType.Dicom)
                return BadRequest("File is not a DICOM file");

            try
            {
                var wwwroot = _env.WebRootPath;
                var inputRelPath = study.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString())
                    .Replace("\\", Path.DirectorySeparatorChar.ToString());
                var inputPath = Path.Combine(wwwroot, inputRelPath);

                if (!System.IO.File.Exists(inputPath))
                    return BadRequest("Source DICOM file not found");

                var outputFileName = Path.GetFileNameWithoutExtension(study.FilePath) + ".mp4";
                var outputRelPath = $"uploads/{outputFileName}";
                var outputPath = Path.Combine(wwwroot, "uploads", outputFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                using var form = new MultipartFormDataContent();
                await using var fileStream = System.IO.File.OpenRead(inputPath);
                var streamContent = new StreamContent(fileStream);
                form.Add(streamContent, "file", Path.GetFileName(inputPath));

                var response =
                    await HttpClientStatic.PostAsync($"{_pythonBaseUrl}/dicom-to-video", form).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return BadRequest($"Conversion Service Error: {errorMsg}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var root = JsonDocument.Parse(jsonResponse).RootElement;
                var data = root.TryGetProperty("data", out var d) ? d : root;

                string? videoRelUrl = null;
                if (data.TryGetProperty("video_url", out var vu) && vu.ValueKind == JsonValueKind.String)
                    videoRelUrl = vu.GetString();

                if (string.IsNullOrEmpty(videoRelUrl))
                    return BadRequest("AI service did not return video_url");

                var downloadUrl = videoRelUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                  videoRelUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? videoRelUrl
                    : $"{_pythonBaseUrl}{videoRelUrl}";

                var videoResp = await HttpClientStatic.GetAsync(downloadUrl).ConfigureAwait(false);
                if (!videoResp.IsSuccessStatusCode)
                {
                    var errBody = await videoResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return BadRequest($"Could not retrieve converted video: {errBody}");
                }

                await using var vidStream = await videoResp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var outFile = System.IO.File.Create(outputPath);
                await vidStream.CopyToAsync(outFile).ConfigureAwait(false);

                study.FilePath = outputRelPath;
                study.FileType = StudyFileType.Video;
                await _context.SaveChangesAsync().ConfigureAwait(false);

                return Ok(new
                {
                    success = true,
                    message = "Converted successfully",
                    filePath = outputRelPath
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"System error: {ex.Message}");
            }
        }
    }
}
