using HeartCathAPI.Areas.Doctor.DTOs;
using HeartCathAPI.Models;
using HeartCathAPI.Repositories.Interfaces;
using HeartCathAPI.Services.Interfaces;

namespace HeartCathAPI.Services.PatientsServices;

public class PatientService : IPatientService
{
        private readonly IPatientRepository _repo;

        public PatientService(IPatientRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<PatientRequest>> GetAllAsync(int userId, int page = 1, int pageSize = 10)
        {
            var patients = await _repo.GetAllAsync(userId, page, pageSize);

            return patients.Select(p => new PatientRequest
            {
                Id = p.Id,
                FullName = p.FullName,
                Age = p.Age,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                PhoneNumber = p.PhoneNumber,
                MedicalRecordNumber = p.MedicalRecordNumber,
                Notes = p.Notes,
                ChronicDiseases = p.ChronicDiseases,
                CreatedAt = p.CreatedAt,
                Studies = p.Studies.Select(s => new StudyDto
                {
                    Id = s.Id,
                    FilePath = s.FilePath,
                    Status = s.Status.ToString(),
                    UploadDate = s.UploadDate,
                    AnalysisResults = s.AnalysisResults.Select(a => new AnalysisResultDto
                    {
                        Id = a.Id,
                        StenosisPercentage = a.StenosisPercentage,
                        RiskLevel = a.RiskLevel,
                        ImagePath = a.ImagePath,
                        ArteryName = a.ArteryName
                    }).ToList()
                }).ToList()
            });
        }

        public async Task<PatientRequest?> GetByIdAsync(int id)
        {
            var patient = await _repo.GetByIdAsync(id);

            if (patient == null)
                return null;

            return new PatientRequest
            {
                Id = patient.Id,
                FullName = patient.FullName,
                Age = patient.Age,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                PhoneNumber = patient.PhoneNumber,
                MedicalRecordNumber = patient.MedicalRecordNumber,
                Notes = patient.Notes,
                ChronicDiseases = patient.ChronicDiseases,
                CreatedAt = patient.CreatedAt,
                Studies = patient.Studies.Select(s => new StudyDto
                {
                    Id = s.Id,
                    FilePath = s.FilePath,
                    Status = s.Status.ToString(),
                    UploadDate = s.UploadDate,
                    AnalysisResults = s.AnalysisResults.Select(a => new AnalysisResultDto
                    {
                        Id = a.Id,
                        StenosisPercentage = a.StenosisPercentage,
                        RiskLevel = a.RiskLevel,
                        ImagePath = a.ImagePath,
                        ArteryName = a.ArteryName
                    }).ToList()
                }).ToList()
            };
        }

        public async Task<PatientRequest> CreateAsync(int userId, CreatePatientRequest dto)
        {
            var patient = new Patient
            {
                UserId = userId,
                FullName = dto.FullName,
                Age = dto.Age,
                DateOfBirth = dto.DateOfBirth,
                Gender = dto.Gender,
                PhoneNumber = dto.PhoneNumber,
                MedicalRecordNumber = dto.MedicalRecordNumber,
                Notes = dto.Notes,
                ChronicDiseases = dto.ChronicDiseases
            };

            await _repo.AddAsync(patient);
            await _repo.SaveAsync();

            return new PatientRequest
            {
                Id = patient.Id,
                FullName = patient.FullName,
                Age = patient.Age,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                PhoneNumber = patient.PhoneNumber,
                MedicalRecordNumber = patient.MedicalRecordNumber,
                Notes = patient.Notes,
                ChronicDiseases = patient.ChronicDiseases,
                CreatedAt = patient.CreatedAt
            };
        }

        public async Task<bool> UpdateAsync(int id, UpdatePatientRequest dto)
        {
            var patient = await _repo.GetByIdAsync(id);

            if (patient == null)
                return false;

            patient.FullName = dto.FullName ?? patient.FullName;
            if (dto.Age.HasValue) patient.Age = dto.Age;
            if (dto.DateOfBirth.HasValue) patient.DateOfBirth = dto.DateOfBirth;
            if (dto.Gender != null) patient.Gender = dto.Gender;
            if (dto.PhoneNumber != null) patient.PhoneNumber = dto.PhoneNumber;
            if (dto.MedicalRecordNumber != null) patient.MedicalRecordNumber = dto.MedicalRecordNumber;
            if (dto.Notes != null) patient.Notes = dto.Notes;
            if (dto.ChronicDiseases != null) patient.ChronicDiseases = dto.ChronicDiseases;

            _repo.Update(patient);
            await _repo.SaveAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var patient = await _repo.GetByIdAsync(id);

            if (patient == null)
                return false;

            _repo.Delete(patient);
            await _repo.SaveAsync();

            return true;
        }
}