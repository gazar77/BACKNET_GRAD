using HeartCathAPI.Data;
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
    public class ToolsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ToolsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        private const string PythonApiUrl = "http://localhost:8000";

        [HttpPost("convert-dicom/{studyId}")]
        public async Task<IActionResult> ConvertDicom(int studyId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var study = await _context.Studies
                .FirstOrDefaultAsync(s => s.Id == studyId && s.UserId == userId);

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

                using var form = new MultipartFormDataContent();
                using var fileStream = System.IO.File.OpenRead(inputPath);
                using var streamContent = new StreamContent(fileStream);
                form.Add(streamContent, "file", Path.GetFileName(inputPath));

                var response = await _httpClient.PostAsync($"{PythonApiUrl}/dicom-to-video", form);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return BadRequest($"Conversion Service Error: {errorMsg}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var aiResult = JsonDocument.Parse(jsonResponse).RootElement;

                var remoteVideoPath = aiResult.GetProperty("video_path").GetString();
                
                if (!string.IsNullOrEmpty(remoteVideoPath) && System.IO.File.Exists(remoteVideoPath))
                {
                    System.IO.File.Copy(remoteVideoPath, outputPath, true);
                }
                else
                {
                    return BadRequest("Result video file not found on AI server");
                }

                // Update study to point to the new MP4
                study.FilePath = outputRelPath;
                study.FileType = StudyFileType.Video;
                await _context.SaveChangesAsync();

                return Ok(new { 
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
