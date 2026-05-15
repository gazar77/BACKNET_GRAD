using HeartCathAPI.Data;
using HeartCathAPI.Models;
using HeartCathAPI.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http;
using System.Text.Json;

namespace HeartCathAPI.Areas.Doctor.Controllers
{
    [Area("Doctor")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize]
    public class AnalysisController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly string _pythonBaseUrl;

        public AnalysisController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _pythonBaseUrl = (configuration["PythonApi:BaseUrl"] ?? "http://localhost:7860").TrimEnd('/');
        }

        private static readonly HttpClient HttpClientStatic = new() { Timeout = TimeSpan.FromMinutes(5) };

        [HttpPost("{studyId}")]
        [Authorize]
        public async Task<IActionResult> Analyze(int studyId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var study = await _context.Studies
                .Include(s => s.Patient)
                .FirstOrDefaultAsync(s => s.Id == studyId && s.UserId == userId);

            if (study == null)
                return NotFound("Study not found");

            // Allow re-analysis if status is Processing (stuck from a prior failed run).
            if (study.Status == StudyStatus.Processing)
                study.Status = StudyStatus.Failed;

            try
            {
                study.Status = StudyStatus.Processing;
                await _context.SaveChangesAsync();

                var wwwroot = _env.WebRootPath;
                var analysisDir = Path.Combine(wwwroot, "analysis");
                if (!Directory.Exists(analysisDir))
                    Directory.CreateDirectory(analysisDir);

                var normalizedFilePath = study.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString())
                    .Replace("\\", Path.DirectorySeparatorChar.ToString());

                var inputPath = Path.Combine(wwwroot, normalizedFilePath);

                if (!System.IO.File.Exists(inputPath))
                    return BadRequest($"Source file not found at: {inputPath}");

                var endpoint = study.FileType == StudyFileType.Video ? "/analyze-video" : "/analyze-image";

                using var form = new MultipartFormDataContent();
                await using var fileStream = System.IO.File.OpenRead(inputPath);
                var streamContent = new StreamContent(fileStream);
                form.Add(streamContent, "file", Path.GetFileName(inputPath));

                var response = await HttpClientStatic.PostAsync($"{_pythonBaseUrl}{endpoint}", form);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return BadRequest($"AI Service Error: {errorMsg}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(jsonResponse).RootElement;
                var data = PayloadElement(root);

                var percentage = 0d;
                if (data.TryGetProperty("stenosis_percentage", out var pctEl))
                    percentage = pctEl.ValueKind == JsonValueKind.Number ? pctEl.GetDouble() : percentage;

                var severity = data.TryGetProperty("severity", out var sevProp)
                    ? sevProp.GetString() ?? "Normal"
                    : "Normal";

                var artery = data.TryGetProperty("artery_name", out var artProp)
                    ? artProp.GetString() ?? "Coronary Artery"
                    : "Coronary Artery";

                string diagnosisFromAi;
                if (data.TryGetProperty("diagnosis_details", out var diagProp) &&
                    diagProp.ValueKind == JsonValueKind.String)
                {
                    diagnosisFromAi = diagProp.GetString() ?? string.Empty;
                }
                else if (data.TryGetProperty("report", out var reportProp))
                {
                    diagnosisFromAi = reportProp.GetString() ?? "Video analysis pending or placeholder.";
                }
                else if (data.TryGetProperty("message", out var msgProp))
                {
                    diagnosisFromAi = msgProp.GetString() ?? "Analysis complete.";
                }
                else
                {
                    diagnosisFromAi = "AI detected result.";
                }

                string imageRef;
                if (TryGetLegacyOverlayPhysicalPath(root, data, wwwroot, studyId,
                         out var legacyRel))
                {
                    imageRef = legacyRel!;
                }
                else if (TryResolveOverlayUrl(data, out var overlayAbsoluteUrl))
                {
                    await TryMirrorOverlayAsync(overlayAbsoluteUrl!, wwwroot, studyId).ConfigureAwait(false);
                    var mirrored = Path.Combine(wwwroot, "analysis", $"result_{studyId}.png");
                    imageRef = System.IO.File.Exists(mirrored)
                        ? $"analysis/result_{studyId}.png"
                        : overlayAbsoluteUrl!;
                }
                else
                {
                    imageRef = $"analysis/result_{studyId}.png";
                }

                var result = new AnalysisResult
                {
                    StudyId = studyId,
                    StenosisPercentage = percentage,
                    Report = "AI detected stenosis",
                    ImagePath = imageRef,
                    ArteryName = artery,
                    RiskLevel = severity,
                    DiagnosisDetails = diagnosisFromAi
                };

                _context.AnalysisResults.Add(result);
                study.Status = StudyStatus.Completed;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    studyId = result.StudyId,
                    stenosisPercentage = result.StenosisPercentage,
                    report = result.Report,
                    imagePath = result.ImagePath,
                    arteryName = result.ArteryName,
                    riskLevel = result.RiskLevel,
                    diagnosisDetails = result.DiagnosisDetails,
                    patientName = study.Patient?.FullName ?? "Unknown Patient"
                });
            }
            catch (Exception ex)
            {
                study.Status = StudyStatus.Failed;
                await _context.SaveChangesAsync();
                return BadRequest($"Analysis system error: {ex.Message}");
            }
        }

        private static JsonElement PayloadElement(JsonElement root)
        {
            return root.TryGetProperty("data", out var dataEl) ? dataEl : root;
        }

        private static bool TryGetLegacyOverlayPhysicalPath(JsonElement root, JsonElement dataEl, string wwwroot,
            int studyId, out string? relativeUnderWwwroot)
        {
            relativeUnderWwwroot = null;

            JsonElement savedPathsEl;
            if (root.TryGetProperty("saved_paths", out savedPathsEl) ||
                dataEl.TryGetProperty("saved_paths", out savedPathsEl))
            {
                if (savedPathsEl.TryGetProperty("overlay_path", out var overlayPathProp))
                {
                    var overlayPath = overlayPathProp.GetString();
                    if (!string.IsNullOrEmpty(overlayPath) && System.IO.File.Exists(overlayPath))
                    {
                        var outputImageRel = $"analysis/result_{studyId}.png";
                        var outputImageFull = Path.Combine(wwwroot, "analysis", $"result_{studyId}.png");
                        Directory.CreateDirectory(Path.GetDirectoryName(outputImageFull)!);
                        System.IO.File.Copy(overlayPath, outputImageFull, true);
                        relativeUnderWwwroot = outputImageRel;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryResolveOverlayUrl(JsonElement dataEl, out string? absoluteUrl)
        {
            absoluteUrl = null;
            JsonElement urlsEl;
            if (!dataEl.TryGetProperty("analysis_metadata", out var meta) ||
                !meta.TryGetProperty("saved_urls", out urlsEl))
                return false;

            if (!urlsEl.TryGetProperty("overlay_url", out var ou) ||
                ou.ValueKind != JsonValueKind.String)
                return false;

            var relative = ou.GetString();
            if (string.IsNullOrEmpty(relative))
                return false;

            absoluteUrl = relative.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         relative.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? relative
                : $"{_pythonBaseUrl}{relative}";
            return true;
        }

        private static async Task TryMirrorOverlayAsync(string overlayAbsoluteUrl, string wwwroot, int studyId)
        {
            try
            {
                var outputImageFull = Path.Combine(wwwroot, "analysis", $"result_{studyId}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(outputImageFull)!);
                using var getResponse = await HttpClientStatic.GetAsync(overlayAbsoluteUrl).ConfigureAwait(false);
                if (!getResponse.IsSuccessStatusCode)
                    return;
                await using var overlayStream =
                    await getResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var fileOut = System.IO.File.Create(outputImageFull);
                await overlayStream.CopyToAsync(fileOut).ConfigureAwait(false);
            }
            catch
            {
                /* keep remote URL in ImagePath if mirror fails */
            }
        }

        [HttpGet("{studyId}")]
        public async Task<IActionResult> GetResult(int studyId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var result = await _context.AnalysisResults
                .Include(a => a.Study)
                .ThenInclude(s => s.Patient)
                .FirstOrDefaultAsync(x => x.StudyId == studyId && x.Study.UserId == userId);

            if (result == null)
                return NotFound("No analysis result found");

            return Ok(new
            {
                studyId = result.StudyId,
                stenosisPercentage = result.StenosisPercentage,
                report = result.Report,
                imagePath = result.ImagePath,
                arteryName = result.ArteryName,
                riskLevel = result.RiskLevel,
                diagnosisDetails = result.DiagnosisDetails,
                patientName = result.Study.Patient?.FullName ?? "Unknown Patient"
            });
        }
    }
}
