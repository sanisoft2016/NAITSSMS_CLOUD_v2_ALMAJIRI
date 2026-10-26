using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface ISubjectService
    {
        Task<IList<SubjectCategoriesDto>> GetSchoolLabel(
            string userId,
            CancellationToken ct = default);

        Task<bool> UpdateSchoolLabel(
            string userId,
            SchoolLevelCategoriesLabelDto model,
            CancellationToken ct = default);

        Task<IList<SubjectDto>> GetSubjects(
            string userId,
            SubjectStatus subjectStatus,
            CancellationToken ct = default);

        Task<bool> ValidateSubjectNameForCreate(
            string userId,
            string subjectName,
            SubjectStatus subjectStatus,
            CancellationToken ct = default);

        Task<bool> ValidateSubjectNameForUpdate(
            string userId,
            string subjectName,
            string id,
            SubjectStatus subjectStatus,
            CancellationToken ct = default);

        Task<bool> CreatClassSubject(
            string userId,
            SubjectDto model,
            SubjectStatus subjectStatus,
            CancellationToken ct = default);

        Task<bool> UpdateSubject(
            string userId,
            SubjectDto model,
            SubjectStatus subjectStatus,
            CancellationToken ct = default);

        Task<bool> DeleteSubject(
            string userId,
            string id,
            CancellationToken ct = default);

        Task<bool> IsRequestToUpdateSubjectTimeStatus(
            string userId,
            string id,
            TimeStatus timeStatus,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> GetSubjectForDropDown(
            string userId,
            CancellationToken ct = default);

        Task<IList<SubjectDto>> GetLevelsThatOfferThisSubject(
            string userId,
            string subjectName,
            CancellationToken ct = default);

        Task<IList<SubjectDto>> SearchLevelsThatOfferThisSubject(
            string userId,
            string searchParameter,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> ClassDropDown(
            string userId,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> ArmDropDown(
            string userId,
            string selectedClassId,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> ElectiveSubjectDropDown(
            string userId,
            CancellationToken ct = default);

        Task<ArmOrCategoryStudentsInTheSelectedArmDto> GetCategoryStudentsInTheSelectedArm(
            string userId,
            byte catStatus,
            string armId,
            string subjectId,
            CancellationToken ct = default);

        Task<ArmOrCategoryStudentsInTheSelectedArmDto> GetStudentForAnArm2Nd(
            string userId,
            string armId,
            string subjectId,
            CancellationToken ct = default);

        Task<string> AssignSubjectToArmStudentsOrStudentsInCategory(
            string userId,
            List<StudentIdsDto> studentIds,
            CancellationToken ct = default);

        Task<bool> UnAssignSubjectToArmStudentsOrStudentsInCategory(
            string userId,
            List<StudentIdsDto> studentIds,
            CancellationToken ct = default);
    }
}
