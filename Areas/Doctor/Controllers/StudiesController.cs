using HeartCathAPI.Data;
using HeartCathAPI.DTOs.Request;
using HeartCathAPI.DTOs.Response;
using HeartCathAPI.Models;
using HeartCathAPI.Models.Enums;

using HeartCathAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace HeartCathAPI.Areas.Doctor.Controllers
{
    [Area("Doctor")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize]
    public class StudiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly FileService _fileService;

        public StudiesController(ApplicationDbContext context, FileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        // Upload Study
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadStudyRequest request)
        {
            if (request.File == null)
                return BadRequest("File required");

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.UserId == userId);

            if (patient == null)
                return BadRequest("Patient not found or unauthorized");

            var extension = Path.GetExtension(request.File.FileName).ToLower();

            StudyFileType fileType;

            if (extension == ".dcm")
                fileType = StudyFileType.Dicom;
            else if (extension == ".mp4" || extension == ".avi" || extension == ".mov")
                fileType = StudyFileType.Video;
            else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                fileType = StudyFileType.Image;
            else
                return BadRequest("Only DICOM, Video (MP4/AVI/MOV), or Image (PNG/JPG/JPEG) are allowed");

            var path = await _fileService.SaveFileAsync(request.File, "uploads");

            var study = new Study
            {
                UserId = userId,
                PatientId = request.PatientId,
                FilePath = path,
                FileType = fileType,
                Status = StudyStatus.Uploaded,
                UploadDate = DateTime.UtcNow
            };

            _context.Studies.Add(study);
            await _context.SaveChangesAsync();

            var response = new StudyResponse
            {
                Id = study.Id,
                PatientId = study.PatientId,
                FilePath = study.FilePath,
                Status = study.Status.ToString(),
                UploadDate = study.UploadDate
            };

            return Ok(response);
        }

        // Get All Studies
        [HttpGet]
        public async Task<IActionResult> GetStudies()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var studies = await _context.Studies.Where(s => s.UserId == userId).ToListAsync();

            var result = studies.Select(s => new StudyResponse
            {
                Id = s.Id,
                PatientId = s.PatientId,
                FilePath = s.FilePath,
                Status = s.Status.ToString(),
                UploadDate = s.UploadDate
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudy(int id)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var study = await _context.Studies
                .Include(s => s.Patient)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (study == null)
                return NotFound();

            var analysis = await _context.AnalysisResults
                .FirstOrDefaultAsync(a => a.StudyId == id);

            var response = new StudyDetailsResponse
            {
                StudyId = study.Id,
                VideoPath = study.FilePath,
                Status = study.Status.ToString(),

                Patient = new PatientInfo
                {
                    Id = study.Patient.Id,
                    Name = study.Patient.FullName,
                    Notes = study.Patient.Notes
                },

                Analysis = analysis == null ? null : new AnalysisInfo
                {
                    Percentage = analysis.StenosisPercentage,
                    Report = analysis.Report,
                    Image = analysis.ImagePath
                }
            };

            return Ok(response);
        }
    }
}