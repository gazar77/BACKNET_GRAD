using HeartCathAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HeartCathAPI.Areas.Doctor.Controllers
{
    [Area("Doctor")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var doctor = await _context.Users.FindAsync(userId);
            if (doctor == null) return NotFound();

            try
            {
                var totalPatients = await _context.Patients.CountAsync(p => p.UserId == userId);
                var totalReports = await _context.AnalysisResults.CountAsync(a => a.Study.UserId == userId);

                var recentStudies = await _context.Studies
                    .Include(s => s.Patient)
                    .Include(s => s.AnalysisResults)
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.UploadDate)
                    .Take(5)
                    .ToListAsync();

                var recentAnalyses = recentStudies.Select(s => {
                    var lastResult = s.AnalysisResults?.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                    return new {
                        id = s.Id.ToString(),
                        patientName = s.Patient?.FullName ?? "Unknown",
                        age = s.Patient?.Age,
                        gender = s.Patient?.Gender,
                        stenosisPercent = (int)(lastResult?.StenosisPercentage ?? 0),
                        artery = lastResult?.ArteryName ?? "N/A",
                        riskLevel = lastResult?.RiskLevel ?? "N/A",
                        diagnosisDetails = lastResult?.DiagnosisDetails ?? "No details available",
                        image1 = lastResult?.ImagePath, // Principal result image
                        image2 = s.FilePath,            // Original image/video path as fallback
                        date = s.UploadDate.ToString("MMM dd, yyyy")
                    };
                }).ToList();

                return Ok(new
                {
                    doctorName = doctor.FullName ?? "Doctor",
                    doctorImage = doctor.ProfileImageUrl,
                    hospital = doctor.Hospital,
                    title = doctor.Title,
                    mobile = doctor.Mobile,
                    extension = doctor.Extension,
                    totalPatients = totalPatients,
                    totalReports = totalReports,
                    recentAnalyses = recentAnalyses
                });
            }
            catch (Exception ex)
            {
                // Fallback response with basic doctor info if statistics fail
                return Ok(new
                {
                    doctorName = doctor.FullName ?? "Doctor",
                    doctorImage = doctor.ProfileImageUrl,
                    hospital = doctor.Hospital,
                    title = doctor.Title,
                    error = ex.Message,
                    totalPatients = 0,
                    totalReports = 0,
                    recentAnalyses = new List<object>()
                });
            }
        }
    }
}
