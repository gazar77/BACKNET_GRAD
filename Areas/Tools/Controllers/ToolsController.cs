using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace HeartCathAPI.Areas.Tools.Controllers
{
    [Area("Tools")]
    [ApiController]
    [Route("api/[area]/[controller]")]
    public class ToolsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public ToolsController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        private const string PythonApiUrl = "http://localhost:8000";

        [HttpPost("dicom-to-video")]
        public async Task<IActionResult> ConvertDicomToVideo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (!file.FileName.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase))
                return BadRequest("File must be .dcm");

            var tempFolder = Path.Combine(_env.ContentRootPath, "temp");
            Directory.CreateDirectory(tempFolder);

            var dicomPath = Path.Combine(tempFolder, Guid.NewGuid() + ".dcm");

            using (var stream = new FileStream(dicomPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var outputVideo = Path.Combine(tempFolder, Guid.NewGuid() + ".mp4");

            using var form = new MultipartFormDataContent();
            using var fileStreamForAI = System.IO.File.OpenRead(dicomPath);
            using var streamContent = new StreamContent(fileStreamForAI);
            form.Add(streamContent, "file", Path.GetFileName(dicomPath));

            var aiResponse = await _httpClient.PostAsync($"{PythonApiUrl}/dicom-to-video", form);

            if (!aiResponse.IsSuccessStatusCode)
            {
                var errorMsg = await aiResponse.Content.ReadAsStringAsync();
                return BadRequest($"Conversion Error: {errorMsg}");
            }

            var jsonResponse = await aiResponse.Content.ReadAsStringAsync();
            var aiResult = JsonDocument.Parse(jsonResponse).RootElement;
            var remoteVideoPath = aiResult.GetProperty("video_path").GetString();

            if (!string.IsNullOrEmpty(remoteVideoPath) && System.IO.File.Exists(remoteVideoPath))
            {
                System.IO.File.Copy(remoteVideoPath, outputVideo, true);
            }
            else
            {
                return BadRequest("Video was not created on AI server");
            }

            var streamVideo = new FileStream(outputVideo, FileMode.Open, FileAccess.Read);

            Response.Headers["Content-Disposition"] =
                $"attachment; filename={Path.GetFileNameWithoutExtension(file.FileName)}.mp4";

            return File(streamVideo, "video/mp4");
        }
    }
}