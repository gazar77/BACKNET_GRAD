using HeartCathAPI.Data;
using HeartCathAPI.Models;
using HeartCathAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HeartCathAPI.Repositories.Implemntation
{
    public class PatientRepository : IPatientRepository
    {
        private readonly ApplicationDbContext _context;

        public PatientRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Patient>> GetAllAsync(int userId, int page = 1, int pageSize = 10)
        {
            return await _context.Patients
                .Include(p => p.Studies)
                    .ThenInclude(s => s.AnalysisResults)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Patient?> GetByIdAsync(int id)
        {
            return await _context.Patients
                .Include(p => p.Studies)
                    .ThenInclude(s => s.AnalysisResults)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddAsync(Patient patient)
        {
            await _context.Patients.AddAsync(patient);
        }

        public void Update(Patient patient)
        {
            _context.Patients.Update(patient);
        }

        public void Delete(Patient patient)
        {
            if (patient.Studies != null && patient.Studies.Any())
            {
                _context.Studies.RemoveRange(patient.Studies);
            }
            _context.Patients.Remove(patient);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }

}
