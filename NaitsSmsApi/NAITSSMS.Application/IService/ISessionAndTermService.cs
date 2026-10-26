using NAITSSMS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface ISessionAndTermService
    {
        Task<SessionsAndTerm> GetAcademicSessions(string userId, CancellationToken ct = default);
        Task<bool> CreateAcademicSession(string userId, AcademicSessionDto model, CancellationToken ct = default);
        Task<bool> UpdateAcademicSessionInfo(string userId, AcademicSessionDto model, CancellationToken ct = default);
        Task<bool> DeleteAcademicSession(string sessionId, CancellationToken ct = default);
        Task<IEnumerable<YearlyAndTermlyStatusDto>> GetTermsBySession(string sessionId, CancellationToken ct = default);
        Task<IList<SelectListItem>> GetSessionsDropdown(string userId, CancellationToken ct = default);
        Task<IList<SelectListItem>> GetTermsDropdownForSession(string sessionId, CancellationToken ct = default);
        Task<PrepareEnvironmentForStudentAssessmentReturnDto> PrepareAssessmentEnvironmentForTheWholeStudents(
           string userId,
           byte? requestStatus,
           CancellationToken ct = default);

        Task<PrepareEnvironmentForStudentAssessmentReturnDto> PrepareAssessmentEnvironmentForStudentsAsync(
            string schoolId,
            IReadOnlyList<Domain.Entities.TxStudentDetail> students,
            byte? requestStatus,
            CancellationToken ct = default);
    }
}
