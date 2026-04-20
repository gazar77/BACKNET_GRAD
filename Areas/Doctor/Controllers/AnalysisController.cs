using HeartCathAPI.Data;
using HeartCathAPI.Models;
using HeartCathAPI.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
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

        public AnalysisController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        private const string PythonApiUrl = "http://localhost:8000";

        // تشغيل التحليل
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

            if (study.Status == StudyStatus.Processing)
                return BadRequest("Study is already processing");

            try
            {
                study.Status = StudyStatus.Processing;
                await _context.SaveChangesAsync();

                var wwwroot = _env.WebRootPath;
                var analysisDir = Path.Combine(wwwroot, "analysis");
                if (!Directory.Exists(analysisDir))
                {
                    Directory.CreateDirectory(analysisDir);
                }

                var pythonScript = Path.Combine(
                    @"C:\Users\joe Store\OneDrive\Desktop\BaclLastGrad",
                    "AI",
                    "angiography_ai.py");

                // Correct path to file in wwwroot
                var normalizedFilePath = study.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString())
                                                      .Replace("\\", Path.DirectorySeparatorChar.ToString());
                
                var inputPath = Path.Combine(wwwroot, normalizedFilePath);
                
                if (!System.IO.File.Exists(inputPath))
                {
                    return BadRequest($"Source file not found at: {inputPath}");
                }

                // Determine endpoint based on file type
                string endpoint = study.FileType == StudyFileType.Video ? "/analyze-video" : "/analyze-image";
                
                using var form = new MultipartFormDataContent();
                using var fileStream = System.IO.File.OpenRead(inputPath);
                using var streamContent = new StreamContent(fileStream);
                form.Add(streamContent, "file", Path.GetFileName(inputPath));

                var response = await _httpClient.PostAsync($"{PythonApiUrl}{endpoint}", form);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return BadRequest($"AI Service Error: {errorMsg}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var aiResult = JsonDocument.Parse(jsonResponse).RootElement;

                double percentage = aiResult.GetProperty("stenosis_percentage").GetDouble();
                string severity = aiResult.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() ?? "Normal" : "Normal";
                string artery = aiResult.TryGetProperty("artery_name", out var artProp) ? artProp.GetString() ?? "Coronary Artery" : "Coronary Artery";
                string diagnosisFromAi = aiResult.TryGetProperty("diagnosis_details", out var diagProp) ? diagProp.GetString() ?? "AI detected stenosis" : "AI detected stenosis";
                
                // Copy result overlay image to wwwroot (only for images)
                var outputImageRel = $"analysis/result_{studyId}.png";
                var outputImageFull = Path.Combine(wwwroot, "analysis", $"result_{studyId}.png");

                if (study.FileType != StudyFileType.Video && aiResult.TryGetProperty("saved_paths", out var savedPaths))
                {
                    var overlayPath = savedPaths.GetProperty("overlay_path").GetString();
                    if (!string.IsNullOrEmpty(overlayPath) && System.IO.File.Exists(overlayPath))
                    {
                        System.IO.File.Copy(overlayPath, outputImageFull, true);
                    }
                }

                var result = new AnalysisResult
                {
                    StudyId = studyId,
                    StenosisPercentage = percentage,
                    Report = "AI detected stenosis",
                    ImagePath = outputImageRel,
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

        // جلب النتيجة
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