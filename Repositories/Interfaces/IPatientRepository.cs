using HeartCathAPI.Models;

namespace HeartCathAPI.Repositories.Interfaces
{
    public interface IPatientRepository
    {
        Task<IEnumerable<Patient>> GetAllAsync(int userId, int page = 1, int pageSize = 10);
        Task<Patient?> GetByIdAsync(int id);
        Task AddAsync(Patient patient);
        void Update(Patient patient);
        void Delete(Patient patient);
        Task SaveAsync();
    }
}
