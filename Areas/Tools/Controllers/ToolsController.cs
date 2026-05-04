using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace HeartCathAPI.Areas.Tools.Controllers
{
    [Area("Tools")]
    [ApiController]
    [Route("api/[area]/[controller]")]
    public class ToolsController : ControllerBase
    {
        private readonly string _pythonBaseUrl;

        public ToolsController(IConfiguration configuration)
        {
            _pythonBaseUrl = (configuration["PythonApi:BaseUrl"] ?? "http://localhost:7860").TrimEnd('/');
        }

        private static readonly HttpClient HttpClientStatic = new() { Timeout = TimeSpan.FromMinutes(10) };

        [HttpPost("dicom-to-video")]
        public async Task<IActionResult> ConvertDicomToVideo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (!file.FileName.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase))
                return BadRequest("File must be .dcm");

            await using var uploadStream = file.OpenReadStream();

            using var form = new MultipartFormDataContent();
            var streamContent = new StreamContent(uploadStream);
            form.Add(streamContent, "file", file.FileName);

            var aiResponse = await HttpClientStatic.PostAsync($"{_pythonBaseUrl}/dicom-to-video", form).ConfigureAwait(false);

            if (!aiResponse.IsSuccessStatusCode)
            {
                var errorMsg = await aiResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                return BadRequest($"Conversion Error: {errorMsg}");
            }

            var jsonResponse = await aiResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
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

            var videoResponse = await HttpClientStatic.GetAsync(downloadUrl).ConfigureAwait(false);
            if (!videoResponse.IsSuccessStatusCode)
            {
                var err = await videoResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                return BadRequest($"Could not retrieve converted video from AI service: {err}");
            }

            var videoBytes = await videoResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            var downloadName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.mp4";

            return File(videoBytes, "video/mp4", downloadName);
        }
    }
}
