using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface IUserRegistrationService
    {
            Task<string> VerifyTokenUniqueness(string token, CancellationToken ct = default);
            Task<ReturnCreateSchoolRegistrarDto> CreateSchoolRegistrar(UserDto2nd model, CancellationToken ct = default);
            Task<bool> CreateTeacherRecord(string userId, UserDto2nd model, CancellationToken ct = default);
            Task<byte> CreatStudent(StudentDetailsDto model, ArmName armName, CurrentClass currentClass, CancellationToken ct = default);
            Task<StudentDetailsDto> GetStudentById(string id, CancellationToken ct = default);
        Task<byte> ValidationChecks(
          string identificationNo,
          CurrentClass currentClass,
          ArmName arm,
          ClassCategoryStatus classCategory,
          CancellationToken ct = default);
        Task<bool> DoesThisUserNameExist(string userName, CancellationToken ct = default);
        Task<bool> IsClassArmAlreadyExist(ArmName armName, string classId, CancellationToken ct = default);
        Task<bool> IsClassAlreadyExist(CurrentClass className, CancellationToken ct = default);
        Task<bool> IsSubjectAlreadyExist(string subjectName, CancellationToken ct = default);
        Task<(string TermId, TermStatus TermStatus)?> GetCurrentTermStatusAndId(CancellationToken ct = default);
        Task<int> GetSchoolSerialId(CancellationToken ct = default);
        Task<string> UpdateStudentDetails(StudentDetailsDto model, ArmName arm, CancellationToken ct = default);
    }
}
