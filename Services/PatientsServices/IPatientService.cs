using HeartCathAPI.Areas.Doctor.DTOs;

namespace HeartCathAPI.Services.Interfaces
{
    public interface IPatientService
    {
        Task<IEnumerable<PatientRequest>> GetAllAsync(int userId, int page = 1, int pageSize = 10);
        Task<PatientRequest?> GetByIdAsync(int id);
        Task<PatientRequest> CreateAsync(int userId, CreatePatientRequest dto);
        Task<bool> UpdateAsync(int id, UpdatePatientRequest dto);
        Task<bool> DeleteAsync(int id);
    }
   


}
