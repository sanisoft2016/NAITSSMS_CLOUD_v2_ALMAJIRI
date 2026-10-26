using NAITSSMS.Domain.Entities;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    internal static class RegistrySchoolLookup
    {
        public static async Task<SchoolTyInfo?> FindSchoolForRegistryUserAsync(
            IUnitOfWork uow,
            string userId,
            CancellationToken ct = default,
            params Expression<Func<SchoolTyInfo, object>>[] includes)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var normalizedUserId = userId.Trim();
            var schoolRepo = uow.Repository<SchoolTyInfo, string>();
            var officerRepo = uow.Repository<SchoolRegistrationOfficer>();

            var directSchool = await schoolRepo.FindAsync(
                entry => entry.SchoolRegistrationOfficerId == normalizedUserId
                    || (entry.SchoolRegistrationOfficer != null && entry.SchoolRegistrationOfficer.UserId == normalizedUserId),
                ct,
                includes);
            if (directSchool != null)
            {
                return directSchool;
            }

            var officer = await officerRepo.FindAsync(entry => entry.UserId == normalizedUserId, ct);
            if (officer != null)
            {
                var schoolByOfficerUserId = await schoolRepo.FindAsync(
                    entry => entry.SchoolRegistrationOfficerId == officer.UserId,
                    ct,
                    includes);
                if (schoolByOfficerUserId != null)
                {
                    return schoolByOfficerUserId;
                }

                var schoolsLinkedToOfficerUser = await schoolRepo.WhereAsync(
                    entry => entry.SchoolRegistrationOfficer != null
                        && entry.SchoolRegistrationOfficer.UserId == normalizedUserId,
                    ct,
                    includes);
                var linkedSchool = schoolsLinkedToOfficerUser.FirstOrDefault();
                if (linkedSchool != null)
                {
                    return linkedSchool;
                }

                // Legacy SignAndWonder stored officer.Id separately from officer.UserId.
                // When imported data still uses legacy Id in SchoolRegistrationOfficerId, resolve via sessions.
                var sessionRepo = uow.Repository<TxAcademicSession, short>();
                var sessionSchools = await sessionRepo.WhereAsync(
                    session => session.SchoolInfo != null
                        && session.SchoolInfo.SchoolRegistrationOfficer != null
                        && session.SchoolInfo.SchoolRegistrationOfficer.UserId == normalizedUserId,
                    ct,
                    session => session.SchoolInfo!);
                var sessionSchool = sessionSchools
                    .Select(session => session.SchoolInfo)
                    .FirstOrDefault(info => info != null);
                if (sessionSchool != null)
                {
                    return sessionSchool;
                }
            }

            var employeeRepo = uow.Repository<TxEmployee>();
            var employee = await employeeRepo.FindAsync(entry => entry.UserId == normalizedUserId, ct);
            if (!string.IsNullOrWhiteSpace(employee?.SchoolId))
            {
                var employeeSchoolId = employee.SchoolId.Trim();
                return await schoolRepo.FindAsync(
                           entry => entry.SchoolRegistrationOfficerId == employeeSchoolId,
                           ct,
                           includes)
                       ?? await schoolRepo.FindAsync(employeeSchoolId, ct);
            }

            var studentRepo = uow.Repository<TxStudentDetail>();
            var student = await studentRepo.FindAsync(entry => entry.UserId == normalizedUserId, ct);
            if (!string.IsNullOrWhiteSpace(student?.SchoolInfoId))
            {
                var studentSchoolId = student.SchoolInfoId.Trim();
                return await schoolRepo.FindAsync(
                           entry => entry.SchoolRegistrationOfficerId == studentSchoolId,
                           ct,
                           includes)
                       ?? await schoolRepo.FindAsync(studentSchoolId, ct);
            }

            var schools = (await schoolRepo.GetAllAsync(ct)).ToList();
            if (schools.Count == 1)
            {
                return schools[0];
            }

            return null;
        }

        public static async Task<string?> ResolveSchoolRegistrationOfficerIdAsync(
            IUnitOfWork uow,
            string userId,
            CancellationToken ct = default)
        {
            var school = await FindSchoolForRegistryUserAsync(uow, userId, ct);
            return string.IsNullOrWhiteSpace(school?.SchoolRegistrationOfficerId)
                ? null
                : school.SchoolRegistrationOfficerId.Trim();
        }
    }
}
