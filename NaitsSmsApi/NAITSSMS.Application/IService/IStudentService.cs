using System.Threading;
using System.Threading.Tasks;

using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.IService
{
    public interface IStudentService
    {
        Task<string> GetLastStudentAdminssionNumber(CancellationToken ct = default);

        Task<PrepareEnvironmentForStudentAssessmentReturnDto> PrepareAssessmentEnvironmentForTheWholeStudents(
            byte? requestStatus,
            CancellationToken ct = default);
        Task<IEnumerable<StudentDetailsDto>> GetStudentByAdmissionNo(
    string searchParameter,
    CancellationToken ct = default);
        Task<List<StudentDetailsDto>> GetAllStudents(string schoolId, CancellationToken ct = default);
        Task<List<StudentDetailsDto>> GetStudentsByParameter(
           string searchParameter,
           CancellationToken ct = default);
        Task DistributeStudentEvenly(CancellationToken ct = default);
        Task<bool> DeleteStudent(string id, CancellationToken ct = default);
        Task<bool> AlterStudentStatusToPrevious(string id, CancellationToken ct = default);
        Task<IList<StudentDetailsDto>> GetRecentlySavedStudents(CancellationToken ct = default);
        Task<StudentDetailsDto> GetStudentById(string id, CancellationToken ct = default);
        Task<IList<StudentDetailsDto>> GetStudentForAnArm(string armId, CancellationToken ct = default);
        Task<IList<StudentDetailsDto>> GetStudentsWithoutPassportByClassLabel(
            string classLabel,
            CancellationToken ct = default);

        Task<int> GetSchoolSerialId(CancellationToken ct = default);
        Task<byte> CreatStudent(StudentDetailsDto model, CancellationToken ct = default);
    }
}
