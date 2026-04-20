using HeartCathAPI.Data;
using HeartCathAPI.DTOs.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HeartCathAPI.Controllers
{
    [Area("Doctor")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize]
    public class DoctorsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DoctorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Get current doctor profile
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (doctorId == null)
                return Unauthorized();

            var doctor = await _context.Users
                .Where(x => x.Id == int.Parse(doctorId))
                .Select(x => new
                {
                    x.Id,
                    x.FullName,
                    x.Email,
                    x.Hospital,
                    x.Title,
                    x.Mobile,
                    x.Extension,
                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (doctor == null)
                return NotFound();

            return Ok(doctor);
        }

        // Update doctor profile
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromForm] UpdateDoctorRequest model)
        {
            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (doctorId == null)
                return Unauthorized();

            var doctor = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == int.Parse(doctorId));

            if (doctor == null)
                return NotFound();

            if (!string.IsNullOrEmpty(model.FullName))
                doctor.FullName = model.FullName;

            if (!string.IsNullOrEmpty(model.Hospital))
                doctor.Hospital = model.Hospital;

            if (!string.IsNullOrEmpty(model.Title))
                doctor.Title = model.Title;

            if (!string.IsNullOrEmpty(model.Mobile))
                doctor.Mobile = model.Mobile;

            if (!string.IsNullOrEmpty(model.Extension))
                doctor.Extension = model.Extension;

            // رفع الصورة
            if (model.ProfileImage != null)
            {
                var folder = Path.Combine("wwwroot", "profiles");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var fileName = Guid.NewGuid() + Path.GetExtension(model.ProfileImage.FileName);

                var path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await model.ProfileImage.CopyToAsync(stream);
                }

                doctor.ProfileImageUrl = "/profiles/" + fileName;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Profile updated successfully",
                image = doctor.ProfileImageUrl
            });
        }
    }
}