using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Application.Mappings;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public partial class RegistrationService : IRegistrationService
    {
        public async Task<IEnumerable<EmployeeDto>> GetAllTeachers(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<EmployeeDto>();
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var teachers = await teacherRepository.WhereAsync(u => u.SchoolId == schoolId, ct, u => u.User);
            return _mapper.Map<IEnumerable<EmployeeDto>>(teachers ?? Enumerable.Empty<TxEmployee>());
        }

        public async Task<IEnumerable<TeacherClassArmAssignmentDto>> GetClassTeachers(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<TeacherClassArmAssignmentDto>();
            }

            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var teacherClassArmAssignments = await teacherClassArmAssignmentRepository.WhereAsync(
                u => u.Employee != null && u.Employee.SchoolId == schoolId,
                ct,
                u => u.Employee!.User,
                r => r.Arm!.Classs!);

            return (teacherClassArmAssignments ?? Enumerable.Empty<TeacherClassArmTxAssignment>())
                .Select(TeacherAssignmentMapping.MapClassArmAssignment)
                .ToList();
        }

        public async Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetSubjectTeachers(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var teacherSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var teacherSubjectArmAssignments = await teacherSubjectArmAssignmentRepository.WhereAsync(
                u => u.Employee.SchoolId == schoolId,
                ct,
                u => u.Employee.User,
                r => r.Arm.Classs,
                r => r.Subject);

            return (teacherSubjectArmAssignments ?? Enumerable.Empty<TeachTxerSubjectArmAssignment>())
                .Select(TeacherAssignmentMapping.MapSubjectArmAssignment)
                .ToList();
        }

        public async Task<IEnumerable<EmployeeDto>> GetTeacherByUserName(string userId, string searchParameter, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchParameter))
            {
                return Enumerable.Empty<EmployeeDto>();
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<EmployeeDto>();
            }

            var normalized = searchParameter.Trim().ToLower();
            var teacherRepository = _uow.Repository<TxEmployee>();
            var teachers = await teacherRepository.WhereAsync(
                t =>
                    t.SchoolId == schoolId &&
                    (t.User.UserName.Trim().ToLower().Contains(normalized)
                     || t.User.FirstName.Trim().ToLower().Contains(normalized)
                     || t.User.MiddleName.Trim().ToLower().Contains(normalized)
                     || t.User.SurName.Trim().ToLower().Contains(normalized)),
                ct,
                t => t.User);

            return _mapper.Map<List<EmployeeDto>>(teachers ?? Enumerable.Empty<TxEmployee>());
        }

        public async Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetSubjectTeacherByUserName(string userId, string searchParameter, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchParameter))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var normalized = searchParameter.Trim().ToLower();
            var teacherSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var teacherSubjectArmAssignments = await teacherSubjectArmAssignmentRepository.WhereAsync(
                u =>
                    u.Employee.SchoolId == schoolId &&
                    (u.Employee.User.UserName.Trim().ToLower().Contains(normalized)
                     || u.Employee.User.FirstName.Trim().ToLower().Contains(normalized)
                     || u.Employee.User.MiddleName.Trim().ToLower().Contains(normalized)
                     || u.Employee.User.SurName.Trim().ToLower().Contains(normalized)),
                ct,
                u => u.Employee.User,
                r => r.Arm.Classs,
                r => r.Subject);

            return (teacherSubjectArmAssignments ?? Enumerable.Empty<TeachTxerSubjectArmAssignment>())
                .Select(TeacherAssignmentMapping.MapSubjectArmAssignment)
                .ToList();
        }

        public async Task<IEnumerable<TeacherClassArmAssignmentDto>> GetFormMasterByUserName(string userId, string searchParameter, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchParameter))
            {
                return Enumerable.Empty<TeacherClassArmAssignmentDto>();
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<TeacherClassArmAssignmentDto>();
            }

            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var teacherClassArmAssignments = await teacherClassArmAssignmentRepository.WhereAsync(
                u =>
                    u.Employee != null &&
                    u.Employee.SchoolId == schoolId &&
                    (u.Employee.User.UserName.Trim().ToLower().Contains(searchParameter)
                     || u.Employee.User.FirstName.Trim().ToLower().Contains(searchParameter)
                     || u.Employee.User.MiddleName.Trim().ToLower().Contains(searchParameter)
                     || u.Employee.User.SurName.Trim().ToLower().Contains(searchParameter)),
                ct,
                u => u.Employee!.User,
                r => r.Arm!.Classs!);

            return (teacherClassArmAssignments ?? Enumerable.Empty<TeacherClassArmTxAssignment>())
                .Select(TeacherAssignmentMapping.MapClassArmAssignment)
                .ToList();
        }

        public async Task<IEnumerable<TeacherSubjectArmAssignmentDto>> UnAssignSubjectTeacher(string userId, string id, CancellationToken ct = default)
        {
            var teacherSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            await teacherSubjectArmAssignmentRepository.DeleteAsync(id, ct);
            await _uow.SaveChangesAsync(ct);
            return await GetSubjectTeachers(userId, ct);
        }

        public async Task<IEnumerable<TeacherClassArmAssignmentDto>> UnAssignClassTeacher(string userId, string id, CancellationToken ct = default)
        {
            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            await teacherClassArmAssignmentRepository.DeleteAsync(id, ct);
            await _uow.SaveChangesAsync(ct);
            return await GetClassTeachers(userId, ct);
        }

        public async Task<bool> EnableUserById(string id, CancellationToken ct = default)
        {
            var teacherRepository = _uow.Repository<TxEmployee>();
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var teacher = await teacherRepository.FindAsync(i => i.Id == id, ct, s => s.User);
            var userModel = teacher?.User;

            if (userModel == null)
            {
                return false;
            }

            userModel.AccessStatus = AccessStatus.Active;
            userRepository.Update(userModel);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DisableUserById(string id, CancellationToken ct = default)
        {
            var teacherRepository = _uow.Repository<TxEmployee>();
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var teacher = await teacherRepository.FindAsync(i => i.Id == id, ct, s => s.User);
            var userModel = teacher?.User;

            if (userModel == null)
            {
                return false;
            }

            userModel.AccessStatus = AccessStatus.Disable;
            userRepository.Update(userModel);
            await _uow.SaveChangesAsync(ct);
            return true;
        }
    }
}