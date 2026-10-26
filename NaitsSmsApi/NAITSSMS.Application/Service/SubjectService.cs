using Microsoft.Extensions.Caching.Memory;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class SubjectService : ISubjectService
    {
        private static readonly SubjectStatus[] ManagedSubjectStatuses =
        {
            SubjectStatus.PlayClass,
            SubjectStatus.PreNursery,
            SubjectStatus.Nursery,
            SubjectStatus.General,
            SubjectStatus.Primary,
            SubjectStatus.Jss,
            SubjectStatus.AllSeniors,
            SubjectStatus.Science,
            SubjectStatus.Art,
            SubjectStatus.Commercial,
            SubjectStatus.Elective,
        };

        private static readonly TimeSpan SubjectListCacheDuration = TimeSpan.FromSeconds(45);

        private readonly IUnitOfWork _uow;
        private readonly ISchoolScopeResolver _schoolScopeResolver;
        private readonly IMemoryCache _cache;

        public SubjectService(IUnitOfWork uow, ISchoolScopeResolver schoolScopeResolver, IMemoryCache cache)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        private async Task<string> ResolveSchoolIdAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return string.Empty;
            }

            return await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
        }

        public async Task<IList<SubjectCategoriesDto>> GetSchoolLabel(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SubjectCategoriesDto>();
            }

            var subjectCategoryRepository = _uow.Repository<SubjectRxCategories>();
            var categories = await subjectCategoryRepository.WhereAsync(category => category.SchoolId == schoolId, ct);
            var existingStatuses = categories.Select(category => category.SubjectStatus).ToHashSet();
            var missingStatuses = ManagedSubjectStatuses.Where(status => !existingStatuses.Contains(status)).ToArray();

            if (missingStatuses.Length > 0)
            {
                foreach (var status in missingStatuses)
                {
                    subjectCategoryRepository.Add(new SubjectRxCategories
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubjectStatus = status,
                        SchoolId = schoolId,
                        SubjectStatusLabel = string.Empty,
                    });
                }

                await _uow.SaveChangesAsync(ct);
                categories = await subjectCategoryRepository.WhereAsync(category => category.SchoolId == schoolId, ct);
            }

            return categories
                .OrderBy(category => GetSubjectCategoryOrder(category.SubjectStatus))
                .Select(MapSubjectCategory)
                .ToList();
        }

        public async Task<bool> UpdateSchoolLabel(string userId, SchoolLevelCategoriesLabelDto model, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || model == null)
            {
                return false;
            }

            await EnsureDefaultSubjectCategoriesAsync(schoolId, ct);

            var subjectCategoryRepository = _uow.Repository<SubjectRxCategories>();
            var categories = await subjectCategoryRepository.WhereAsync(category => category.SchoolId == schoolId, ct);

            foreach (var category in categories)
            {
                category.SubjectStatusLabel = category.SubjectStatus switch
                {
                    SubjectStatus.General => model.GeneralSujectLabel,
                    SubjectStatus.PlayClass => model.PlayClassLabel,
                    SubjectStatus.PreNursery => model.PreNurseryLabel,
                    SubjectStatus.Nursery => model.NurseryLabel,
                    SubjectStatus.Primary => model.PrimaryLabel,
                    SubjectStatus.Jss => model.JssLabel,
                    SubjectStatus.AllSeniors => model.SsLabel,
                    SubjectStatus.Elective => model.ElectiveSubjectLabel,
                    _ => category.SubjectStatusLabel,
                };

                subjectCategoryRepository.Update(category);
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<IList<SubjectDto>> GetSubjects(string userId, SubjectStatus subjectStatus, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SubjectDto>();
            }

            var cacheKey = BuildSubjectListCacheKey(schoolId, subjectStatus);
            if (_cache.TryGetValue(cacheKey, out IList<SubjectDto>? cached) && cached != null)
            {
                return cached;
            }

            // Use indexed lookups: (SchoolId, SubjectStatus) → category Id, then SubjectCategoryId → subjects.
            var subjectCategoryRepository = _uow.Repository<SubjectRxCategories>();
            var category = await subjectCategoryRepository.FindAsync(
                item => item.SchoolId == schoolId && item.SubjectStatus == subjectStatus,
                ct);

            if (category == null || string.IsNullOrWhiteSpace(category.Id))
            {
                var empty = (IList<SubjectDto>)new List<SubjectDto>();
                _cache.Set(cacheKey, empty, SubjectListCacheDuration);
                return empty;
            }

            var categoryId = category.Id;
            var subjectRepository = _uow.Repository<RxSubject>();
            var subjects = await subjectRepository.SelectWhereAsync(
                subject => subject.SubjectCategoryId == categoryId,
                subject => new SubjectDto
                {
                    Id = subject.Id,
                    SubjectName = subject.SubjectName,
                    TimeStatus = subject.TimeStatus,
                    SubjectCategoryId = subject.SubjectCategoryId,
                },
                ct);

            var ordered = (IList<SubjectDto>)subjects
                .OrderBy(subject => subject.SubjectName)
                .ToList();

            _cache.Set(cacheKey, ordered, SubjectListCacheDuration);
            return ordered;
        }

        public async Task<bool> ValidateSubjectNameForCreate(string userId, string subjectName, SubjectStatus subjectStatus, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(subjectName))
            {
                return false;
            }

            var duplicateStatuses = (await FindSubjectsByNameAsync(schoolId, subjectName, ct))
                .Select(subject => subject.SubjectStatus)
                .ToList();

            return CanCreateSubjectForStatus(subjectStatus, duplicateStatuses);
        }

        public async Task<bool> ValidateSubjectNameForUpdate(
            string userId,
            string subjectName,
            string id,
            SubjectStatus subjectStatus,
            CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(subjectName) || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            var duplicateStatuses = (await FindSubjectsByNameAsync(schoolId, subjectName, ct))
                .Where(subject => !string.Equals(subject.Id, id, StringComparison.OrdinalIgnoreCase))
                .Select(subject => subject.SubjectStatus)
                .ToList();

            return CanUpdateSubjectForStatus(subjectStatus, duplicateStatuses);
        }

        public async Task<bool> CreatClassSubject(string userId, SubjectDto model, SubjectStatus subjectStatus, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || model == null || string.IsNullOrWhiteSpace(model.SubjectName))
            {
                return false;
            }

            if (!await ValidateSubjectNameForCreate(userId, model.SubjectName, subjectStatus, ct))
            {
                return false;
            }

            var categoryId = await EnsureSubjectCategoryIdAsync(schoolId, subjectStatus, ct);
            if (string.IsNullOrWhiteSpace(categoryId))
            {
                return false;
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            subjectRepository.Add(new RxSubject
            {
                Id = Guid.NewGuid().ToString(),
                SubjectName = NormalizeSubjectNameForPersistence(model.SubjectName),
                TimeStatus = model.TimeStatus,
                SubjectCategoryId = categoryId,
            });

            await _uow.SaveChangesAsync(ct);
            InvalidateSubjectListCache(schoolId, subjectStatus);
            return true;
        }

        public async Task<bool> UpdateSubject(string userId, SubjectDto model, SubjectStatus subjectStatus, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return false;
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            var subject = await subjectRepository.FindAsync(
                item => item.Id == model.Id
                    && item.SubjectCategory != null
                    && item.SubjectCategory.SchoolId == schoolId,
                ct,
                item => item.SubjectCategory!);

            if (subject == null)
            {
                return false;
            }

            if (model.TimeStatus == TimeStatus.Previous)
            {
                await RemoveSubjectAssignmentArtifactsAsync(model.Id, ct);
            }

            subject.SubjectName = NormalizeSubjectNameForPersistence(model.SubjectName);
            subject.SubjectCategoryId = !string.IsNullOrWhiteSpace(model.SubjectCategoryId)
                ? model.SubjectCategoryId
                : await EnsureSubjectCategoryIdAsync(schoolId, subjectStatus, ct);
            subject.TimeStatus = model.TimeStatus;

            subjectRepository.Update(subject);
            await _uow.SaveChangesAsync(ct);
            InvalidateSubjectListCache(schoolId, subjectStatus);
            if (subject.SubjectCategory != null)
            {
                InvalidateSubjectListCache(schoolId, subject.SubjectCategory.SubjectStatus);
            }
            return true;
        }

        public async Task<bool> DeleteSubject(string userId, string id, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            var subject = await subjectRepository.FindAsync(
                item => item.Id == id
                    && item.SubjectCategory != null
                    && item.SubjectCategory.SchoolId == schoolId,
                ct,
                item => item.SubjectCategory!);

            if (subject == null)
            {
                return false;
            }

            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var hasPreviousAssessment = await studentAssessmentRepository.FindAsync(
                assessment => assessment.SubjectId == id
                    && assessment.YearlyAndTermlyStatus != null
                    && assessment.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Previous,
                ct,
                assessment => assessment.YearlyAndTermlyStatus!);

            if (hasPreviousAssessment != null)
            {
                return false;
            }

            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();
            var subjectAssignments = await subjectStudentAssignmentRepository.WhereAsync(assignment => assignment.SubjectId == id, ct);
            foreach (var assignment in subjectAssignments)
            {
                subjectStudentAssignmentRepository.Delete(assignment);
            }

            var subjectAssessments = await studentAssessmentRepository.WhereAsync(assessment => assessment.SubjectId == id, ct);
            foreach (var assessment in subjectAssessments)
            {
                studentAssessmentRepository.Delete(assessment);
            }

            var teacherSubjectAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var teacherAssignments = await teacherSubjectAssignmentRepository.WhereAsync(assignment => assignment.SubjectId == id, ct);
            foreach (var assignment in teacherAssignments)
            {
                teacherSubjectAssignmentRepository.Delete(assignment);
            }

            subjectRepository.Delete(subject);
            await _uow.SaveChangesAsync(ct);
            if (subject.SubjectCategory != null)
            {
                InvalidateSubjectListCache(schoolId, subject.SubjectCategory.SubjectStatus);
            }
            return true;
        }

        public async Task<bool> IsRequestToUpdateSubjectTimeStatus(string userId, string id, TimeStatus timeStatus, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            var subject = await subjectRepository.FindAsync(
                item => item.Id == id
                    && item.SubjectCategory != null
                    && item.SubjectCategory.SchoolId == schoolId,
                ct,
                item => item.SubjectCategory!);

            return subject != null && subject.TimeStatus != timeStatus;
        }

        public async Task<IList<SelectListItem>> GetSubjectForDropDown(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SelectListItem>();
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            var subjects = await subjectRepository.WhereAsync(
                subject => subject.SubjectCategory != null && subject.SubjectCategory.SchoolId == schoolId,
                ct,
                subject => subject.SubjectCategory!);

            return subjects
                .GroupBy(subject => NormalizeSubjectNameForLookup(subject.SubjectName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(subject => subject.SubjectName)
                .Select(subject => new SelectListItem
                {
                    Value = subject.SubjectName,
                    Text = subject.SubjectName,
                })
                .ToList();
        }

        public async Task<IList<SubjectDto>> GetLevelsThatOfferThisSubject(string userId, string subjectName, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(subjectName))
            {
                return new List<SubjectDto>();
            }

            var normalizedSubjectName = NormalizeSubjectNameForLookup(subjectName);
            var subjectRepository = _uow.Repository<RxSubject>();
            var subjects = await subjectRepository.WhereAsync(
                subject => subject.SubjectCategory != null
                    && subject.SubjectCategory.SchoolId == schoolId
                    && (normalizedSubjectName == "allsubjects"
                        || subject.SubjectName.Trim().ToLower() == normalizedSubjectName),
                ct,
                subject => subject.SubjectCategory!);

            return subjects
                .OrderBy(subject => subject.SubjectName)
                .Select(MapSubject)
                .ToList();
        }

        public async Task<IList<SubjectDto>> SearchLevelsThatOfferThisSubject(string userId, string searchParameter, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(searchParameter))
            {
                return new List<SubjectDto>();
            }

            var normalizedSearch = NormalizeSubjectNameForLookup(searchParameter);
            var subjectRepository = _uow.Repository<RxSubject>();
            var subjects = await subjectRepository.WhereAsync(
                subject => subject.SubjectCategory != null
                    && subject.SubjectCategory.SchoolId == schoolId
                    && subject.SubjectName.Trim().ToLower().Contains(normalizedSearch),
                ct,
                subject => subject.SubjectCategory!);

            return subjects
                .OrderBy(subject => subject.SubjectName)
                .Select(MapSubject)
                .ToList();
        }

        public async Task<IList<SelectListItem>> ClassDropDown(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SelectListItem>();
            }

            var classRepository = _uow.Repository<RxClasss>();
            var classes = await classRepository.WhereAsync(classModel => classModel.SchoolInfoId == schoolId, ct);

            return classes
                .OrderBy(classModel => classModel.ClassName)
                .Select(classModel => new SelectListItem
                {
                    Value = classModel.Id,
                    Text = classModel.ClassLabel,
                })
                .ToList();
        }

        public async Task<IList<SelectListItem>> ArmDropDown(string userId, string selectedClassId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(selectedClassId))
            {
                return new List<SelectListItem>();
            }

            var classRepository = _uow.Repository<RxClasss>();
            var classModel = await classRepository.FindAsync(
                item => item.Id == selectedClassId && item.SchoolInfoId == schoolId,
                ct);
            if (classModel == null)
            {
                return new List<SelectListItem>();
            }

            var classArmRepository = _uow.Repository<AxClassArm>();
            var arms = await classArmRepository.WhereAsync(arm => arm.ClassId == selectedClassId, ct);

            return arms
                .OrderBy(arm => arm.ArmName)
                .Select(arm => new SelectListItem
                {
                    Value = arm.Id,
                    Text = arm.ArmName.ToString(),
                })
                .ToList();
        }

        public async Task<IList<SelectListItem>> ElectiveSubjectDropDown(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SelectListItem>();
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            var electiveSubjects = await subjectRepository.WhereAsync(
                subject => subject.SubjectCategory != null
                    && subject.SubjectCategory.SchoolId == schoolId
                    && subject.SubjectCategory.SubjectStatus == SubjectStatus.Elective
                    && subject.TimeStatus == TimeStatus.Current,
                ct,
                subject => subject.SubjectCategory!);

            return electiveSubjects
                .OrderBy(subject => subject.SubjectName)
                .Select(subject => new SelectListItem
                {
                    Value = subject.Id,
                    Text = subject.SubjectName,
                })
                .ToList();
        }

        public async Task<ArmOrCategoryStudentsInTheSelectedArmDto> GetCategoryStudentsInTheSelectedArm(
            string userId,
            byte catStatus,
            string armId,
            string subjectId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(armId) || string.IsNullOrWhiteSpace(subjectId))
            {
                return new ArmOrCategoryStudentsInTheSelectedArmDto
                {
                    StudentsList = new List<StudentDetailsDto>(),
                    AssignedStudentIds = new List<string>(),
                };
            }

            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var isArtCategory = catStatus == 2;
            var isScienceCategory = catStatus == 3;
            var isCommercialCategory = catStatus == 4;

            var assignedStudentIds = await subjectStudentAssignmentRepository.WhereAsync(
                assignment => assignment.SubjectId == subjectId
                    && assignment.StudentDetail != null
                    && assignment.StudentDetail.ClassArmId == armId
                    && assignment.StudentDetail.Status != StudentStatus.Previous
                    && (!isArtCategory || assignment.StudentDetail.ClassCategory == ClassCategoryStatus.Art)
                    && (!isScienceCategory || assignment.StudentDetail.ClassCategory == ClassCategoryStatus.Science)
                    && (!isCommercialCategory || assignment.StudentDetail.ClassCategory == ClassCategoryStatus.Commercial),
                ct,
                assignment => assignment.StudentDetail!);

            var students = await studentRepository.WhereAsync(
                student => student.ClassArmId == armId
                    && student.Status != StudentStatus.Previous
                    && (!isArtCategory || student.ClassCategory == ClassCategoryStatus.Art)
                    && (!isScienceCategory || student.ClassCategory == ClassCategoryStatus.Science)
                    && (!isCommercialCategory || student.ClassCategory == ClassCategoryStatus.Commercial),
                ct,
                student => student.User!);

            return new ArmOrCategoryStudentsInTheSelectedArmDto
            {
                StudentsList = students.Select(MapStudentSummary).ToList(),
                AssignedStudentIds = assignedStudentIds.Select(assignment => assignment.StudentId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            };
        }

        public async Task<ArmOrCategoryStudentsInTheSelectedArmDto> GetStudentForAnArm2Nd(
            string userId,
            string armId,
            string subjectId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(armId) || string.IsNullOrWhiteSpace(subjectId))
            {
                return new ArmOrCategoryStudentsInTheSelectedArmDto
                {
                    StudentsList = new List<StudentDetailsDto>(),
                    AssignedStudentIds = new List<string>(),
                };
            }

            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();
            var studentRepository = _uow.Repository<TxStudentDetail>();

            var assignedStudentIds = await subjectStudentAssignmentRepository.WhereAsync(
                assignment => assignment.SubjectId == subjectId
                    && assignment.StudentDetail != null
                    && assignment.StudentDetail.ClassArmId == armId,
                ct,
                assignment => assignment.StudentDetail!);

            var students = await studentRepository.WhereAsync(
                student => student.ClassArmId == armId && student.Status != StudentStatus.Previous,
                ct,
                student => student.User!);

            return new ArmOrCategoryStudentsInTheSelectedArmDto
            {
                StudentsList = students.Select(MapStudentSummary).ToList(),
                AssignedStudentIds = assignedStudentIds.Select(assignment => assignment.StudentId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            };
        }

        public async Task<string> AssignSubjectToArmStudentsOrStudentsInCategory(
            string userId,
            List<StudentIdsDto> studentIds,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || studentIds == null || studentIds.Count < 3)
            {
                return string.Empty;
            }

            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return string.Empty;
            }

            var armId = studentIds.FirstOrDefault()?.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(armId))
            {
                return string.Empty;
            }

            studentIds.RemoveAt(0);
            var categoriesValue = studentIds.LastOrDefault()?.Id ?? "1";
            if (studentIds.Count == 0)
            {
                return string.Empty;
            }

            studentIds.RemoveAt(studentIds.Count - 1);
            var subjectId = studentIds.FirstOrDefault()?.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return string.Empty;
            }

            studentIds.RemoveAt(0);
            var selectedStudentIds = studentIds
                .Select(student => student.Id)
                .Where(studentId => !string.IsNullOrWhiteSpace(studentId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();
            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var teacherSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var teacherRepository = _uow.Repository<TxEmployee>();
            var studentRepository = _uow.Repository<TxStudentDetail>();

            // Never create assessments for Previous (alumni) students, even if the client sent their IDs.
            var eligibleStudentIds = (await studentRepository.WhereAsync(
                    student =>
                        selectedStudentIds.Contains(student.UserId) &&
                        student.ClassArmId == armId &&
                        student.Status != StudentStatus.Previous,
                    ct))
                .Select(student => student.UserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var teacherAssignment = await teacherSubjectArmAssignmentRepository.FindAsync(
                assignment => assignment.ArmId == armId && assignment.SubjectId == subjectId,
                ct);

            var teacherFullName = string.Empty;
            if (teacherAssignment == null)
            {
                var currentTeacher = (await teacherRepository.WhereAsync(
                    teacher => teacher.SchoolId == schoolId && teacher.TimeStatus == TimeStatus.Current,
                    ct,
                    teacher => teacher.User!))
                    .OrderBy(teacher => BuildFullName(teacher.User))
                    .FirstOrDefault();

                if (currentTeacher != null)
                {
                    teacherFullName = BuildFullName(currentTeacher.User);
                    teacherAssignment = new TeachTxerSubjectArmAssignment
                    {
                        Id = Guid.NewGuid().ToString(),
                        EmployeeId = currentTeacher.Id,
                        ArmId = armId,
                        SubjectId = subjectId,
                    };
                    teacherSubjectArmAssignmentRepository.Add(teacherAssignment);
                }
            }

            var currentYearlyAndTermlyStatus = await GetCurrentYearlyAndTermlyStatusAsync(schoolId, ct);
            foreach (var selectedStudentId in eligibleStudentIds)
            {
                var existingAssignment = await subjectStudentAssignmentRepository.FindAsync(
                    assignment => assignment.StudentId == selectedStudentId && assignment.SubjectId == subjectId,
                    ct);

                if (existingAssignment == null)
                {
                    subjectStudentAssignmentRepository.Add(new SubjectStudentTxAssignment
                    {
                        Id = Guid.NewGuid().ToString(),
                        StudentId = selectedStudentId,
                        SubjectId = subjectId,
                    });
                }

                if (currentYearlyAndTermlyStatus != null && teacherAssignment != null)
                {
                    var currentAssessment = await studentAssessmentRepository.FindAsync(
                        assessment => assessment.StudentId == selectedStudentId
                            && assessment.ClassArmId == armId
                            && assessment.SubjectId == subjectId
                            && assessment.YearlyAndTermlyStatusId == currentYearlyAndTermlyStatus.Id,
                        ct);

                    if (currentAssessment == null)
                    {
                        studentAssessmentRepository.Add(new StudenXtAccessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClassArmId = armId,
                            StudentId = selectedStudentId,
                            EmployeeId = teacherAssignment.EmployeeId,
                            SubjectId = subjectId,
                            LastUpdateDate = DateTime.UtcNow,
                            Ca1 = 0,
                            Ca2 = 0,
                            Ca3 = 0,
                            Exams = 0,
                            YearlyAndTermlyStatusId = currentYearlyAndTermlyStatus.Id,
                        });
                    }
                }
            }

            var existingAssignments = await subjectStudentAssignmentRepository.WhereAsync(
                assignment => assignment.SubjectId == subjectId
                    && assignment.StudentDetail != null
                    && assignment.StudentDetail.ClassArmId == armId,
                ct,
                assignment => assignment.StudentDetail!);

            foreach (var existingAssignment in existingAssignments)
            {
                if (!IsStudentWithinSelectedCategory(existingAssignment.StudentDetail, categoriesValue)
                    || eligibleStudentIds.Contains(existingAssignment.StudentId, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                subjectStudentAssignmentRepository.Delete(existingAssignment);
                await RemoveCurrentTermAssessmentIfExists(existingAssignment.StudentId, armId, subjectId, currentYearlyAndTermlyStatus?.Id, ct);
            }

            await _uow.SaveChangesAsync(ct);
            return teacherFullName;
        }

        public async Task<bool> UnAssignSubjectToArmStudentsOrStudentsInCategory(
            string userId,
            List<StudentIdsDto> studentIds,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || studentIds == null || studentIds.Count < 2)
            {
                return false;
            }

            var schoolId = await ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return false;
            }

            var armId = studentIds.FirstOrDefault()?.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(armId))
            {
                return false;
            }

            studentIds.RemoveAt(0);
            var subjectId = studentIds.FirstOrDefault()?.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return false;
            }

            studentIds.RemoveAt(0);
            var currentYearlyAndTermlyStatus = await GetCurrentYearlyAndTermlyStatusAsync(schoolId, ct);

            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();
            foreach (var studentId in studentIds.Select(item => item.Id).Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                var existingAssignment = await subjectStudentAssignmentRepository.FindAsync(
                    assignment => assignment.StudentId == studentId
                        && assignment.SubjectId == subjectId
                        && assignment.StudentDetail != null
                        && assignment.StudentDetail.ClassArmId == armId,
                    ct,
                    assignment => assignment.StudentDetail!);

                if (existingAssignment != null)
                {
                    subjectStudentAssignmentRepository.Delete(existingAssignment);
                }

                await RemoveCurrentTermAssessmentIfExists(studentId, armId, subjectId, currentYearlyAndTermlyStatus?.Id, ct);
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        private static SubjectCategoriesDto MapSubjectCategory(SubjectRxCategories category)
        {
            return new SubjectCategoriesDto
            {
                Id = short.TryParse(category.Id, out var parsedId) ? parsedId : (short)0,
                SubjectStatus = category.SubjectStatus,
                SubjectStatusLabel = category.SubjectStatusLabel ?? string.Empty,
                SchoolId = category.SchoolId ?? string.Empty,
            };
        }

        private static int GetSubjectCategoryOrder(SubjectStatus status)
        {
            return status switch
            {
                SubjectStatus.PlayClass => 0,
                SubjectStatus.PreNursery => 1,
                SubjectStatus.Nursery => 2,
                SubjectStatus.Primary => 3,
                SubjectStatus.Jss => 4,
                SubjectStatus.AllSeniors => 5,
                SubjectStatus.Science => 6,
                SubjectStatus.Art => 7,
                SubjectStatus.Commercial => 8,
                SubjectStatus.Elective => 9,
                SubjectStatus.General => 10,
                _ => 11,
            };
        }

        private async Task<List<SubjectNameStatusRecord>> FindSubjectsByNameAsync(string schoolId, string subjectName, CancellationToken ct)
        {
            var normalizedSubjectName = NormalizeSubjectNameForLookup(subjectName);
            var subjectRepository = _uow.Repository<RxSubject>();
            var subjects = await subjectRepository.WhereAsync(
                subject => subject.SubjectCategory != null
                    && subject.SubjectCategory.SchoolId == schoolId
                    && subject.SubjectName.Trim().ToLower() == normalizedSubjectName,
                ct,
                subject => subject.SubjectCategory!);

            return subjects
                .Select(subject => new SubjectNameStatusRecord
                {
                    Id = subject.Id,
                    SubjectStatus = subject.SubjectCategory?.SubjectStatus ?? SubjectStatus.General,
                })
                .ToList();
        }

        private static bool CanCreateSubjectForStatus(SubjectStatus subjectStatus, IEnumerable<SubjectStatus> duplicateStatuses)
        {
            var statuses = duplicateStatuses.ToList();
            return subjectStatus switch
            {
                SubjectStatus.PlayClass => !statuses.Contains(SubjectStatus.PlayClass),
                SubjectStatus.PreNursery => !statuses.Contains(SubjectStatus.PreNursery),
                SubjectStatus.General => statuses.All(status => status == SubjectStatus.General),
                SubjectStatus.Nursery => !statuses.Intersect(new[] { SubjectStatus.Nursery, SubjectStatus.General }).Any(),
                SubjectStatus.Primary => !statuses.Intersect(new[] { SubjectStatus.Primary, SubjectStatus.General }).Any(),
                SubjectStatus.Jss => !statuses.Intersect(new[] { SubjectStatus.Jss, SubjectStatus.General }).Any(),
                SubjectStatus.AllSeniors => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Science => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Art => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Elective,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Commercial => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Elective => !statuses.Contains(SubjectStatus.Elective),
                _ => false,
            };
        }

        private static bool CanUpdateSubjectForStatus(SubjectStatus subjectStatus, IEnumerable<SubjectStatus> duplicateStatuses)
        {
            var statuses = duplicateStatuses.ToList();
            return subjectStatus switch
            {
                SubjectStatus.PlayClass => !statuses.Contains(SubjectStatus.PlayClass),
                SubjectStatus.PreNursery => !statuses.Contains(SubjectStatus.PreNursery),
                SubjectStatus.General => !statuses.Contains(SubjectStatus.General),
                SubjectStatus.Nursery => !statuses.Intersect(new[] { SubjectStatus.Nursery, SubjectStatus.General }).Any(),
                SubjectStatus.Primary => !statuses.Intersect(new[] { SubjectStatus.Primary, SubjectStatus.General }).Any(),
                SubjectStatus.Jss => !statuses.Intersect(new[] { SubjectStatus.Jss, SubjectStatus.General }).Any(),
                SubjectStatus.AllSeniors => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Science => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Art => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Elective,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Commercial => !statuses.Intersect(new[]
                {
                    SubjectStatus.Art,
                    SubjectStatus.Science,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.General,
                    SubjectStatus.Commercial,
                }).Any(),
                SubjectStatus.Elective => !statuses.Contains(SubjectStatus.Elective),
                _ => false,
            };
        }

        private async Task<string> EnsureSubjectCategoryIdAsync(string schoolId, SubjectStatus subjectStatus, CancellationToken ct)
        {
            await EnsureDefaultSubjectCategoriesAsync(schoolId, ct);

            var subjectCategoryRepository = _uow.Repository<SubjectRxCategories>();
            var category = await subjectCategoryRepository.FindAsync(
                item => item.SchoolId == schoolId && item.SubjectStatus == subjectStatus,
                ct);

            return category?.Id ?? string.Empty;
        }

        private async Task EnsureDefaultSubjectCategoriesAsync(string schoolId, CancellationToken ct)
        {
            var subjectCategoryRepository = _uow.Repository<SubjectRxCategories>();
            var categories = await subjectCategoryRepository.WhereAsync(category => category.SchoolId == schoolId, ct);
            var existingStatuses = categories
                .Select(category => category.SubjectStatus)
                .ToHashSet();

            var hasPendingInsert = false;
            foreach (var status in ManagedSubjectStatuses)
            {
                if (existingStatuses.Contains(status))
                {
                    continue;
                }

                subjectCategoryRepository.Add(new SubjectRxCategories
                {
                    Id = Guid.NewGuid().ToString(),
                    SubjectStatus = status,
                    SchoolId = schoolId,
                    SubjectStatusLabel = string.Empty,
                });
                hasPendingInsert = true;
            }

            if (hasPendingInsert)
            {
                await _uow.SaveChangesAsync(ct);
            }
        }

        private async Task RemoveSubjectAssignmentArtifactsAsync(string subjectId, CancellationToken ct)
        {
            var teacherSubjectArmAssignmentsRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var teacherAssignments = await teacherSubjectArmAssignmentsRepository.WhereAsync(
                assignment => assignment.SubjectId == subjectId,
                ct);

            foreach (var assignment in teacherAssignments)
            {
                teacherSubjectArmAssignmentsRepository.Delete(assignment);
            }

            var studentAccessmentRepository = _uow.Repository<StudenXtAccessment>();
            var currentAssessments = await studentAccessmentRepository.WhereAsync(
                assessment => assessment.SubjectId == subjectId
                    && assessment.YearlyAndTermlyStatus != null
                    && assessment.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                assessment => assessment.YearlyAndTermlyStatus!);

            foreach (var assessment in currentAssessments)
            {
                studentAccessmentRepository.Delete(assessment);
            }
        }

        private async Task<TxYearlyAndTermlyStatus?> GetCurrentYearlyAndTermlyStatusAsync(string schoolId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            return await yearlyAndTermlyStatusRepository.FindAsync(
                status => status.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                    && status.Session != null
                    && status.Session.SchoolInfoId == schoolId,
                ct,
                status => status.Session!);
        }

        private async Task RemoveCurrentTermAssessmentIfExists(
            string studentId,
            string armId,
            string subjectId,
            string? yearlyAndTermlyStatusId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(yearlyAndTermlyStatusId))
            {
                return;
            }

            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var currentAssessment = await studentAssessmentRepository.FindAsync(
                assessment => assessment.StudentId == studentId
                    && assessment.ClassArmId == armId
                    && assessment.SubjectId == subjectId
                    && assessment.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId,
                ct);

            if (currentAssessment != null)
            {
                studentAssessmentRepository.Delete(currentAssessment);
            }
        }

        private static bool IsStudentWithinSelectedCategory(TxStudentDetail student, byte catStatus)
        {
            return catStatus switch
            {
                2 => student.ClassCategory == ClassCategoryStatus.Art,
                3 => student.ClassCategory == ClassCategoryStatus.Science,
                4 => student.ClassCategory == ClassCategoryStatus.Commercial,
                _ => true,
            };
        }

        private static bool IsStudentWithinSelectedCategory(TxStudentDetail student, string categoryValue)
        {
            return categoryValue switch
            {
                "2" => student.ClassCategory == ClassCategoryStatus.Art,
                "3" => student.ClassCategory == ClassCategoryStatus.Science,
                "4" => student.ClassCategory == ClassCategoryStatus.Commercial,
                _ => true,
            };
        }

        private static string BuildSubjectListCacheKey(string schoolId, SubjectStatus subjectStatus)
            => $"subject-list:{schoolId}:{(byte)subjectStatus}";

        private void InvalidateSubjectListCache(string schoolId, SubjectStatus subjectStatus)
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return;
            }

            _cache.Remove(BuildSubjectListCacheKey(schoolId, subjectStatus));
        }

        private static string NormalizeSubjectNameForPersistence(string subjectName)
        {
            return string.IsNullOrWhiteSpace(subjectName)
                ? string.Empty
                : subjectName.Replace("\\", string.Empty).Trim();
        }

        private static string NormalizeSubjectNameForLookup(string subjectName)
        {
            return string.IsNullOrWhiteSpace(subjectName)
                ? string.Empty
                : subjectName.Trim().ToLower();
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
                        Id = short.TryParse(subject.SubjectCategory.Id, out var parsedId) ? parsedId : (short)0,
                        SubjectStatus = subject.SubjectCategory.SubjectStatus,
                        SubjectStatusLabel = subject.SubjectCategory.SubjectStatusLabel ?? string.Empty,
                        SchoolId = subject.SubjectCategory.SchoolId ?? string.Empty,
                    },
            };
        }

        private static StudentDetailsDto MapStudentSummary(TxStudentDetail student)
        {
            return new StudentDetailsDto
            {
                Id = student.Id,
                IdNo = student.IdNo,
                FirstName = student.User?.FirstName ?? string.Empty,
                MiddleName = student.User?.MiddleName ?? string.Empty,
                SurName = student.User?.SurName ?? string.Empty,
                ClassCategory = student.ClassCategory,
                CurrentClass = student.CurrentClass,
                Status = student.Status,
                UserId = student.UserId,
                ClassArmId = student.ClassArmId,
                User = new ApplicationUserDto
                {
                    Id = student.UserId,
                    UserName = student.User?.UserName ?? string.Empty,
                    FirstName = student.User?.FirstName ?? string.Empty,
                    MiddleName = student.User?.MiddleName ?? string.Empty,
                    SurName = student.User?.SurName ?? string.Empty,
                    Gender = student.User?.Gender ?? Gender.Male,
                    AccessStatus = student.User?.AccessStatus ?? AccessStatus.Active,
                },
                ClassArm = student.ClassArm == null ? new ClassArmDto() : MapClassArm(student.ClassArm),
            };
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
                        SchoolInfoId = arm.Classs.SchoolInfoId,
                    },
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

        private sealed class SubjectNameStatusRecord
        {
            public string Id { get; init; } = string.Empty;

            public SubjectStatus SubjectStatus { get; init; }
        }
    }
}
