using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public partial class RegistrationService
    {
        public async Task<PrepareEnvironmentForStudentAssessmentReturnDto> ProcessNewAssessmentAsync(
            string userId,
            NewAssessmentProcessRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return InvalidRequest("Request body is required.");
            }

            if (request.CollectionType == NewAssessmentCollectionMode.WholeSchool)
            {
                var concatParameters = BuildWholeSchoolConcatParameters(request);
                return await PrepareAssessmentEnvironmentForTheWholeStudentAsync(userId, concatParameters, ct)
                    ?? InvalidRequest(
                        "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.");
            }

            if (request.CollectionType == NewAssessmentCollectionMode.PerClass)
            {
                var concatParameters = BuildPerClassConcatParameters(request);
                return await PrepareAssessmentEnvironmentForaClassStudentsAsync(userId, concatParameters, ct)
                    ?? InvalidRequest(
                        "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.");
            }

            if (request.CollectionType == NewAssessmentCollectionMode.PerArm)
            {
                var concatParameters = BuildPerArmConcatParameters(request);
                return await PrepareAssessmentEnvironmentForAnArmStudentsAsync(userId, concatParameters, ct)
                    ?? InvalidRequest(
                        "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.");
            }

            return InvalidRequest("Invalid collection type.");
        }

        /// <summary>
        /// Legacy RegistryController.NewAssessmentHandler: pick Session/Term like MVC ViewBag, plus Class/Arm dropdowns.
        /// Session selection: OrderBy(int.Parse(Value)).Last() when Values are numeric; otherwise Status=Current then latest Session year.
        /// Term selection: OrderBy(int.Parse(Value)).Last() for the selected session.
        /// </summary>
        public async Task<NewAssessmentHandlerBootstrapDto> GetNewAssessmentHandlerBootstrapAsync(
            string userId,
            CancellationToken ct = default)
        {
            var result = new NewAssessmentHandlerBootstrapDto();
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return result;
            }

            var sessions = (await _uow.Repository<TxAcademicSession>().WhereAsync(
                    s => s.SchoolInfoId == schoolId,
                    ct))
                .ToList();

            if (sessions.Count == 0)
            {
                return result;
            }

            var currentSessionEntity = SelectLegacyCurrentSession(sessions);
            if (currentSessionEntity == null)
            {
                return result;
            }

            result.Session.Add(new SelectListItem
            {
                Text = currentSessionEntity.Session,
                Value = currentSessionEntity.Id,
            });

            var terms = (await _uow.Repository<TxYearlyAndTermlyStatus>().WhereAsync(
                    t => t.SessionId == currentSessionEntity.Id,
                    ct))
                .OrderBy(t => t.TermStatus)
                .Select(t => new SelectListItem
                {
                    Text = t.TermStatus.ToString(),
                    Value = ((int)t.TermStatus).ToString(),
                })
                .ToList();

            var currentTerm = SelectLegacyLastByNumericValue(terms);
            if (currentTerm == null || string.IsNullOrEmpty(currentTerm.Value))
            {
                result.Term.Add(new SelectListItem { Text = "None", Value = "-1" });
            }
            else
            {
                result.Term.Add(currentTerm);
            }

            // Legacy StudentIndexClassAndArmDropDown
            var classes = (await _uow.Repository<RxClasss>().WhereAsync(
                    c => c.SchoolInfoId == schoolId,
                    ct))
                .OrderBy(c => c.ClassName)
                .Select(c => new SelectListItem
                {
                    Text = c.ClassLabel,
                    Value = ((byte)c.ClassName) + "," + c.ClassLabel,
                })
                .ToList();
            result.Classes = classes;

            var arms = (await _uow.Repository<AxClassArm>().WhereAsync(
                    a => a.Classs!.SchoolInfoId == schoolId,
                    ct,
                    a => a.Classs!))
                .OrderBy(a => a.ArmName)
                .GroupBy(a => a.ArmName)
                .Select(g => g.First())
                .Select(a => new SelectListItem
                {
                    Text = a.ArmName.ToString(),
                    Value = ((byte)a.ArmName).ToString(),
                })
                .ToList();

            // Legacy: when only one arm, move it after a "Select" placeholder.
            if (arms.Count == 1)
            {
                var onlyArm = arms[0];
                result.ArmName = new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    onlyArm,
                };
            }
            else
            {
                result.ArmName = arms;
            }

            return result;
        }

        private static TxAcademicSession? SelectLegacyCurrentSession(IReadOnlyList<TxAcademicSession> sessions)
        {
            if (sessions.Count == 0)
            {
                return null;
            }

            // Exact legacy: OrderBy(z => int.Parse(z.Value)).Last() when Ids are numeric.
            if (sessions.All(s => int.TryParse(s.Id, out _)))
            {
                return sessions.OrderBy(s => int.Parse(s.Id)).Last();
            }

            // Cloud GUID Id: prefer Status=Current, else latest session year label.
            var current = sessions.FirstOrDefault(s => s.Status == TimeStatus.Current);
            if (current != null)
            {
                return current;
            }

            return sessions
                .OrderByDescending(s => ParseSessionStartYear(s.Session))
                .ThenByDescending(s => s.Session)
                .FirstOrDefault();
        }

        private static int ParseSessionStartYear(string sessionLabel)
        {
            if (string.IsNullOrWhiteSpace(sessionLabel))
            {
                return 0;
            }

            var match = System.Text.RegularExpressions.Regex.Match(sessionLabel.Trim(), @"^(\d{4})");
            return match.Success && int.TryParse(match.Groups[1].Value, out var year) ? year : 0;
        }

        private static SelectListItem? SelectLegacyLastByNumericValue(IReadOnlyList<SelectListItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            var numeric = items
                .Where(item => int.TryParse(item.Value, out _))
                .OrderBy(item => int.Parse(item.Value))
                .ToList();

            return numeric.Count > 0 ? numeric.Last() : items.LastOrDefault();
        }

        /// <summary>
        /// Legacy PrepareAssessmentEnvironmentForTheWholeStudent(concatParameters):
        /// assessmentCategory,actionType[,psychoAffectDefaultValue]
        /// </summary>
        public async Task<PrepareEnvironmentForStudentAssessmentReturnDto?> PrepareAssessmentEnvironmentForTheWholeStudentAsync(
            string userId,
            string concatParameters,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(concatParameters))
            {
                return InvalidRequest("concatParameters is required.");
            }

            var itemList = concatParameters.Split(',').ToList();
            if (itemList.Count < 2)
            {
                return InvalidRequest("Invalid concatParameters for whole-school assessment processing.");
            }

            var assessmentCategory = itemList[0];
            var actionType = itemList[1];
            byte psychoAffectDefaultValue = 7;
            if (itemList.Count >= 3 && byte.TryParse(itemList[2], out var parsedPsycho))
            {
                psychoAffectDefaultValue = parsedPsycho;
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return InvalidRequest("Unable to resolve the current school.");
            }

            var prepareEnvironmentForStudentAssessmentReturn = new PrepareEnvironmentForStudentAssessmentReturnDto();

            if (assessmentCategory == "0" && actionType == "0")
            {
                return await _sessionAndTermService.PrepareAssessmentEnvironmentForTheWholeStudents(userId, null, ct);
            }

            if (assessmentCategory == "0" && actionType == "1")
            {
                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                if (yearlyAndTermlyStatus == null)
                {
                    return null;
                }

                var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
                var currentAssessmentToDelete = await studentAssessmentRepository.WhereAsync(
                    s =>
                        s.Student!.SchoolInfoId == schoolId &&
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatus.Id,
                    ct,
                    s => s.Subject!);

                // Legacy: only remove duplicate zero-CA rows, not every assessment.
                var byStudentGroup = currentAssessmentToDelete.GroupBy(x => x.StudentId);
                foreach (var each in byStudentGroup)
                {
                    var eachList = each.ToList();
                    var bySubjectGroup = eachList.GroupBy(x => x.Subject!.SubjectName).ToList();
                    if (bySubjectGroup.Count < eachList.Count)
                    {
                        foreach (var item in bySubjectGroup)
                        {
                            var wrongRec = item.Where(x => x.Ca1 == 0 && x.Ca2 == 0 && x.Ca3 == 0);
                            foreach (var item1 in wrongRec)
                            {
                                await studentAssessmentRepository.DeleteAsync(item1.Id, ct);
                            }
                        }
                    }
                }

                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current assessment of the selected class has successfully been DELETED.";
                return prepareEnvironmentForStudentAssessmentReturn;
            }

            if (assessmentCategory == "1" && actionType == "0")
            {
                // Legacy PrepareAssessmentEnvironmentForTheWholeStudent — create psycho/affective school-wide.
                var studentsList = await _uow.Repository<TxStudentDetail>().WhereAsync(
                    s =>
                        (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning) &&
                        s.SchoolInfoId == schoolId,
                    ct,
                    a => a.ClassArm!.Classs!);

                var getAffectiveAndPsychomotorItems = await GetAffectiveAndPsychomotorItemsAsync(schoolId, ct);
                if (getAffectiveAndPsychomotorItems.TotalAttendance == 255)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."
                    };
                }

                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                if (yearlyAndTermlyStatus == null)
                {
                    return null;
                }

                var classTeacherList = await _uow.Repository<TeacherClassArmTxAssignment>().WhereAsync(
                    a => a.Arm!.Classs!.SchoolInfoId == schoolId,
                    ct,
                    a => a.Arm!.Classs!);

                var affectiveAndPsycho = getAffectiveAndPsychomotorItems.AffectiveAndPsychomotor
                    ?? new List<AffectiveAndPsychomotorDto>();
                var yearlyAndTermlyStatusId = yearlyAndTermlyStatus.Id;

                var createResult = await CreateMissingPsychomotorAssessmentsAsync(
                    studentsList,
                    yearlyAndTermlyStatusId,
                    affectiveAndPsycho,
                    psychoAffectDefaultValue,
                    armId => classTeacherList.Find(c => c.ArmId == armId)?.EmployeeId,
                    "Sorry! Class-teacher has not been assigned to one or more Class-Arm(s) of the school.",
                    ct);

                prepareEnvironmentForStudentAssessmentReturn.Message = BuildPsychomotorCreateMessage(
                    createResult,
                    scopeLabel: "school");
                return prepareEnvironmentForStudentAssessmentReturn;
            }

            if (assessmentCategory == "1" && actionType == "1")
            {
                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                if (yearlyAndTermlyStatus == null)
                {
                    return null;
                }

                // Prefer assessment.ClassArm (set at create time) over Student.ClassArmId —
                // students may have moved arms after psycho rows were generated.
                var deleted = await DeletePsychomotorAssessmentsAsync(
                    s =>
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatus.Id &&
                        s.ClassArm!.Classs!.SchoolInfoId == schoolId,
                    ct);

                prepareEnvironmentForStudentAssessmentReturn.Message =
                    deleted > 0
                        ? "The current psychomotor and Affective of the selected class has successfully been DELETED."
                        : "No psychomotor/Affective records were found to delete for the current term.";
                return prepareEnvironmentForStudentAssessmentReturn;
            }

            return prepareEnvironmentForStudentAssessmentReturn;
        }

        private static string BuildWholeSchoolConcatParameters(NewAssessmentProcessRequestDto request)
        {
            var parts = new List<string>
            {
                request.AssessmentCategory ?? "0",
                request.ActionType ?? "0",
            };

            if (request.PsychoAffectDefaultValue.HasValue)
            {
                parts.Add(request.PsychoAffectDefaultValue.Value.ToString());
            }

            return string.Join(",", parts);
        }

        /// <summary>
        /// Legacy: PrepareAssessmentEnvironmentForaClassStudents(concatParameters).
        /// Class-change concat from the view:
        /// ClassName,Session,assessmentCategory,actionType[,psychoAffectDefaultValue]
        /// </summary>
        public async Task<PrepareEnvironmentForStudentAssessmentReturnDto?> PrepareAssessmentEnvironmentForaClassStudentsAsync(
            string userId,
            string concatParameters,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(concatParameters))
            {
                return InvalidRequest("concatParameters is required.");
            }

            var itemList = concatParameters.Split(',').ToList();
            // ClassName dropdown Value is "enum,ClassLabel", so the wire format is:
            // ClassEnum,ClassLabel,Session,assessmentCategory,actionType[,psycho]
            // ClassLabel may contain commas — parse category/action/psycho from the end.
            if (!TryParsePerClassConcat(
                    itemList,
                    out var currentClass,
                    out var assessmentCategory,
                    out var actionType,
                    out var psychoAffectDefaultValue))
            {
                return InvalidRequest("Invalid concatParameters for per-class assessment processing.");
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return InvalidRequest("Unable to resolve the current school.");
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classEntity = await classRepo.FindAsync(
                s => s.SchoolInfoId == schoolId && s.ClassName == currentClass,
                ct);
            if (classEntity == null)
            {
                return InvalidRequest("Selected class was not found.");
            }

            var classId = classEntity.Id;
            var prepareEnvironmentForStudentAssessmentReturn = new PrepareEnvironmentForStudentAssessmentReturnDto();

            if (assessmentCategory == "0" && actionType == "0")
            {
                prepareEnvironmentForStudentAssessmentReturn = await PerClassStudentProcessingAsync(schoolId, classId, ct);
            }
            else if (assessmentCategory == "0" && actionType == "1")
            {
                var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
                var yearlyAndTermlyStatus = await yearlyAndTermlyStatusRepository.FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);

                var yearlyAndTermlyStatusId = yearlyAndTermlyStatus?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    prepareEnvironmentForStudentAssessmentReturn =
                        await PerClassStudentProcessingAsync(schoolId, classId, ct);
                    if (prepareEnvironmentForStudentAssessmentReturn.Message == "Success")
                    {
                        await _uow.SaveChangesAsync(ct);
                        yearlyAndTermlyStatus = await yearlyAndTermlyStatusRepository.FindAsync(
                            x =>
                                x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                                x.Session!.SchoolInfoId == schoolId,
                            ct,
                            x => x.Session!);
                        yearlyAndTermlyStatusId = yearlyAndTermlyStatus?.Id;
                    }
                    else
                    {
                        return prepareEnvironmentForStudentAssessmentReturn;
                    }
                }

                if (string.IsNullOrWhiteSpace(yearlyAndTermlyStatusId))
                {
                    return prepareEnvironmentForStudentAssessmentReturn;
                }

                // Legacy: load with Subject, then delete duplicate zero-CA rows only.
                var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
                var currentAssessmentToDelete = await studentAssessmentRepository.WhereAsync(
                    s =>
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                        s.Student!.ClassArm!.ClassId == classId,
                    ct,
                    c => c.Subject!);

                var byStudentGroup = currentAssessmentToDelete.GroupBy(x => x.StudentId);
                foreach (var each in byStudentGroup)
                {
                    var eachList = each.ToList();
                    var bySubjectGroup = eachList.GroupBy(x => x.Subject!.SubjectName).ToList();
                    if (bySubjectGroup.Count < eachList.Count)
                    {
                        // Means there are duplicate records
                        foreach (var item in bySubjectGroup)
                        {
                            var wrongRec = item.Where(x => x.Ca1 == 0 && x.Ca2 == 0 && x.Ca3 == 0);
                            foreach (var item1 in wrongRec)
                            {
                                await studentAssessmentRepository.DeleteAsync(item1.Id, ct);
                            }
                        }
                    }
                }

                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current assessment of the selected class has successfully been DELETED.";
            }

            if (assessmentCategory == "1" && actionType == "0")
            {
                // Legacy: create current psychomotor and Affective for the class.
                var studentRepository = _uow.Repository<TxStudentDetail>();
                var studentsList = await studentRepository.WhereAsync(
                    s =>
                        (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning) &&
                        s.ClassArm!.ClassId == classId,
                    ct,
                    a => a.ClassArm!.Classs!);

                var currentClassTeacherList = await _uow.Repository<TeacherClassArmTxAssignment>().WhereAsync(
                    a => a.Arm!.ClassId == classId,
                    ct,
                    a => a.Arm!);

                var getAffectiveAndPsychomotorItems = await GetAffectiveAndPsychomotorItemsAsync(schoolId, ct);
                if (getAffectiveAndPsychomotorItems.TotalAttendance == 255)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."
                    };
                }

                var affectiveAndPsycho = getAffectiveAndPsychomotorItems.AffectiveAndPsychomotor
                    ?? new List<AffectiveAndPsychomotorDto>();

                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                if (yearlyAndTermlyStatus == null)
                {
                    return null;
                }

                var createResult = await CreateMissingPsychomotorAssessmentsAsync(
                    studentsList,
                    yearlyAndTermlyStatus.Id,
                    affectiveAndPsycho,
                    psychoAffectDefaultValue,
                    armId => currentClassTeacherList.Find(c => c.ArmId == armId)?.EmployeeId,
                    "Sorry! Class-teacher has not been assigned to one or more Arm(s) in the selected class.",
                    ct);

                prepareEnvironmentForStudentAssessmentReturn.Message = BuildPsychomotorCreateMessage(
                    createResult,
                    scopeLabel: "class");
            }
            else if (assessmentCategory == "1" && actionType == "1")
            {
                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                if (yearlyAndTermlyStatus == null)
                {
                    return null;
                }

                // Match rows by the ClassArm stamped on the assessment (legacy used Student.ClassArm;
                // that misses rows when the student later moved to another arm/class).
                var deleted = await DeletePsychomotorAssessmentsAsync(
                    s =>
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatus.Id &&
                        s.ClassArm!.ClassId == classId,
                    ct);

                prepareEnvironmentForStudentAssessmentReturn.Message =
                    deleted > 0
                        ? "The current psychomotor and Affective of the selected class has successfully been DELETED."
                        : "No psychomotor/Affective records were found to delete for the selected class in the current term.";
            }

            return prepareEnvironmentForStudentAssessmentReturn;
        }

        public async Task<string> GetSystemImplementationStatusAsync(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return string.Empty;
            }

            var schoolRepository = _uow.Repository<SchoolTyInfo, string>();
            var school = await schoolRepository.FindAsync(
                x => x.SchoolRegistrationOfficerId == schoolId,
                ct);
            if (school == null)
            {
                return string.Empty;
            }

            // Schools registered before registration set these default to per-term (":0"),
            // as registration and assessment preparation do. Empty would be read as per-student.
            if (string.IsNullOrWhiteSpace(school.SystemImplementationType)
                || string.IsNullOrWhiteSpace(school.FeatureImplementationType))
            {
                var defaultType = FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":0", true);
                if (string.IsNullOrWhiteSpace(school.SystemImplementationType))
                {
                    school.SystemImplementationType = defaultType;
                }

                if (string.IsNullOrWhiteSpace(school.FeatureImplementationType))
                {
                    school.FeatureImplementationType = defaultType;
                }

                schoolRepository.Update(school);
                await _uow.SaveChangesAsync(ct);
            }

            return school.SystemImplementationType;
        }

        public async Task<byte> GetAppropriatePaymentStatusAsync(string userId, CancellationToken ct = default)
        {
            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(
                x => x.SchoolRegistrationOfficer!.UserId == userId,
                ct,
                x => x.SchoolRegistrationOfficer!);

            if (school == null || string.IsNullOrWhiteSpace(school.TotalStudents))
            {
                return 0;
            }

            var decryptNoOfStudentInSchool = FreePlugIn.Decrypt(school.TotalStudents, true).Split(':').ToList();
            if (decryptNoOfStudentInSchool.Count < 2 ||
                !int.TryParse(decryptNoOfStudentInSchool[1], out var noOfStudentInSchoolInInt))
            {
                return 0;
            }

            var studentActualNo = await _uow.Repository<TxStudentDetail>().CountAsync(
                c =>
                    c.SchoolInfo!.SchoolRegistrationOfficer!.UserId == userId &&
                    c.Status != StudentStatus.Previous,
                ct);

            if (noOfStudentInSchoolInInt >= studentActualNo)
            {
                return 0;
            }

            return (byte)(studentActualNo - noOfStudentInSchoolInInt);
        }

        private async Task<PrepareEnvironmentForStudentAssessmentReturnDto> PerClassStudentProcessingAsync(
            string schoolId,
            string classId,
            CancellationToken ct)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var studentsList = await studentRepository.WhereAsync(
                s =>
                    (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning) &&
                    s.ClassArm!.ClassId == classId,
                ct,
                a => a.ClassArm!.Classs!,
                s => s.User!);

            return await _sessionAndTermService.PrepareAssessmentEnvironmentForStudentsAsync(
                schoolId,
                studentsList,
                requestStatus: null,
                ct);
        }

        private static readonly (byte Id, string Name)[] DefaultAffectiveCatalog =
        [
            (1, "Attentiveness"),
            (2, "Honesty"),
            (3, "Neatness"),
            (4, "Politeness"),
            (5, "Punctuality"),
            (6, "Relationship with Others"),
            (7, "Club/Society"),
            (8, "Drawing and Painting"),
            (9, "Hand Writing"),
            (10, "Hobbies"),
            (11, "Speech Fluency"),
            (12, "Sport and Game"),
        ];

        private async Task EnsureAffectiveCatalogAsync(CancellationToken ct)
        {
            var repo = _uow.Repository<AffectiveXzAndPsychomotor, byte>();
            var existing = await repo.GetAllAsync(ct);
            var existingIds = (existing ?? Enumerable.Empty<AffectiveXzAndPsychomotor>())
                .Select(x => x.Id)
                .ToHashSet();

            var missing = DefaultAffectiveCatalog
                .Where(item => !existingIds.Contains(item.Id))
                .Select(item => new AffectiveXzAndPsychomotor { Id = item.Id, ItemsName = item.Name })
                .ToList();

            if (missing.Count == 0)
            {
                return;
            }

            foreach (var item in missing)
            {
                repo.Add(item);
            }

            await _uow.SaveChangesAsync(ct);
        }

        private async Task<AffectiveAndPsychomotorAndTotalAttendanceDto> GetAffectiveAndPsychomotorItemsAsync(
            string schoolId,
            CancellationToken ct)
        {
            await EnsureAffectiveCatalogAsync(ct);

            var affectiveAndPsychomor = await _uow.Repository<AffectiveXzAndPsychomotor, byte>()
                .GetAllAsync(ct);
            var affectiveAndPsychomorDto = (affectiveAndPsychomor ?? Enumerable.Empty<AffectiveXzAndPsychomotor>())
                .OrderBy(x => x.Id)
                .Select(x => new AffectiveAndPsychomotorDto
                {
                    Id = x.Id,
                    ItemsName = x.ItemsName,
                })
                .ToList();
            await AffectiveItemLabels.ApplyForSchoolAsync(_uow, schoolId, affectiveAndPsychomorDto, ct);

            var result = new AffectiveAndPsychomotorAndTotalAttendanceDto
            {
                AffectiveAndPsychomotor = affectiveAndPsychomorDto,
            };

            var currentYearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                x =>
                    x.Session!.SchoolInfoId == schoolId &&
                    x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.Session!);

            if (currentYearlyAndTermlyStatus == null)
            {
                result.TotalAttendance = 255;
            }
            else
            {
                result.TotalAttendance = currentYearlyAndTermlyStatus.TotalAttendance ?? 0;
            }

            return result;
        }

        /// <summary>
        /// Legacy class wire: ClassEnum,ClassLabel,Session,assessmentCategory,actionType[,psycho].
        /// ClassLabel may contain commas, so category/action/psycho are taken from the end.
        /// </summary>
        private static bool TryParsePerClassConcat(
            IReadOnlyList<string> itemList,
            out CurrentClass currentClass,
            out string assessmentCategory,
            out string actionType,
            out byte psychoAffectDefaultValue)
        {
            currentClass = default;
            assessmentCategory = string.Empty;
            actionType = string.Empty;
            psychoAffectDefaultValue = 7;

            if (itemList.Count < 5 || !int.TryParse(itemList[0], out var classValue))
            {
                return false;
            }

            currentClass = (CurrentClass)classValue;
            ParseCategoryActionPsychoFromEnd(itemList, minPrefixLength: 3, out assessmentCategory, out actionType, out psychoAffectDefaultValue);
            return IsZeroOrOne(assessmentCategory) && IsZeroOrOne(actionType);
        }

        /// <summary>
        /// Legacy arm wire: Arm,ClassEnum,ClassLabel,Session,assessmentCategory,actionType[,psycho].
        /// </summary>
        private static bool TryParsePerArmConcat(
            IReadOnlyList<string> itemList,
            out ArmName armName,
            out CurrentClass currentClass,
            out string assessmentCategory,
            out string actionType,
            out byte psychoAffectDefaultValue)
        {
            armName = default;
            currentClass = default;
            assessmentCategory = string.Empty;
            actionType = string.Empty;
            psychoAffectDefaultValue = 7;

            if (itemList.Count < 6 ||
                !int.TryParse(itemList[0], out var armValue) ||
                !int.TryParse(itemList[1], out var classValue))
            {
                return false;
            }

            armName = (ArmName)armValue;
            currentClass = (CurrentClass)classValue;
            ParseCategoryActionPsychoFromEnd(itemList, minPrefixLength: 4, out assessmentCategory, out actionType, out psychoAffectDefaultValue);
            return IsZeroOrOne(assessmentCategory) && IsZeroOrOne(actionType);
        }

        private static void ParseCategoryActionPsychoFromEnd(
            IReadOnlyList<string> itemList,
            int minPrefixLength,
            out string assessmentCategory,
            out string actionType,
            out byte psychoAffectDefaultValue)
        {
            psychoAffectDefaultValue = 7;

            // With psycho: [...prefix, session, category, action, psycho] where psycho is 0-6.
            // Psycho 0/1 is ambiguous with Delete wires ending ...,category,action (both 0/1):
            // only accept trailing 0/1 as psycho for Psychomotor Create (category=1, action=0).
            if (itemList.Count >= minPrefixLength + 3
                && byte.TryParse(itemList[^1], out var psycho)
                && psycho <= 6
                && IsZeroOrOne(itemList[^2])
                && IsZeroOrOne(itemList[^3])
                && (psycho > 1 || (itemList[^3] == "1" && itemList[^2] == "0")))
            {
                psychoAffectDefaultValue = psycho;
                actionType = itemList[^2];
                assessmentCategory = itemList[^3];
                return;
            }

            // Without psycho: [...prefix, session, category, action] — Delete never sends psycho.
            actionType = itemList[^1];
            assessmentCategory = itemList[^2];
        }

        private static bool IsZeroOrOne(string value) => value == "0" || value == "1";

        /// <summary>
        /// Legacy: PrepareAssessmentEnvironmentForAnArmStudents(concatParameters).
        /// Arm-change concat from the view:
        /// ArmName,ClassName,Session,assessmentCategory,actionType[,psychoAffectDefaultValue]
        /// </summary>
        public async Task<PrepareEnvironmentForStudentAssessmentReturnDto?> PrepareAssessmentEnvironmentForAnArmStudentsAsync(
            string userId,
            string concatParameters,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(concatParameters))
            {
                return InvalidRequest("concatParameters is required.");
            }

            var itemList = concatParameters.Split(',').ToList();
            // ArmName is enum-only; ClassName is "enum,ClassLabel", so wire format is:
            // Arm,ClassEnum,ClassLabel,Session,assessmentCategory,actionType[,psycho]
            // ClassLabel may contain commas — parse category/action/psycho from the end.
            if (!TryParsePerArmConcat(
                    itemList,
                    out var armName,
                    out var currentClass,
                    out var assessmentCategory,
                    out var actionType,
                    out var psychoAffectDefaultValue))
            {
                return InvalidRequest("Invalid concatParameters for per-arm assessment processing.");
            }

            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return InvalidRequest("Unable to resolve the current school.");
            }

            var classArm = await _uow.Repository<AxClassArm>().FindAsync(
                s =>
                    s.Classs!.SchoolInfoId == schoolId &&
                    s.ArmName == armName &&
                    s.Classs.ClassName == currentClass,
                ct,
                s => s.Classs!);
            if (classArm == null)
            {
                return InvalidRequest("Selected class arm was not found.");
            }

            var classArmId = classArm.Id;
            var prepareEnvironmentForStudentAssessmentReturn = new PrepareEnvironmentForStudentAssessmentReturnDto();

            if (assessmentCategory == "0" && actionType == "0")
            {
                prepareEnvironmentForStudentAssessmentReturn =
                    await PerArmStudentProcessingAsync(schoolId, classArmId, ct);
            }
            else if (assessmentCategory == "0" && actionType == "1")
            {
                var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
                var yearlyAndTermlyStatus = await yearlyAndTermlyStatusRepository.FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);

                var yearlyAndTermlyStatusId = yearlyAndTermlyStatus?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    prepareEnvironmentForStudentAssessmentReturn =
                        await PerArmStudentProcessingAsync(schoolId, classArmId, ct);
                    if (prepareEnvironmentForStudentAssessmentReturn.Message?.StartsWith("Success") == true)
                    {
                        await _uow.SaveChangesAsync(ct);
                        yearlyAndTermlyStatus = await yearlyAndTermlyStatusRepository.FindAsync(
                            x =>
                                x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                                x.Session!.SchoolInfoId == schoolId,
                            ct,
                            x => x.Session!);
                        yearlyAndTermlyStatusId = yearlyAndTermlyStatus?.Id;
                    }
                    else
                    {
                        return prepareEnvironmentForStudentAssessmentReturn;
                    }
                }

                if (string.IsNullOrWhiteSpace(yearlyAndTermlyStatusId))
                {
                    return prepareEnvironmentForStudentAssessmentReturn;
                }

                // Legacy: load with Subject only, then delete duplicate zero-CA rows (not every row).
                var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
                var currentAssessmentToDelete = await studentAssessmentRepository.WhereAsync(
                    s =>
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                        s.Student!.ClassArmId == classArmId,
                    ct,
                    c => c.Subject!);

                var byStudentGroup = currentAssessmentToDelete.GroupBy(s => s.StudentId);
                foreach (var each in byStudentGroup)
                {
                    var eachList = each.ToList();
                    var bySubjectGroup = eachList.GroupBy(x => x.Subject!.SubjectName).ToList();
                    var bySubjectGroupCount = bySubjectGroup.Count;
                    if (bySubjectGroupCount < eachList.Count)
                    {
                        // Means there are duplicate records
                        foreach (var item in bySubjectGroup)
                        {
                            var wrongRec = item.Where(x => x.Ca1 == 0 && x.Ca2 == 0 && x.Ca3 == 0);
                            foreach (var item1 in wrongRec)
                            {
                                // Delete by id — WhereAsync is AsNoTracking; attaching Include graphs throws.
                                await studentAssessmentRepository.DeleteAsync(item1.Id, ct);
                            }
                        }
                    }
                }

                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current assessment of the selected class-arm has successfully been DELETED.";
            }

            if (assessmentCategory == "1" && actionType == "0")
            {
                // Legacy: create current psychomotor and Affective for the class-arm.
                var studentsList = await _uow.Repository<TxStudentDetail>().WhereAsync(
                    s =>
                        (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning) &&
                        s.ClassArmId == classArmId,
                    ct,
                    a => a.ClassArm!.Classs!);

                var classTeacherId = (await _uow.Repository<TeacherClassArmTxAssignment>().FindAsync(
                    a => a.ArmId == classArmId,
                    ct))?.EmployeeId;
                if (string.IsNullOrWhiteSpace(classTeacherId))
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Sorry! Teacher needs to be assigned to the selected class-arm(s)."
                    };
                }

                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                if (yearlyAndTermlyStatus == null)
                {
                    return null;
                }

                var yearlyAndTermlyStatusId = yearlyAndTermlyStatus.Id;
                var getAffectiveAndPsychomotorItems = await GetAffectiveAndPsychomotorItemsAsync(schoolId, ct);
                if (getAffectiveAndPsychomotorItems.TotalAttendance == 255)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."
                    };
                }

                var affectiveAndPsycho = getAffectiveAndPsychomotorItems.AffectiveAndPsychomotor
                    ?? new List<AffectiveAndPsychomotorDto>();

                var createResult = await CreateMissingPsychomotorAssessmentsAsync(
                    studentsList,
                    yearlyAndTermlyStatusId,
                    affectiveAndPsycho,
                    psychoAffectDefaultValue,
                    _ => classTeacherId,
                    "Sorry! Teacher needs to be assigned to the selected class-arm(s).",
                    ct);

                prepareEnvironmentForStudentAssessmentReturn.Message = BuildPsychomotorCreateMessage(
                    createResult,
                    scopeLabel: "class-arm");
            }
            else if (assessmentCategory == "1" && actionType == "1")
            {
                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                if (yearlyAndTermlyStatus == null)
                {
                    return null;
                }

                // Match by assessment.ClassArmId (set at create), not Student.ClassArmId.
                var deleted = await DeletePsychomotorAssessmentsAsync(
                    s =>
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatus.Id &&
                        s.ClassArmId == classArmId,
                    ct);

                prepareEnvironmentForStudentAssessmentReturn.Message =
                    deleted > 0
                        ? "The current psychomotor and Affective of the selected class-arm has successfully been DELETED."
                        : "No psychomotor/Affective records were found to delete for the selected class-arm in the current term.";
            }

            return prepareEnvironmentForStudentAssessmentReturn;
        }

        /// <summary>
        /// Creates missing psycho/affective rows for each student's <em>current</em> ClassArmId.
        /// Completeness is scoped by ClassArmId so promoted students (e.g. into JSS 1 / SS 1)
        /// are not skipped because older-arm rows already exist for the same term.
        /// </summary>
        private async Task<PsychomotorCreateResult> CreateMissingPsychomotorAssessmentsAsync(
            IReadOnlyList<TxStudentDetail> students,
            string yearlyAndTermlyStatusId,
            IReadOnlyList<AffectiveAndPsychomotorDto> affectiveAndPsycho,
            byte psychoAffectDefaultValue,
            Func<string, string?> resolveFormMasterId,
            string missingTeacherMessage,
            CancellationToken ct)
        {
            var result = new PsychomotorCreateResult { StudentCount = students.Count };
            if (affectiveAndPsycho.Count == 0)
            {
                result.ErrorMessage =
                    "Failed! Psychomotor/Affective catalog items are missing. Ensure AffectiveAndPsychomotor items are seeded.";
                return result;
            }

            var repo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();

            foreach (var each in students)
            {
                var studentId = each.UserId;
                var armId = each.ClassArmId;
                if (string.IsNullOrWhiteSpace(armId))
                {
                    continue;
                }

                var existingForArm = await repo.WhereAsync(
                    r =>
                        r.StudentId == studentId &&
                        r.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                        r.ClassArmId == armId,
                    ct);

                var existingItemIds = existingForArm.Select(r => r.ItemId).ToHashSet();
                if (affectiveAndPsycho.All(item => existingItemIds.Contains(item.Id)))
                {
                    result.AlreadyComplete++;
                    continue;
                }

                var formMasterId = resolveFormMasterId(armId);
                if (string.IsNullOrWhiteSpace(formMasterId))
                {
                    result.ErrorMessage = missingTeacherMessage;
                    return result;
                }

                foreach (var eachItem in affectiveAndPsycho)
                {
                    if (existingItemIds.Contains(eachItem.Id))
                    {
                        continue;
                    }

                    repo.Add(new AffectiveAndPsychomoTxtorAssessment
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassArmId = armId,
                        StudentId = studentId,
                        YearlyAndTermlyStatusId = yearlyAndTermlyStatusId,
                        ItemId = eachItem.Id,
                        FormMasterId = formMasterId,
                        Score = psychoAffectDefaultValue,
                    });
                    result.Inserted++;
                }
            }

            if (result.Inserted > 0)
            {
                await _uow.SaveChangesAsync(ct);
            }

            return result;
        }

        private static string BuildPsychomotorCreateMessage(
            PsychomotorCreateResult result,
            string scopeLabel)
        {
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return result.ErrorMessage!;
            }

            if (result.StudentCount == 0)
            {
                return $"Sorry! No Fresher/Returning student was found in the selected {scopeLabel}.";
            }

            if (result.Inserted == 0)
            {
                return
                    $"Success! No new records were created — psychomotor/Affective for this {scopeLabel} " +
                    $"is already complete on each student's current class-arm for the current term " +
                    $"({result.AlreadyComplete} student(s) checked).";
            }

            return
                $"Success! Generated {result.Inserted} psychomotor/Affective record(s) " +
                $"for the selected {scopeLabel} ({result.StudentCount} student(s)).";
        }

        private sealed class PsychomotorCreateResult
        {
            public int Inserted { get; set; }
            public int StudentCount { get; set; }
            public int AlreadyComplete { get; set; }
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// Hard-deletes psychomotor/affective assessment rows matching <paramref name="predicate"/>.
        /// Uses Delete-by-id so AsNoTracking WhereAsync results still remove cleanly.
        /// </summary>
        private async Task<int> DeletePsychomotorAssessmentsAsync(
            Expression<Func<AffectiveAndPsychomoTxtorAssessment, bool>> predicate,
            CancellationToken ct)
        {
            var affectiveRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var rows = await affectiveRepo.WhereAsync(predicate, ct);
            if (rows == null || rows.Count == 0)
            {
                return 0;
            }

            var deleted = 0;
            foreach (var row in rows)
            {
                if (await affectiveRepo.DeleteAsync(row.Id, ct))
                {
                    deleted++;
                }
            }

            if (deleted > 0)
            {
                await _uow.SaveChangesAsync(ct);
            }

            return deleted;
        }

        private async Task<PrepareEnvironmentForStudentAssessmentReturnDto> PerArmStudentProcessingAsync(
            string schoolId,
            string classArmId,
            CancellationToken ct)
        {
            var studentsList = await _uow.Repository<TxStudentDetail>().WhereAsync(
                s =>
                    (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning) &&
                    s.ClassArmId == classArmId,
                ct,
                a => a.ClassArm!.Classs!,
                s => s.User!);

            return await _sessionAndTermService.PrepareAssessmentEnvironmentForStudentsAsync(
                schoolId,
                studentsList,
                requestStatus: null,
                ct);
        }

        private static string BuildPerClassConcatParameters(NewAssessmentProcessRequestDto request)
        {
            // Wire must match ClassEnum,ClassLabel,Session,category,action[,psycho]
            // so TryParsePerClassConcat / ParseCategoryActionPsychoFromEnd stay aligned.
            var parts = new List<string>
            {
                ((int)(request.ClassName ?? 0)).ToString(),
                string.Empty, // ClassLabel
                string.Empty, // Session
                request.AssessmentCategory ?? "0",
                request.ActionType ?? "0",
            };

            if (request.PsychoAffectDefaultValue.HasValue)
            {
                parts.Add(request.PsychoAffectDefaultValue.Value.ToString());
            }

            return string.Join(",", parts);
        }

        private static string BuildPerArmConcatParameters(NewAssessmentProcessRequestDto request)
        {
            // Wire must match Arm,ClassEnum,ClassLabel,Session,category,action[,psycho].
            var parts = new List<string>
            {
                ((int)(request.ArmName ?? 0)).ToString(),
                ((int)(request.ClassName ?? 0)).ToString(),
                string.Empty, // ClassLabel
                string.Empty, // Session
                request.AssessmentCategory ?? "0",
                request.ActionType ?? "0",
            };

            if (request.PsychoAffectDefaultValue.HasValue)
            {
                parts.Add(request.PsychoAffectDefaultValue.Value.ToString());
            }

            return string.Join(",", parts);
        }

        private static PrepareEnvironmentForStudentAssessmentReturnDto InvalidRequest(string message)
        {
            return new PrepareEnvironmentForStudentAssessmentReturnDto
            {
                Message = message,
            };
        }

        public Task<PrepareEnvironmentForStudentAssessmentReturnDto> GetAssessmentCompletionDefaultedTeachersAsync(
            string userId,
            CancellationToken ct = default)
            => _sessionAndTermService.PrepareAssessmentEnvironmentForTheWholeStudents(userId, 1, ct);
    }
}