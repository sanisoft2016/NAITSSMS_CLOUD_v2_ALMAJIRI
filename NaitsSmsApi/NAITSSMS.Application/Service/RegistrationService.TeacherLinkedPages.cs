using Microsoft.AspNetCore.Identity;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Application.Mappings;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public partial class RegistrationService : IRegistrationService
    {
        public Task<TeacherAssignmentOptionsDto> GetSubjectAssignmentOptions(string userId, CancellationToken ct = default)
        {
            return GetTeacherAssignmentOptionsCore(userId, ct);
        }

        public Task<TeacherAssignmentOptionsDto> GetClassArmAssignmentOptions(string userId, CancellationToken ct = default)
        {
            return GetTeacherAssignmentOptionsCore(userId, ct);
        }

        public async Task<IList<SelectListItem>> GetSubjectAssignmentSubjects(string userId, string classId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return new List<SelectListItem>();
            }

            var classRepository = _uow.Repository<RxClasss>();
            var classModel = await classRepository.FindAsync(c => c.Id == classId && c.SchoolInfoId == userId, ct);
            if (classModel == null)
            {
                return new List<SelectListItem>
                {
                    new() { Text = "No Subject or all Subjects have been assigned." }
                };
            }

            var teacherSubjectAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var assignedSubjects = await teacherSubjectAssignmentRepository.WhereAsync(
                s => s.Arm != null &&
                     s.Arm.ClassId == classId &&
                     s.Subject != null &&
                     s.Subject.TimeStatus == TimeStatus.Current,
                ct,
                s => s.Subject!,
                s => s.Arm!);

            var subjectPool = new List<SubjectDto>();

            if (classModel.ClassName == CurrentClass.PlayClass)
            {
                subjectPool.AddRange(await BasicClassesSubjectListCurrent(userId, CurrentClass.PlayClass, ct));
            }
            else if (classModel.ClassName == CurrentClass.PreNursery)
            {
                subjectPool.AddRange(await BasicClassesSubjectListCurrent(userId, CurrentClass.PreNursery, ct));
            }
            else if (classModel.ClassName == CurrentClass.Nursery1 || classModel.ClassName == CurrentClass.Nursery2)
            {
                subjectPool.AddRange(await BasicClassesSubjectListCurrent(userId, CurrentClass.Nursery1, ct));
                subjectPool.AddRange(await ElectiveSubjectsCollections(0, userId, ct));
            }
            else if (classModel.ClassName == CurrentClass.Primary1 || classModel.ClassName == CurrentClass.Primary2
                || classModel.ClassName == CurrentClass.Primary3 || classModel.ClassName == CurrentClass.Primary4
                || classModel.ClassName == CurrentClass.Primary5 || classModel.ClassName == CurrentClass.Primary6)
            {
                subjectPool.AddRange(await BasicClassesSubjectListCurrent(userId, CurrentClass.Primary1, ct));
                subjectPool.AddRange(await ElectiveSubjectsCollections(0, userId, ct));
            }
            else if (classModel.ClassName == CurrentClass.Jss1 || classModel.ClassName == CurrentClass.Jss2
                || classModel.ClassName == CurrentClass.Jss3)
            {
                subjectPool.AddRange(await BasicClassesSubjectListCurrent(userId, CurrentClass.Jss1, ct));
                subjectPool.AddRange(await ElectiveSubjectsCollections(0, userId, ct));
            }
            else if (classModel.ClassName == CurrentClass.Ss1 || classModel.ClassName == CurrentClass.Ss2
                || classModel.ClassName == CurrentClass.Ss3)
            {
                // Every stream's subjects are offered, even before any learner is registered in that
                // stream: a school must be able to assign e.g. a Commerce teacher ahead of enrolment.
                subjectPool.AddRange(await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Science, ct));
                subjectPool.AddRange(await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Art, ct));
                subjectPool.AddRange(await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Commercial, ct));

                subjectPool.AddRange(await ElectiveSubjectsCollections(0, userId, ct));
            }

            var uniqueSubjects = subjectPool
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .ToList();

            if (assignedSubjects.Any())
            {
                var studentRepository = _uow.Repository<TxStudentDetail>();
                var totalNoOfArm = (await studentRepository.WhereAsync(
                    s => s.ClassArm != null && s.ClassArm.ClassId == classId,
                    ct,
                    s => s.ClassArm!))
                    .Select(s => s.ClassArmId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                if (totalNoOfArm > 0)
                {
                    foreach (var eachSubject in assignedSubjects.GroupBy(c => c.SubjectId))
                    {
                        if (eachSubject.Count() == totalNoOfArm)
                        {
                            var subjectToRemove = uniqueSubjects.FirstOrDefault(f => f.Id == eachSubject.Key);
                            if (subjectToRemove != null)
                            {
                                uniqueSubjects.Remove(subjectToRemove);
                            }
                        }
                    }
                }
            }

            var result = uniqueSubjects
                .Select(r => new SelectListItem { Text = r.SubjectName, Value = r.Id })
                .ToList();

            if (!result.Any())
            {
                result.Add(new SelectListItem { Text = "No Subject or all Subjects have been assigned." });
            }
            else if (result.Count == 1)
            {
                result.Add(new SelectListItem { Text = "Default", Value = "default" });
            }

            return result;
        }

        public async Task<TeacherAssignmentArmResultDto> GetSubjectAssignmentArms(
            string userId,
            string subjectId,
            string classId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(classId) || subjectId == "default")
            {
                return new TeacherAssignmentArmResultDto();
            }

            var armRepository = _uow.Repository<AxClassArm>();
            var teacherSubjectAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();

            var arms = await armRepository.WhereAsync(
                c => c.ClassId == classId && c.StudentDetails.Any(),
                ct,
                c => c.Classs);

            if (!arms.Any())
            {
                return new TeacherAssignmentArmResultDto { Message = "No Arm in the selected class." };
            }

            var assignedSubjects = await teacherSubjectAssignmentRepository.WhereAsync(
                s => s.Arm.ClassId == classId && s.SubjectId == subjectId,
                ct,
                s => s.Subject,
                s => s.Arm.Classs);

            if (!assignedSubjects.Any())
            {
                return new TeacherAssignmentArmResultDto
                {
                    Arms = arms.Select(MapClassArm).ToList()
                };
            }

            var totalNoOfArm = arms.Count;
            var totalNoOfAssignedArm = assignedSubjects.Count;
            if (totalNoOfAssignedArm == totalNoOfArm)
            {
                return new TeacherAssignmentArmResultDto
                {
                    Message = "All the Subjects has already been assigned to teacher(s)."
                };
            }

            var assignedArmIds = assignedSubjects.Select(r => r.ArmId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new TeacherAssignmentArmResultDto
            {
                Arms = arms.Where(a => !assignedArmIds.Contains(a.Id)).Select(MapClassArm).ToList()
            };
        }

        public async Task<TeacherAssignmentArmResultDto> GetClassArmAssignmentArms(
            string userId,
            string classId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return new TeacherAssignmentArmResultDto();
            }

            var armRepository = _uow.Repository<AxClassArm>();
            var teacherClassAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();

            var classArms = await armRepository.WhereAsync(
                a => a.ClassId == classId && a.StudentDetails.Any(),
                ct,
                a => a.Classs);

            if (!classArms.Any())
            {
                return new TeacherAssignmentArmResultDto();
            }

            var assignedClassArms = await teacherClassAssignmentRepository.WhereAsync(a => a.Arm.ClassId == classId, ct);
            var assignedArmIds = assignedClassArms.Select(n => n.ArmId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new TeacherAssignmentArmResultDto
            {
                Arms = classArms
                    .Where(arm => !assignedArmIds.Contains(arm.Id))
                    .Select(MapClassArm)
                    .ToList()
            };
        }

        public async Task<TeacherAssignmentArmResultDto> AssignSubjectTeacherToArm(
            string userId,
            string teacherId,
            string subjectId,
            string armId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(teacherId) || string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(armId))
            {
                return new TeacherAssignmentArmResultDto();
            }

            var armRepository = _uow.Repository<AxClassArm>();
            var assignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();

            var arm = await armRepository.FindAsync(a => a.Id == armId, ct, a => a.Classs);
            if (arm == null)
            {
                return new TeacherAssignmentArmResultDto();
            }

            var subjectTeacherId = await ResolveEmployeeIdAsync(teacherId, ct);
            if (string.IsNullOrWhiteSpace(subjectTeacherId))
            {
                return new TeacherAssignmentArmResultDto();
            }

            var existingAssignment = await assignmentRepository.FindAsync(
                a => a.ArmId == armId && a.SubjectId == subjectId,
                ct);

            if (existingAssignment == null)
            {
                assignmentRepository.Add(new TeachTxerSubjectArmAssignment
                {
                    Id = Guid.NewGuid().ToString(),
                    ArmId = armId,
                    SubjectId = subjectId,
                    EmployeeId = subjectTeacherId
                });

                await _uow.SaveChangesAsync(ct);
            }

            return await GetSubjectAssignmentArms(userId, subjectId, arm.ClassId, ct);
        }

        public async Task<TeacherAssignmentArmResultDto> AssignClassArmTeacher(
            string userId,
            string teacherId,
            string armId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(teacherId) || string.IsNullOrWhiteSpace(armId))
            {
                return new TeacherAssignmentArmResultDto();
            }

            var armRepository = _uow.Repository<AxClassArm>();
            var teacherClassAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var affectiveAndPsychomotorAssessmentRepository = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();

            var arm = await armRepository.FindAsync(a => a.Id == armId, ct, a => a.Classs);
            if (arm == null)
            {
                return new TeacherAssignmentArmResultDto();
            }

            var formMasterId = await ResolveEmployeeIdAsync(teacherId, ct);
            if (string.IsNullOrWhiteSpace(formMasterId))
            {
                return new TeacherAssignmentArmResultDto();
            }

            var existingAssignment = await teacherClassAssignmentRepository.FindAsync(a => a.ArmId == armId, ct);
            if (existingAssignment == null)
            {
                teacherClassAssignmentRepository.Add(new TeacherClassArmTxAssignment
                {
                    Id = Guid.NewGuid().ToString(),
                    ArmId = armId,
                    EmployeeId = formMasterId
                });
            }

            var assessmentRows = await affectiveAndPsychomotorAssessmentRepository.WhereAsync(
                c => c.ClassArmId == armId && c.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                c => c.YearlyAndTermlyStatus);

            foreach (var each in assessmentRows)
            {
                each.FormMasterId = formMasterId;
                affectiveAndPsychomotorAssessmentRepository.Update(each);
            }

            await _uow.SaveChangesAsync(ct);
            return await GetClassArmAssignmentArms(userId, arm.ClassId, ct);
        }

        public async Task<IList<TeacherUnassignedSubjectRecordDto>> GetSubjectUnAssignedTeachers(
            string userId,
            CancellationToken ct = default)
        {
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var allStudents = await studentDetailRepository.WhereAsync(
                t =>
                    (t.Status == StudentStatus.Fresher || t.Status == StudentStatus.Returning) &&
                    t.SchoolInfoId == userId,
                ct,
                t => t.ClassArm.Classs);

            var returnList = new List<TeacherUnassignedSubjectRecordDto>();

            foreach (var eachClass in allStudents.GroupBy(s => s.ClassArm.ClassId))
            {
                var classStudent = eachClass.FirstOrDefault();
                if (classStudent?.ClassArm?.Classs == null)
                {
                    continue;
                }

                if (classStudent.CurrentClass == CurrentClass.Ss1 || classStudent.CurrentClass == CurrentClass.Ss2 || classStudent.CurrentClass == CurrentClass.Ss3)
                {
                    foreach (var eachDept in eachClass.GroupBy(s => s.ClassCategory))
                    {
                        var classSubjectCollection = eachDept.Key switch
                        {
                            ClassCategoryStatus.Science => await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Science, ct),
                            ClassCategoryStatus.Commercial => await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Commercial, ct),
                            _ => await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Art, ct)
                        };

                        foreach (var eachArm in eachDept.GroupBy(a => a.ClassArmId))
                        {
                            returnList.AddRange(await BuildUnassignedSubjectRows(eachArm, classSubjectCollection, ct));
                        }
                    }
                }
                else
                {
                    var classSubjectCollection = await BasicClassesSubjectListCurrent(userId, classStudent.ClassArm.Classs.ClassName, ct);
                    foreach (var eachArm in eachClass.GroupBy(a => a.ClassArmId))
                    {
                        returnList.AddRange(await BuildUnassignedSubjectRows(eachArm, classSubjectCollection, ct));
                    }
                }
            }

            return returnList;
        }

        public async Task<TeacherCbtUsersDto> GetRegisteredTeachersForCbt(
            string userId,
            string searchParameter = null,
            CancellationToken ct = default)
        {
            var teacherRepository = _uow.Repository<TxEmployee>();
            List<TxEmployee> employees;

            if (string.IsNullOrWhiteSpace(searchParameter))
            {
                employees = await teacherRepository.WhereAsync(
                    e => e.SchoolId == userId,
                    ct,
                    e => e.User);
            }
            else
            {
                var normalized = searchParameter.Trim().ToLower();
                employees = await teacherRepository.WhereAsync(
                    e => e.SchoolId == userId &&
                         (e.User.UserName.Trim().ToLower().Contains(normalized)
                          || e.User.FirstName.Trim().ToLower().Contains(normalized)
                          || e.User.MiddleName.Trim().ToLower().Contains(normalized)
                          || e.User.SurName.Trim().ToLower().Contains(normalized)),
                    ct,
                    e => e.User);
            }

            employees = employees
                .OrderBy(e => BuildFullName(e.User))
                .ThenBy(e => e.User?.UserName)
                .ToList();

            return new TeacherCbtUsersDto
            {
                Employees = employees.Select(MapCbtEmployee).ToList(),
                UserIds = (await GetCbtTeacherUserIdsAsync(employees, ct)).ToList()
            };
        }

        public async Task<TeacherCbtUsersDto> AddUserToCbtRole(string userId, string targetUserId, CancellationToken ct = default)
        {
            var cbtTeacherRoleId = await GetCbtTeacherRoleIdAsync(ct);
            if (string.IsNullOrWhiteSpace(cbtTeacherRoleId) || string.IsNullOrWhiteSpace(targetUserId))
            {
                return await GetRegisteredTeachersForCbt(userId, null, ct);
            }

            var userRoleRepository = _uow.Repository<IdentityUserRole<string>>();
            var existingRole = await userRoleRepository.WhereAsync(
                r => r.UserId == targetUserId && r.RoleId == cbtTeacherRoleId,
                ct);

            if (!existingRole.Any())
            {
                userRoleRepository.Add(new IdentityUserRole<string>
                {
                    UserId = targetUserId,
                    RoleId = cbtTeacherRoleId
                });

                await _uow.SaveChangesAsync(ct);
            }

            return await GetRegisteredTeachersForCbt(userId, null, ct);
        }

        public async Task<TeacherCbtUsersDto> RemoveUserFromCbtRole(string userId, string targetUserId, CancellationToken ct = default)
        {
            var cbtTeacherRoleId = await GetCbtTeacherRoleIdAsync(ct);
            if (string.IsNullOrWhiteSpace(cbtTeacherRoleId) || string.IsNullOrWhiteSpace(targetUserId))
            {
                return await GetRegisteredTeachersForCbt(userId, null, ct);
            }

            var userRoleRepository = _uow.Repository<IdentityUserRole<string>>();
            var existingRoles = await userRoleRepository.WhereAsync(
                r => r.UserId == targetUserId && r.RoleId == cbtTeacherRoleId,
                ct);

            foreach (var eachRole in existingRoles)
            {
                userRoleRepository.Delete(eachRole);
            }

            if (existingRoles.Any())
            {
                await _uow.SaveChangesAsync(ct);
            }

            return await GetRegisteredTeachersForCbt(userId, null, ct);
        }

        private async Task<TeacherAssignmentOptionsDto> GetTeacherAssignmentOptionsCore(string userId, CancellationToken ct)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new TeacherAssignmentOptionsDto();
            }

            var classRepository = _uow.Repository<RxClasss>();
            var teacherRepository = _uow.Repository<TxEmployee>();

            var teachers = await teacherRepository.WhereAsync(
                t =>
                    t.TimeStatus == TimeStatus.Current &&
                    t.SchoolId == schoolId &&
                    t.EmployeeStatus == EmployeeStatus.Teacher,
                ct,
                t => t.User);

            var classes = await classRepository.WhereAsync(s => s.SchoolInfoId == schoolId, ct);

            return new TeacherAssignmentOptionsDto
            {
                Teachers = teachers
                    .OrderBy(t => BuildFullName(t.User))
                    .Select(t => new SelectListItem
                    {
                        Value = t.UserId,
                        Text = BuildFullName(t.User)
                    })
                    .ToList(),
                Classes = classes
                    .OrderBy(c => c.ClassName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id,
                        Text = c.ClassLabel
                    })
                    .ToList()
            };
        }

        private async Task<string> GetCbtTeacherRoleIdAsync(CancellationToken ct)
        {
            var roleRepository = _uow.Repository<IdentityRole>();
            var role = await roleRepository.FindAsync(v => v.Name == "CbtTeacher", ct);
            return role?.Id ?? string.Empty;
        }

        private async Task<IList<string>> GetCbtTeacherUserIdsAsync(IEnumerable<TxEmployee> employees, CancellationToken ct)
        {
            var cbtTeacherRoleId = await GetCbtTeacherRoleIdAsync(ct);
            if (string.IsNullOrWhiteSpace(cbtTeacherRoleId))
            {
                return new List<string>();
            }

            var currentEmployeeUserIds = employees
                .Where(e => e.TimeStatus == TimeStatus.Current && !string.IsNullOrWhiteSpace(e.UserId))
                .Select(e => e.UserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (currentEmployeeUserIds.Count == 0)
            {
                return new List<string>();
            }

            var userRoleRepository = _uow.Repository<IdentityUserRole<string>>();
            var userRoles = await userRoleRepository.WhereAsync(
                r => r.RoleId == cbtTeacherRoleId && currentEmployeeUserIds.Contains(r.UserId),
                ct);

            return userRoles
                .Select(r => r.UserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<string?> ResolveEmployeeIdAsync(string teacherId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(teacherId))
            {
                return null;
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var teacherById = await teacherRepository.FindAsync(t => t.Id == teacherId, ct);
            if (teacherById != null)
            {
                return teacherById.Id;
            }

            var teacherByUserId = await teacherRepository.FindAsync(t => t.UserId == teacherId, ct);
            return teacherByUserId?.Id;
        }

        private async Task<List<TeacherUnassignedSubjectRecordDto>> BuildUnassignedSubjectRows(
            IGrouping<string, TxStudentDetail> eachArm,
            List<SubjectDto> classSubjectCollection,
            CancellationToken ct)
        {
            var classSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var assignments = await classSubjectArmAssignmentRepository.WhereAsync(a => a.ArmId == eachArm.Key, ct);
            var assignedSubjectIds = assignments
                .Select(a => a.SubjectId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var currentArm = eachArm.FirstOrDefault()?.ClassArm;
            var currentClass = currentArm?.Classs;
            if (currentArm == null || currentClass == null)
            {
                return new List<TeacherUnassignedSubjectRecordDto>();
            }

            return classSubjectCollection
                .Where(subject => !string.IsNullOrWhiteSpace(subject.Id))
                .GroupBy(subject => subject.Id)
                .Select(group => group.First())
                .Where(subject => !assignedSubjectIds.Contains(subject.Id))
                .Select(subject => new TeacherUnassignedSubjectRecordDto
                {
                    SubjectName = subject.SubjectName,
                    ClassName = currentClass.ClassName,
                    ClassLabel = currentClass.ClassLabel,
                    ArmName = currentArm.ArmName
                })
                .ToList();
        }

        private static ClassArmDto MapClassArm(AxClassArm arm)
        {
            return new ClassArmDto
            {
                Id = arm.Id,
                SNo = arm.SNo,
                ClassId = arm.ClassId,
                ArmName = arm.ArmName,
                FormMaster = arm.FormMaster,
                Classs = arm.Classs == null
                    ? null
                    : new ClassDto
                    {
                        Id = arm.Classs.Id,
                        SNo = arm.Classs.SNo,
                        ClassName = arm.Classs.ClassName,
                        ClassLabel = arm.Classs.ClassLabel,
                        SchoolInfoId = arm.Classs.SchoolInfoId
                    }
            };
        }

        private static TeacherCbtUserRecordDto MapCbtEmployee(TxEmployee employee)
        {
            return new TeacherCbtUserRecordDto
            {
                Id = employee.Id,
                EmployeeIdNo = employee.EmployeeIdNo,
                EmployeeStatus = employee.EmployeeStatus.ToString(),
                TimeStatus = employee.TimeStatus,
                UserId = employee.UserId,
                UserName = employee.User?.UserName ?? string.Empty,
                FullName = BuildFullName(employee.User),
                PhoneNumber = employee.User?.PhoneNumber ?? string.Empty
            };
        }

        private static string BuildFullName(ApplicationUser? user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            var names = new[] { user.FirstName, user.MiddleName, user.SurName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            return names.Length == 0 ? user.UserName : string.Join(" ", names);
        }

        private static SubjectDto MapSubject(RxSubject subject)
        {
            return new SubjectDto
            {
                Id = subject.Id,
                SubjectName = subject.SubjectName,
                TimeStatus = subject.TimeStatus,
                SubjectCategoryId = subject.SubjectCategoryId,
                SubjectCategory = subject.SubjectCategory == null
                    ? new SubjectCategoriesDto()
                    : new SubjectCategoriesDto
                    {
                        Id = short.TryParse(subject.SubjectCategory.Id, out var categoryId) ? categoryId : (short)0,
                        SubjectStatus = subject.SubjectCategory.SubjectStatus,
                        SubjectStatusLabel = subject.SubjectCategory.SubjectStatusLabel ?? string.Empty,
                        SchoolId = subject.SubjectCategory.SchoolId ?? string.Empty,
                    },
            };
        }

        public async Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetTeacherAssignedSubjectsByTeacherId(
            string userId,
            string selectedTeacherId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(selectedTeacherId))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var teacher = await teacherRepository.FindAsync(
                entry => entry.UserId == selectedTeacherId && entry.SchoolId == schoolId,
                ct);

            if (teacher == null)
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var assignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var assignments = await assignmentRepository.WhereAsync(
                entry => entry.EmployeeId == teacher.Id,
                ct,
                entry => entry.Employee!.User,
                entry => entry.Subject!,
                entry => entry.Arm!.Classs);

            return assignments
                .Select(TeacherAssignmentMapping.MapSubjectArmAssignment)
                .OrderBy(entry => entry.Subject?.SubjectName)
                .ThenBy(entry => entry.ClassArmLabel)
                .ToList();
        }

        public async Task<bool> TransferSubjectTeacherSubjectAssignment(
            string userId,
            IList<SubjectIdsBto> subjectIds,
            CancellationToken ct = default)
        {
            if (subjectIds == null || subjectIds.Count < 2)
            {
                return false;
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return false;
            }

            var proposedTeacherUserId = subjectIds.Last().Id?.Trim();
            if (string.IsNullOrWhiteSpace(proposedTeacherUserId))
            {
                return false;
            }

            var assignmentIds = subjectIds
                .Take(subjectIds.Count - 1)
                .Select(entry => entry.Id?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            if (assignmentIds.Count == 0)
            {
                return false;
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var proposedTeacher = await teacherRepository.FindAsync(
                teacher => teacher.UserId == proposedTeacherUserId && teacher.SchoolId == schoolId,
                ct);

            if (proposedTeacher == null)
            {
                return false;
            }

            var assignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var assessmentRepository = _uow.Repository<StudenXtAccessment>();
            var updated = false;

            foreach (var assignmentId in assignmentIds)
            {
                var assignment = await assignmentRepository.FindAsync(
                    entry =>
                        entry.Id == assignmentId &&
                        entry.Employee != null &&
                        entry.Employee.SchoolId == schoolId,
                    ct,
                    entry => entry.Employee!);
                if (assignment == null)
                {
                    continue;
                }

                // Detach the included Employee navigation before Update: the query is
                // AsNoTracking, so every assignment for the current teacher carries its own
                // detached copy of the same Employee. Attaching more than one would throw a
                // duplicate-key tracking conflict. The EmployeeId FK scalar is preserved.
                assignment.Employee = null;
                assignment.EmployeeId = proposedTeacher.Id;
                assignmentRepository.Update(assignment);
                updated = true;

                var assessments = await assessmentRepository.WhereAsync(
                    entry =>
                        entry.ClassArmId == assignment.ArmId &&
                        entry.SubjectId == assignment.SubjectId &&
                        entry.YearlyAndTermlyStatus != null &&
                        entry.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    ct,
                    entry => entry.YearlyAndTermlyStatus!);

                foreach (var assessment in assessments)
                {
                    // Same reason: many assessments share one TxYearlyAndTermlyStatus. Null the
                    // included navigation so Attach doesn't double-track it (the
                    // YearlyAndTermlyStatusId FK scalar is preserved).
                    assessment.YearlyAndTermlyStatus = null;
                    assessment.EmployeeId = proposedTeacher.Id;
                    assessmentRepository.Update(assessment);
                }
            }

            if (!updated)
            {
                return false;
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<IEnumerable<TeacherClassArmAssignmentDto>> GetTeacherAssignedArmsByTeacherId(
            string userId,
            string selectedTeacherId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(selectedTeacherId))
            {
                return Enumerable.Empty<TeacherClassArmAssignmentDto>();
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Enumerable.Empty<TeacherClassArmAssignmentDto>();
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var teacher = await teacherRepository.FindAsync(
                entry => entry.UserId == selectedTeacherId && entry.SchoolId == schoolId,
                ct);

            if (teacher == null)
            {
                return Enumerable.Empty<TeacherClassArmAssignmentDto>();
            }

            var assignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var assignments = await assignmentRepository.WhereAsync(
                entry => entry.EmployeeId == teacher.Id,
                ct,
                entry => entry.Employee!.User,
                entry => entry.Arm!.Classs);

            return assignments
                .Select(TeacherAssignmentMapping.MapClassArmAssignment)
                .OrderBy(entry => entry.ClassArmLabel)
                .ToList();
        }

        public async Task<bool> TransferArmTeacherArmAssignment(
            string userId,
            IList<SubjectIdsBto> subjectIds,
            CancellationToken ct = default)
        {
            if (subjectIds == null || subjectIds.Count < 2)
            {
                return false;
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return false;
            }

            var proposedTeacherUserId = subjectIds.Last().Id?.Trim();
            if (string.IsNullOrWhiteSpace(proposedTeacherUserId))
            {
                return false;
            }

            var assignmentIds = subjectIds
                .Take(subjectIds.Count - 1)
                .Select(entry => entry.Id?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            if (assignmentIds.Count == 0)
            {
                return false;
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var proposedTeacher = await teacherRepository.FindAsync(
                teacher => teacher.UserId == proposedTeacherUserId && teacher.SchoolId == schoolId,
                ct);

            if (proposedTeacher == null)
            {
                return false;
            }

            var assignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var psychoRepository = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var updated = false;
            string? currentTeacherEmployeeId = null;

            foreach (var assignmentId in assignmentIds)
            {
                var assignment = await assignmentRepository.FindAsync(
                    entry =>
                        entry.Id == assignmentId &&
                        entry.Employee != null &&
                        entry.Employee.SchoolId == schoolId,
                    ct,
                    entry => entry.Employee!);
                if (assignment == null)
                {
                    continue;
                }

                currentTeacherEmployeeId = assignment.EmployeeId;
                // Detach the included Employee navigation (AsNoTracking gives each assignment its
                // own detached copy of the same teacher) so Attach doesn't double-track it.
                assignment.Employee = null;
                assignment.EmployeeId = proposedTeacher.Id;
                assignmentRepository.Update(assignment);
                updated = true;
            }

            if (!updated)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(currentTeacherEmployeeId))
            {
                var psychoRows = await psychoRepository.WhereAsync(
                    entry =>
                        entry.FormMasterId == currentTeacherEmployeeId &&
                        entry.YearlyAndTermlyStatus != null &&
                        entry.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    ct,
                    entry => entry.YearlyAndTermlyStatus!);

                foreach (var psychoRow in psychoRows)
                {
                    // Null the shared TxYearlyAndTermlyStatus navigation so Attach doesn't
                    // double-track it (the YearlyAndTermlyStatusId FK scalar is preserved).
                    psychoRow.YearlyAndTermlyStatus = null;
                    psychoRow.FormMasterId = proposedTeacher.Id;
                    psychoRepository.Update(psychoRow);
                }
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }
    }
}