using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;
using System.Security.Claims;

namespace NAITSSMS.API.Controllers.Api
{
    /// <summary>
    /// Teachers And Others Service Controller - Handles teacher-specific operations, assessments, and results
    /// Modern .NET 9 implementation with dependency injection
    /// Replaces legacy ServiceLocator.Get&lt;ITeachersAndOthersService&gt;() pattern
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TeachersAndOthersServiceController : ControllerBase
    {
        private readonly ITeachersAndOthersService _teachersAndOthersService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TeachersAndOthersServiceController> _logger;

        /// <summary>
        /// Constructor with dependency injection
        /// Replaces legacy: ServiceLocator.Get&lt;ITeachersAndOthersService&gt;();
        /// </summary>
        /// <param name="teachersAndOthersService">Teachers and others service for business logic</param>
        /// <param name="unitOfWork">Unit of work for transaction management</param>
        /// <param name="logger">Logger for diagnostics</param>
        public TeachersAndOthersServiceController(
            ITeachersAndOthersService teachersAndOthersService,
            IUnitOfWork unitOfWork,
            ILogger<TeachersAndOthersServiceController> logger)
        {
            _teachersAndOthersService = teachersAndOthersService ?? throw new ArgumentNullException(nameof(teachersAndOthersService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region GROUP 1: Teacher Context & Assignments

        /// <summary>
        /// Get teacher assigned classes and subjects
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetTeacherAssignedClassesAndSubjects()
        /// </summary>
        [HttpGet("teacher/assignments")]
        public async Task<IActionResult> GetTeacherAssignedClassesAndSubjects(CancellationToken ct)
        {
            try
            {
                var assignments = await _teachersAndOthersService.GetTeacherAssignedClassesAndSubjects(ct);
                return Ok(assignments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving teacher assigned classes and subjects");
                return StatusCode(500, new { message = "An error occurred while retrieving teacher assignments" });
            }
        }

        /// <summary>
        /// Get teacher assigned arms as form master
        /// Legacy: TeacherController.AssignedArmAsFormMaster → GetTeacherAssignedArmAsFormMaster()
        /// </summary>
        [HttpGet("teacher/assigned-arms")]
        public async Task<IActionResult> GetTeacherAssignedArmAsFormMaster(CancellationToken ct)
        {
            try
            {
                var arms = await _teachersAndOthersService.GetTeacherAssignedArmAsFormMaster(ct);
                return Ok(arms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving teacher assigned arms");
                return StatusCode(500, new { message = "An error occurred while retrieving assigned arms" });
            }
        }

        /// <summary>
        /// Legacy TeacherController.AssignedArmsStudents(armId, classLabel)
        /// </summary>
        [HttpGet("form-master/assigned-arms-students")]
        public async Task<IActionResult> AssignedArmsStudents(
            [FromQuery] string armId,
            [FromQuery] string? classLabel,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(armId))
                {
                    return BadRequest(new { message = "armId is required." });
                }

                var studentsList = await _teachersAndOthersService.GetStudentInAnArmForAssessment(armId, ct);
                return Ok(new
                {
                    classLabel,
                    armTermlyAssessmentStatus = studentsList.ArmTermlyAssessmentStatus,
                    studentDetails = studentsList.StudentDetails ?? Array.Empty<StudentDetailsDto>(),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assigned-arm students for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving arm students." });
            }
        }

        /// <summary>
        /// Legacy TeacherController.MarkTermCompleted(conCatParameters)
        /// conCatParameters = armId,status (0=Uncompleted, 1=Completed)
        /// </summary>
        [HttpGet("form-master/mark-term-completed")]
        public async Task<IActionResult> MarkTermCompleted(
            [FromQuery] string conCatParameters,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(conCatParameters))
                {
                    return Ok(false);
                }

                var status = await _teachersAndOthersService.ChangeTermlyAssessmentStatus(conCatParameters, ct);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating termly assessment status for parameters {Parameters}", conCatParameters);
                return Ok(false);
            }
        }

        /// <summary>
        /// Legacy TeacherController.PerArmCaResultProcessings(concatParameters)
        /// concatParameters = arm,classs,classLabel
        /// </summary>
        [HttpGet("form-master/per-arm-ca-result")]
        public async Task<IActionResult> PerArmCaResultProcessings(
            [FromQuery] string concatParameters,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(concatParameters))
                {
                    return Ok(new { message = (string?)null, studentsAccessment = Array.Empty<object>() });
                }

                var individualValues = concatParameters.Split(',').ToList();
                if (individualValues.Count < 3
                    || !Enum.TryParse<ArmName>(individualValues[0], true, out var arm)
                    || !Enum.TryParse<CurrentClass>(individualValues[1], true, out var clas))
                {
                    return BadRequest(new { message = "Invalid concatParameters. Expected arm,class,classLabel." });
                }

                var classLabel = individualValues[2] + " " + arm;
                var studentsReports = await _teachersAndOthersService.GetStudentsInAnArmForCaByClassNameAndArmName(clas, arm, ct);
                if (studentsReports == null || studentsReports.StatusCode == 1)
                {
                    return Ok(new
                    {
                        classLabel,
                        message = "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.",
                        studentsAccessment = Array.Empty<object>(),
                    });
                }

                var school = await _teachersAndOthersService.GetSchoolModel(ct);
                return Ok(new
                {
                    classLabel,
                    message = (string?)null,
                    school,
                    caSetting = studentsReports.CasSetting,
                    schoolHead = studentsReports.SchoolHeadInfo,
                    armFormMaster = studentsReports.AssessmentResultOutputXtractionForFormTeacher?.FormMaster,
                    totalAttendance = studentsReports.AssessmentResultOutputXtractionForFormTeacher?.YearlyAndTermlyAssessmentStatus?.TotalAttendance,
                    studentsAccessment = studentsReports.StudentsAccessment,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PerArmCaResultProcessings");
                return StatusCode(500, new { message = "An error occurred while processing CA report sheet." });
            }
        }

        /// <summary>
        /// Legacy TeacherController.PerArmResultProcessings(concatParameters)
        /// concatParameters = arm,classs,classLabel
        /// </summary>
        [HttpGet("form-master/per-arm-result")]
        public async Task<IActionResult> PerArmResultProcessings(
            [FromQuery] string concatParameters,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(concatParameters))
                {
                    return Ok(new { message = (string?)null, studentsAccessment = Array.Empty<object>() });
                }

                var individualValues = concatParameters.Split(',').ToList();
                if (individualValues.Count < 3
                    || !Enum.TryParse<ArmName>(individualValues[0], true, out var arm)
                    || !Enum.TryParse<CurrentClass>(individualValues[1], true, out var clas))
                {
                    return BadRequest(new { message = "Invalid concatParameters. Expected arm,class,classLabel." });
                }

                var classLabel = individualValues[2] + " " + arm;
                var studentsReports = await _teachersAndOthersService.ProcessClassArmResultByClassNameAndArmName(clas, arm, ct);
                if (studentsReports == null || studentsReports.StatusCode == 1)
                {
                    return Ok(new
                    {
                        classLabel,
                        message = "Failed! Result processing environment has yet to be prepared or no student in '" + classLabel + "'; contact Admin Officer.",
                        studentsAccessment = Array.Empty<object>(),
                    });
                }

                var school = await _teachersAndOthersService.GetSchoolModel(ct);
                return Ok(new
                {
                    classLabel,
                    message = (string?)null,
                    school,
                    studentsAttendance = studentsReports.StudentsAttendance,
                    caSetting = studentsReports.CasSetting,
                    schoolHead = studentsReports.SchoolHeadInfo,
                    armFormMaster = studentsReports.AffectiveAndPsychomotorAssessment?.FirstOrDefault()?.FormMaster,
                    affectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment,
                    totalAttendance = studentsReports.AffectiveAndPsychomotorAssessment?.FirstOrDefault()?.YearlyAndTermlyAssessmentStatus?.TotalAttendance,
                    studentsAccessment = studentsReports.StudentsAccessment,
                    statusCode = studentsReports.StatusCode,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PerArmResultProcessings");
                return StatusCode(500, new { message = "An error occurred while processing termly report sheet." });
            }
        }

        /// <summary>
        /// Legacy TeacherController.ThirdTermResultAlone(concatParameters)
        /// </summary>
        [HttpGet("form-master/third-term-result")]
        public async Task<IActionResult> ThirdTermResultAlone(
            [FromQuery] string concatParameters,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(concatParameters))
                {
                    return Ok(new { message = (string?)null, studentsAccessment = Array.Empty<object>() });
                }

                var individualValues = concatParameters.Split(',').ToList();
                if (individualValues.Count < 3
                    || !Enum.TryParse<ArmName>(individualValues[0], true, out var arm)
                    || !Enum.TryParse<CurrentClass>(individualValues[1], true, out var clas))
                {
                    return BadRequest(new { message = "Invalid concatParameters. Expected arm,class,classLabel." });
                }

                var classLabel = individualValues[2] + " " + arm;
                var studentsReports = await _teachersAndOthersService.ProcessThirdTermClassArmResultByClassNameAndArmName(clas, arm, ct);
                if (studentsReports == null || studentsReports.StatusCode == 1)
                {
                    return Ok(new
                    {
                        classLabel,
                        message = "Failed! Result processing environment has yet to be prepared or no student in '" + classLabel + "'; contact Admin Officer.",
                        studentsAccessment = Array.Empty<object>(),
                    });
                }

                var school = await _teachersAndOthersService.GetSchoolModel(ct);
                return Ok(new
                {
                    classLabel,
                    message = (string?)null,
                    school,
                    studentsAttendance = studentsReports.StudentsAttendance,
                    caSetting = studentsReports.CasSetting,
                    schoolHead = studentsReports.SchoolHeadInfo,
                    armFormMaster = studentsReports.AffectiveAndPsychomotorAssessment?.FirstOrDefault()?.FormMaster,
                    affectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment,
                    totalAttendance = studentsReports.AffectiveAndPsychomotorAssessment?.FirstOrDefault()?.YearlyAndTermlyAssessmentStatus?.TotalAttendance,
                    studentsAccessment = studentsReports.StudentsAccessment,
                    statusCode = studentsReports.StatusCode,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ThirdTermResultAlone");
                return StatusCode(500, new { message = "An error occurred while processing third term result." });
            }
        }

        /// <summary>
        /// Get class and arm dropdown for teachers
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetClassArmForDropDown()
        /// </summary>
        [HttpGet("dropdowns/class-arm")]
        public async Task<IActionResult> GetClassArmForDropDown(CancellationToken ct)
        {
            try
            {
                var dropdown = await _teachersAndOthersService.GetClassArmForDropDown(ct);
                return Ok(dropdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving class arm dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving dropdown" });
            }
        }

        #endregion

        #region GROUP 2: Student Assessments & CA Management

        /// <summary>
        /// Get students in an arm by arm ID and CA setting
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetStudentsInAnArmByArmIdAndCaSetting()
        /// </summary>
        [HttpGet("arm/{armId}/students/ca-setting")]
        public async Task<IActionResult> GetStudentsInAnArmByArmIdAndCaSetting(
            string armId,
            [FromQuery] string subjectId,
            CancellationToken ct)
        {
            try
            {
                var students = await _teachersAndOthersService.GetStudentsInAnArmByArmIdAndCaSetting(armId, subjectId, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving students" });
            }
        }

        /// <summary>
        /// Get students in an arm for assessment
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetStudentInAnArmForAssessment()
        /// </summary>
        [HttpGet("arm/{armId}/students/assessment")]
        public async Task<IActionResult> GetStudentInAnArmForAssessment(string armId, CancellationToken ct)
        {
            try
            {
                var assessment = await _teachersAndOthersService.GetStudentInAnArmForAssessment(armId, ct);
                return Ok(assessment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student assessment for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving student assessment" });
            }
        }

        /// <summary>
        /// Create CA and exams scores
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().CreateCasAndExamsScore()
        /// </summary>
        [HttpPost("assessment/ca-exams-scores")]
        public async Task<IActionResult> CreateCasAndExamsScore(
            [FromBody] List<StudentsAccessmentDto>? studentsAccessmentScores,
            CancellationToken ct)
        {
            try
            {
                if (studentsAccessmentScores == null || studentsAccessmentScores.Count == 0)
                {
                    return BadRequest(new { message = "No CA and exams scores were provided." });
                }

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _teachersAndOthersService.CreateCasAndExamsScore(studentsAccessmentScores, ct);

                if (result)
                {
                    return Ok(new { message = "CA and exams scores created successfully" });
                }

                return BadRequest(new { message = "Failed to create CA and exams scores. Ensure you are signed in as a teacher with a staff profile." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating CA and exams scores");
                return StatusCode(500, new { message = "An error occurred while creating CA and exams scores" });
            }
        }

        /// <summary>
        /// Get students in arm for CA by class name and arm name
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetStudentsInAnArmForCaByClassNameAndArmName()
        /// </summary>
        //[HttpGet("ca/class/{className}/arm/{armName}")]
        //public async Task<IActionResult> GetStudentsInAnArmForCaByClassNameAndArmName(
        //    CurrentClass className,
        //    ArmName armName,
        //    CancellationToken ct)
        //{
        //    try
        //    {
        //        var result = await _teachersAndOthersService.GetStudentsInAnArmForCaByClassNameAndArmName(className, armName, ct);
        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving CA for class {ClassName} arm {ArmName}", className, armName);
        //        return StatusCode(500, new { message = "An error occurred while retrieving CA" });
        //    }
        //}

        /// <summary>
        /// Legacy TeacherController.AffectiveAndPsychomotorRemark(concatParameter)
        /// concatParameter = studentId,admissionNo,studentName,studentClass,studentArm,armId
        /// </summary>
        [HttpGet("form-master/affective-psychomotor-remark")]
        public async Task<IActionResult> AffectiveAndPsychomotorRemark(
            [FromQuery] string concatParameter,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(concatParameter))
                {
                    return BadRequest(new { message = "concatParameter is required." });
                }

                var eachValue = concatParameter.Split(',').ToList();
                if (eachValue.Count < 6)
                {
                    return BadRequest(new { message = "Invalid concatParameter." });
                }

                var studentId = eachValue[0];
                var admissionNo = eachValue[1];
                var studentName = eachValue[2];
                var studentClass = eachValue[3];
                var studentArm = eachValue[4];
                var armId = eachValue[5];

                var existing = await _teachersAndOthersService.GetAffectiveAndPsychomotiveForReUpdateWithAttendance(studentId, ct);
                if (existing?.AffectiveAndPsychomotorAssessmentDto != null && existing.AffectiveAndPsychomotorAssessmentDto.Any())
                {
                    return Ok(new
                    {
                        mode = "reupdate",
                        studentId,
                        admissionNo,
                        studentName,
                        studentClass,
                        studentArm,
                        armId,
                        attendance = existing.Attendance,
                        teacherRemarks = existing.TeacherRemarks,
                        schoolHeadRemarks = existing.SchoolHeadRemarks,
                        totalAttendance = existing.TotalAttendance,
                        items = existing.AffectiveAndPsychomotorAssessmentDto.Select(NormalizeRemarkItem),
                    });
                }

                var catalog = await _teachersAndOthersService.GetAffectiveAndPsychomotorItems(ct);
                if (catalog?.AffectiveAndPsychomotor == null || !catalog.AffectiveAndPsychomotor.Any())
                {
                    return Ok(new
                    {
                        mode = "empty",
                        studentId,
                        admissionNo,
                        studentName,
                        studentClass,
                        studentArm,
                        armId,
                        message = "Failed! environment has not yet been prepared. Contact registry Officer.",
                        items = Array.Empty<object>(),
                    });
                }

                return Ok(new
                {
                    mode = "create",
                    studentId,
                    admissionNo,
                    studentName,
                    studentClass,
                    studentArm,
                    armId,
                    attendance = 0,
                    teacherRemarks = "",
                    schoolHeadRemarks = "",
                    totalAttendance = catalog.TotalAttendance,
                    items = catalog.AffectiveAndPsychomotor.Select(x => new
                    {
                        itemId = x.Id,
                        itemsName = x.ItemsName,
                        score = (byte?)null,
                    }),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading affective/psychomotor remark");
                return StatusCode(500, new
                {
                    message = "An error occurred while loading remark.",
                    detail = ex.Message,
                });
            }
        }

        private static object NormalizeRemarkItem(AffectiveAndPsychomotorAssessmentDto item)
        {
            return new
            {
                itemId = item.ItemId,
                itemsName = item.AffectiveAndPsychomotor?.ItemsName ?? string.Empty,
                score = item.Score,
            };
        }

        /// <summary>
        /// Legacy TeacherController.SubmitAffectiveAndPsychomotorScore
        /// </summary>
        [HttpPost("form-master/affective-psychomotor-scores")]
        public async Task<IActionResult> SubmitAffectiveAndPsychomotorScore(
            [FromBody] FormMasterAffectivePsychomotorSubmitDto model,
            CancellationToken ct)
        {
            try
            {
                if (model == null || model.Scores == null || model.Scores.Count == 0)
                {
                    return BadRequest(new { message = "No scores were provided." });
                }

                var status = await _teachersAndOthersService.CreateAffectiveAndPsychomotorScore(model, ct);
                if (!status)
                {
                    return BadRequest(new { message = "Failed to submit affective and psychomotor scores." });
                }

                return Ok(new { message = "Your enteries has Successfully been Submited." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting affective/psychomotor scores");
                return StatusCode(500, new { message = "An error occurred while submitting scores." });
            }
        }

        /// <summary>
        /// Get affective and psychomotor for re-update with attendance
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetAffectiveAndPsychomotiveForReUpdateWithAttendance()
        /// </summary>
        [HttpGet("students/{studentId}/affective-psychomotor")]
        public async Task<IActionResult> GetAffectiveAndPsychomotiveForReUpdateWithAttendance(
            string studentId,
            CancellationToken ct)
        {
            try
            {
                var result = await _teachersAndOthersService.GetAffectiveAndPsychomotiveForReUpdateWithAttendance(studentId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving affective and psychomotor for student {StudentId}", studentId);
                return StatusCode(500, new { message = "An error occurred while retrieving affective and psychomotor data" });
            }
        }

        /// <summary>
        /// Get students in an arm by arm ID and CA setting for unentered CA scores
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasScores()
        /// </summary>
        [HttpGet("arm/{armId}/students/ca-unentered")]
        public async Task<IActionResult> GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasScores(
            string armId,
            [FromQuery] string subjectId,
            CancellationToken ct)
        {
            try
            {
                var students = await _teachersAndOthersService.GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasScores(armId, subjectId, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unentered CA scores for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving unentered CA scores" });
            }
        }

        /// <summary>
        /// Get students in an arm by arm ID and CA setting for unentered CA and exams scores
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasAndExamsScores()
        /// </summary>
        [HttpGet("arm/{armId}/students/ca-exams-unentered")]
        public async Task<IActionResult> GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasAndExamsScores(
            string armId,
            [FromQuery] string subjectId,
            CancellationToken ct)
        {
            try
            {
                var students = await _teachersAndOthersService.GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasAndExamsScores(armId, subjectId, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unentered CA and exams scores for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving unentered scores" });
            }
        }

        /// <summary>
        /// Get unentered student remark by arm ID
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetUnenteredStudentRemarkByArmId()
        /// </summary>
        [HttpGet("arm/{armId}/remarks/unentered")]
        public async Task<IActionResult> GetUnenteredStudentRemarkByArmId(string armId, CancellationToken ct)
        {
            try
            {
                var remarks = await _teachersAndOthersService.GetUnenteredStudentRemarkByArmId(armId, ct);
                return Ok(remarks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unentered remarks for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving unentered remarks" });
            }
        }

        /// <summary>
        /// Get uncompleted CAs and exams score entering
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetUncompletedCAsandExamsScoreEntering()
        /// </summary>
        [HttpGet("teacher/uncompleted-ca-exams")]
        public async Task<IActionResult> GetUncompletedCAsandExamsScoreEntering(CancellationToken ct)
        {
            try
            {
                var uncompleted = await _teachersAndOthersService.GetUncompletedCAsandExamsScoreEntering(ct);
                return Ok(uncompleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving uncompleted CA and exams entries");
                return StatusCode(500, new { message = "An error occurred while retrieving uncompleted entries" });
            }
        }

        /// <summary>
        /// Get uncompleted CAs score entering
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetUncompletedCAsScoreEntering()
        /// </summary>
        [HttpGet("teacher/uncompleted-ca")]
        public async Task<IActionResult> GetUncompletedCAsScoreEntering(CancellationToken ct)
        {
            try
            {
                var uncompleted = await _teachersAndOthersService.GetUncompletedCAsScoreEntering(ct);
                return Ok(uncompleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving uncompleted CA entries");
                return StatusCode(500, new { message = "An error occurred while retrieving uncompleted CA entries" });
            }
        }

        #endregion

        #region GROUP 3: Result Processing & Report Sheets

        /// <summary>
        /// Get termly assessment for selected student
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetTermlyAssessmentForSelectedStudent()
        /// </summary>
        [HttpGet("students/{studentId}/termly-assessment")]
        public async Task<IActionResult> GetTermlyAssessmentForSelectedStudent(
            string studentId,
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            CancellationToken ct)
        {
            try
            {
                var assessment = await _teachersAndOthersService.GetTermlyAssessmentForSelectedStudent(studentId, sessionId, termStatus, ct);
                return Ok(assessment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving termly assessment for student {StudentId}", studentId);
                return StatusCode(500, new { message = "An error occurred while retrieving termly assessment" });
            }
        }

        /// <summary>
        /// Get yearly assessment for selected student
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetYearlyAssessmentForSelectedStudent()
        /// </summary>
        [HttpGet("students/{studentId}/yearly-assessment")]
        public async Task<IActionResult> GetYearlyAssessmentForSelectedStudent(
            string studentId,
            [FromQuery] string selectedSessionId,
            CancellationToken ct)
        {
            try
            {
                var assessment = await _teachersAndOthersService.GetYearlyAssessmentForSelectedStudent(studentId, selectedSessionId, ct);
                return Ok(assessment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving yearly assessment for student {StudentId}", studentId);
                return StatusCode(500, new { message = "An error occurred while retrieving yearly assessment" });
            }
        }

        /// <summary>
        /// Get student yearly report sheet by arm ID
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetStudentYearlyReportSheet()
        /// </summary>
        [HttpGet("arms/{armId}/yearly-report")]
        public async Task<IActionResult> GetStudentYearlyReportSheet(string armId, CancellationToken ct)
        {
            try
            {
                var report = await _teachersAndOthersService.GetStudentYearlyReportSheet(armId, ct);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving yearly report sheet for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving yearly report sheet" });
            }
        }

        /// <summary>
        /// Process class result by class name
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ProcessClassResultByClassName()
        /// </summary>
        [HttpPost("results/class/process")]
        public async Task<IActionResult> ProcessClassResultByClassName(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing class result for {ClassName}", className);
                var result = await _teachersAndOthersService.ProcessClassResultByClassName(sessionId, termStatus, className, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing class result for {ClassName}", className);
                return StatusCode(500, new { message = "An error occurred while processing class result" });
            }
        }

        /// <summary>
        /// Process class arm result by class name and arm name
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ProcessClassArmResultByClassNameAndArmName()
        /// </summary>
        [HttpPost("results/arm/process")]
        public async Task<IActionResult> ProcessClassArmResultByClassNameAndArmName(
            [FromQuery] CurrentClass className,
            [FromQuery] ArmName armName,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing arm result for {ClassName} {ArmName}", className, armName);
                var result = await _teachersAndOthersService.ProcessClassArmResultByClassNameAndArmName(className, armName, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing arm result for {ClassName} {ArmName}", className, armName);
                return StatusCode(500, new { message = "An error occurred while processing arm result" });
            }
        }

        /// <summary>
        /// Process class arm result by session, term, class name and arm name
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ProcessClassArmResultByClassNameAndArmName(sessionId, term, class, arm)
        /// </summary>
        [HttpPost("results/arm/process-detailed")]
        public async Task<IActionResult> ProcessClassArmResultByClassNameAndArmNameDetailed(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            [FromQuery] ArmName armName,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing detailed arm result for {ClassName} {ArmName}", className, armName);
                var result = await _teachersAndOthersService.ProcessClassArmResultByClassNameAndArmName(sessionId, termStatus, className, armName, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing detailed arm result");
                return StatusCode(500, new { message = "An error occurred while processing detailed arm result" });
            }
        }

        /// <summary>
        /// Process third term class result by session, term and class name
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetClassThirdTermResult()
        /// </summary>
        [HttpPost("results/class/third-term")]
        public async Task<IActionResult> GetClassThirdTermResult(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing third term class result for {ClassName}", className);
                var result = await _teachersAndOthersService.GetClassThirdTermResult(sessionId, termStatus, className, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing third term class result for {ClassName}", className);
                return StatusCode(500, new { message = "An error occurred while processing third term class result" });
            }
        }

        /// <summary>
        /// Process third term class arm result by class name and arm name
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ProcessThirdTermClassArmResultByClassNameAndArmName()
        /// </summary>
        [HttpPost("results/arm/third-term")]
        public async Task<IActionResult> ProcessThirdTermClassArmResultByClassNameAndArmName(
            [FromQuery] CurrentClass className,
            [FromQuery] ArmName armName,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing third term result for {ClassName} {ArmName}", className, armName);
                var result = await _teachersAndOthersService.ProcessThirdTermClassArmResultByClassNameAndArmName(className, armName, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing third term result");
                return StatusCode(500, new { message = "An error occurred while processing third term result" });
            }
        }

        /// <summary>
        /// Process all results
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ProcessAllResults()
        /// </summary>
        [HttpPost("results/process-all")]
        public async Task<IActionResult> ProcessAllResults(CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing all results");
                var result = await _teachersAndOthersService.ProcessAllResults(ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing all results");
                return StatusCode(500, new { message = "An error occurred while processing all results" });
            }
        }

        /// <summary>
        /// Process class broadsheet
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ProcessClassBroadSheet()
        /// </summary>
        [HttpPost("results/broadsheet/class")]
        public async Task<IActionResult> ProcessClassBroadSheet(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing class broadsheet for {ClassName}", className);
                var result = await _teachersAndOthersService.ProcessClassBroadSheet(sessionId, termStatus, className, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing class broadsheet");
                return StatusCode(500, new { message = "An error occurred while processing class broadsheet" });
            }
        }

        /// <summary>
        /// Process arm broadsheet
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ProcessArmBroadSheet()
        /// </summary>
        [HttpPost("results/broadsheet/arm")]
        public async Task<IActionResult> ProcessArmBroadSheet(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            [FromQuery] ArmName armName,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing arm broadsheet for {ClassName} {ArmName}", className, armName);
                var result = await _teachersAndOthersService.ProcessArmBroadSheet(sessionId, termStatus, className, armName, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing arm broadsheet");
                return StatusCode(500, new { message = "An error occurred while processing arm broadsheet" });
            }
        }

        #endregion

        #region GROUP 4: School Info & Validation

        /// <summary>
        /// Get school model for student and parent
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSchoolModelForStudentAndParent()
        /// </summary>
        [HttpGet("school/info/student-parent")]
        public async Task<IActionResult> GetSchoolModelForStudentAndParent(CancellationToken ct)
        {
            try
            {
                var schoolInfo = await _teachersAndOthersService.GetSchoolModelForStudentAndParent(ct);
                return Ok(schoolInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving school info for student/parent");
                return StatusCode(500, new { message = "An error occurred while retrieving school info" });
            }
        }

        /// <summary>
        /// Get school model for registration officer
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSchoolModelForSchoolRegistrationOfficer()
        /// </summary>
        [HttpGet("school/info/registration-officer")]
        public async Task<IActionResult> GetSchoolModelForSchoolRegistrationOfficer(CancellationToken ct)
        {
            try
            {
                var schoolInfo = await _teachersAndOthersService.GetSchoolModelForSchoolRegistrationOfficer(ct);
                return Ok(schoolInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving school info for registration officer");
                return StatusCode(500, new { message = "An error occurred while retrieving school info" });
            }
        }

        /// <summary>
        /// Get school model
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSchoolModel()
        /// </summary>
        [HttpGet("school/info")]
        public async Task<IActionResult> GetSchoolModel(CancellationToken ct)
        {
            try
            {
                var schoolInfo = await _teachersAndOthersService.GetSchoolModel(ct);
                return Ok(schoolInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving school model");
                return StatusCode(500, new { message = "An error occurred while retrieving school model" });
            }
        }

        /// <summary>
        /// Verify and get school info
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().VerifyAndGetSchoolInfo()
        /// </summary>
        [HttpGet("school/info/verify")]
        public async Task<IActionResult> VerifyAndGetSchoolInfo(CancellationToken ct)
        {
            try
            {
                var schoolInfo = await _teachersAndOthersService.VerifyAndGetSchoolInfo(ct);
                return Ok(schoolInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying school info");
                return StatusCode(500, new { message = "An error occurred while verifying school info" });
            }
        }

        /// <summary>
        /// Validate students promotion
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().StudentsPromotionValidations()
        /// </summary>
        [HttpGet("promotion/validate")]
        public async Task<IActionResult> StudentsPromotionValidations(CancellationToken ct)
        {
            try
            {
                var validationResult = await _teachersAndOthersService.StudentsPromotionValidations(ct);
                return Ok(new { message = validationResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating student promotion");
                return StatusCode(500, new { message = "An error occurred while validating promotion" });
            }
        }

        #endregion

        #region GROUP 5: Dropdowns & Utilities

        /// <summary>
        /// Get sessions dropdown
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSessionsDropdown()
        /// </summary>
        [HttpGet("dropdowns/sessions")]
        public async Task<IActionResult> GetSessionsDropdown(CancellationToken ct)
        {
            try
            {
                var sessions = await _teachersAndOthersService.GetSessionsDropdown(ct);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving sessions" });
            }
        }

        /// <summary>
        /// Get terms dropdown
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetTermsDropdown()
        /// </summary>
        [HttpGet("dropdowns/terms")]
        public async Task<IActionResult> GetTermsDropdown(CancellationToken ct)
        {
            try
            {
                var terms = await _teachersAndOthersService.GetTermsDropdown(ct);
                return Ok(terms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving terms dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving terms" });
            }
        }

        /// <summary>
        /// Change system implementation type
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().ChangeSystemImplementationType()
        /// </summary>
        [HttpPost("system/implementation-type/change")]
        public async Task<IActionResult> ChangeSystemImplementationType(
            [FromQuery] string concatParameters,
            CancellationToken ct)
        {
            try
            {
                var result = await _teachersAndOthersService.ChangeSystemImplementationType(concatParameters, ct);

                if (result)
                {
                    return Ok(new { message = "System implementation type changed successfully" });
                }

                return BadRequest(new { message = "Failed to change system implementation type" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing system implementation type");
                return StatusCode(500, new { message = "An error occurred while changing implementation type" });
            }
        }

        #endregion

        #region GROUP 6: SMS Management (Teacher Context)

        /// <summary>
        /// Get network type for phone number
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetNetworkType()
        /// </summary>
        [HttpGet("sms/network-type")]
        public async Task<IActionResult> GetNetworkType([FromQuery] string telNo, CancellationToken ct)
        {
            try
            {
                var networkType = await _teachersAndOthersService.GetNetworkType(telNo, ct);
                return Ok(new { networkType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining network type");
                return StatusCode(500, new { message = "An error occurred while determining network type" });
            }
        }

        /// <summary>
        /// Get SMS token and key only
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSmsTokenAndKeyOnly()
        /// </summary>
        [HttpGet("sms/token-key-only")]
        public async Task<IActionResult> GetSmsTokenAndKeyOnly(CancellationToken ct)
        {
            try
            {
                var tokenKey = await _teachersAndOthersService.GetSmsTokenAndKeyOnly(ct);
                return Ok(new { tokenKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SMS token and key");
                return StatusCode(500, new { message = "An error occurred while retrieving SMS token and key" });
            }
        }

        /// <summary>
        /// Get SMS token and key
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSmsTokenAndKey()
        /// </summary>
        [HttpGet("sms/token-key")]
        public async Task<IActionResult> GetSmsTokenAndKey(CancellationToken ct)
        {
            try
            {
                var tokenKey = await _teachersAndOthersService.GetSmsTokenAndKey(ct);
                return Ok(new { tokenKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SMS token and key");
                return StatusCode(500, new { message = "An error occurred while retrieving SMS token and key" });
            }
        }
        private string? GetCurrentUserId()
        {
            return User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.PrimarySid)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
        }
        /// <summary>
        /// Get and encode credentials
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetAndEncodeCredentials()
        /// </summary>
        [HttpGet("sms/credentials/encode")]
        public async Task<IActionResult> GetAndEncodeCredentials(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                var credentials = await _teachersAndOthersService.GetAndEncodeCredentials(userId, ct);
                return Ok(new { credentials });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encoding credentials");
                return StatusCode(500, new { message = "An error occurred while encoding credentials" });
            }
        }

        /// <summary>
        /// Get system info for SMS
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSystemInfoForSms()
        /// </summary>
        [HttpGet("sms/system-info")]
        public async Task<IActionResult> GetSystemInfoForSms(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                var systemInfo = await _teachersAndOthersService.GetSystemInfoForSms(userId, ct);
                return Ok(systemInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system SMS info");
                return StatusCode(500, new { message = "An error occurred while retrieving system SMS info" });
            }
        }

        /// <summary>
        /// Get school info for SMS
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSchoolInfoForSms()
        /// </summary>
        [HttpGet("sms/school-info")]
        public async Task<IActionResult> GetSchoolInfoForSms(CancellationToken ct)
        {
            try
            {
                var schoolInfo = await _teachersAndOthersService.GetSchoolInfoForSms(ct);
                return Ok(schoolInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving school info");
                return StatusCode(500, new { message = "An error occurred while retrieving school info" });
            }
        }

        /// <summary>
        /// Update students guardian info
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().StudentsGuardianInfoUpdate()
        /// </summary>
        [HttpPost("students/guardian-info/update")]
        [HttpPost("update-students-guardian-for-sms")]
        public async Task<IActionResult> StudentsGuardianInfoUpdate(
            [FromBody] List<StudentsGuardianInfoUpdateDto> model,
            CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _teachersAndOthersService.StudentsGuardianInfoUpdate(model, ct);

                if (result)
                {
                    return Ok(new { message = "Guardian information updated successfully" });
                }

                return BadRequest(new { message = "Failed to update guardian information" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating guardian information");
                return StatusCode(500, new { message = "An error occurred while updating guardian information" });
            }
        }

        /// <summary>
        /// Get system ID
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSystemId()
        /// </summary>
        [HttpGet("system/id")]
        public IActionResult GetSystemId()
        {
            try
            {
                var systemId = _teachersAndOthersService.GetSystemId();
                return Ok(new { systemId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system ID");
                return StatusCode(500, new { message = "An error occurred while retrieving system ID" });
            }
        }

        #endregion

        #region GROUP 7: CBT Management

        /// <summary>
        /// Create CBT question and answer
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().CreateQuestionsAndAnswer()
        /// </summary>
        [HttpPost("cbt/questions")]
        public async Task<IActionResult> CreateQuestionsAndAnswer(
            [FromBody] CbtQuestionAndAnswerDto model,
            CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                await _teachersAndOthersService.CreateQuestionsAndAnswer(model, ct);
                return Ok(new { message = "CBT question created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating CBT question");
                return StatusCode(500, new { message = "An error occurred while creating CBT question" });
            }
        }

        /// <summary>
        /// Get CBT questions by reference ID
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetCbtQuestionsByReferenceId()
        /// </summary>
        [HttpGet("cbt/questions/by-reference")]
        public async Task<IActionResult> GetCbtQuestionsByReferenceId(
            [FromQuery] string subjectId,
            [FromQuery] string classId,
            [FromQuery] byte termId,
            [FromQuery] AssessmentType assessmentType,
            CancellationToken ct)
        {
            try
            {
                var questions = await _teachersAndOthersService.GetCbtQuestionsByReferenceId(subjectId, classId, termId, assessmentType, ct);
                return Ok(questions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving CBT questions");
                return StatusCode(500, new { message = "An error occurred while retrieving CBT questions" });
            }
        }

        /// <summary>
        /// Get CBT question and answer by ID
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetCbtQuestionAndAnswerById()
        /// </summary>
        [HttpGet("cbt/questions/{questionId}")]
        public async Task<IActionResult> GetCbtQuestionAndAnswerById(string questionId, CancellationToken ct)
        {
            try
            {
                var question = await _teachersAndOthersService.GetCbtQuestionAndAnswerById(questionId, ct);
                return Ok(question);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving CBT question {QuestionId}", questionId);
                return StatusCode(500, new { message = "An error occurred while retrieving CBT question" });
            }
        }

        /// <summary>
        /// Update CBT questions and answer
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().UpdateQuestionsAndAnswer()
        /// </summary>
        [HttpPut("cbt/questions")]
        public async Task<IActionResult> UpdateQuestionsAndAnswer(
            [FromBody] CbtQuestionAndAnswerDto questionAndAnswer,
            CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                await _teachersAndOthersService.UpdateQuestionsAndAnswer(questionAndAnswer, ct);
                return Ok(new { message = "CBT question updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating CBT question");
                return StatusCode(500, new { message = "An error occurred while updating CBT question" });
            }
        }

        /// <summary>
        /// Delete CBT question
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().DeleteQuestion()
        /// </summary>
        [HttpDelete("cbt/questions/{questionId}")]
        public async Task<IActionResult> DeleteQuestion(string questionId, CancellationToken ct)
        {
            try
            {
                var result = await _teachersAndOthersService.DeleteQuestion(questionId, ct);

                if (result)
                {
                    return Ok(new { message = "CBT question deleted successfully" });
                }

                return NotFound(new { message = "Question not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting CBT question {QuestionId}", questionId);
                return StatusCode(500, new { message = "An error occurred while deleting CBT question" });
            }
        }

        /// <summary>
        /// Get question by search parameter
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetQuestionByParameter()
        /// </summary>
        [HttpGet("cbt/questions/search")]
        public async Task<IActionResult> GetQuestionByParameter(
            [FromQuery] string searchParameter,
            CancellationToken ct)
        {
            try
            {
                var questions = await _teachersAndOthersService.GetQuestionByParameter(searchParameter, ct);
                return Ok(questions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching CBT questions");
                return StatusCode(500, new { message = "An error occurred while searching CBT questions" });
            }
        }

        /// <summary>
        /// Get CBT questions (with generation options)
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetQuestions()
        /// </summary>
        [HttpPost("cbt/questions/generate")]
        public async Task<IActionResult> GetQuestions(
            [FromQuery] QuestionProcessingActionType actionType,
            [FromQuery] byte termId,
            [FromQuery] string assessmentType,
            [FromQuery] string classId,
            [FromQuery] string subjectId,
            [FromQuery] int noOfQuestions,
            CancellationToken ct)
        {
            try
            {
                var questions = await _teachersAndOthersService.GetQuestions(actionType, termId, assessmentType, classId, subjectId, noOfQuestions, ct);
                return Ok(questions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating CBT questions");
                return StatusCode(500, new { message = "An error occurred while generating CBT questions" });
            }
        }

        /// <summary>
        /// Get list of termly assessment category
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetListOfTermlyAssessmentCategory()
        /// </summary>
        [HttpGet("cbt/assessment-categories")]
        public async Task<IActionResult> GetListOfTermlyAssessmentCategory(CancellationToken ct)
        {
            try
            {
                var categories = await _teachersAndOthersService.GetListOfTermlyAssessmentCategory(ct);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assessment categories");
                return StatusCode(500, new { message = "An error occurred while retrieving assessment categories" });
            }
        }

        /// <summary>
        /// Get list of termly continuous assessment for dropdown
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetListOfTermlyContinuousAssessmentForDropdown()
        /// </summary>
        [HttpGet("cbt/assessment-dropdown")]
        public async Task<IActionResult> GetListOfTermlyContinuousAssessmentForDropdown(
            [FromQuery] AssessmentType assessmentType,
            CancellationToken ct)
        {
            try
            {
                var dropdown = await _teachersAndOthersService.GetListOfTermlyContinuousAssessmentForDropdown(assessmentType, ct);
                return Ok(dropdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assessment dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving assessment dropdown" });
            }
        }

        /// <summary>
        /// Get default list of termly continuous assessment for dropdown
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetDefaultListOfTermlyContinuousAssessmentForDropdown()
        /// </summary>
        [HttpGet("cbt/assessment-dropdown/default")]
        public async Task<IActionResult> GetDefaultListOfTermlyContinuousAssessmentForDropdown(CancellationToken ct)
        {
            try
            {
                var dropdown = await _teachersAndOthersService.GetDefaultListOfTermlyContinuousAssessmentForDropdown(ct);
                return Ok(dropdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default assessment dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving default assessment dropdown" });
            }
        }

        /// <summary>
        /// Get class dropdown
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetClassDropdown()
        /// </summary>
        [HttpGet("cbt/dropdowns/class")]
        public async Task<IActionResult> GetClassDropdown([FromQuery] string className, CancellationToken ct)
        {
            try
            {
                var dropdown = await _teachersAndOthersService.GetClassDropdown(className, ct);
                return Ok(dropdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving class dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving class dropdown" });
            }
        }

        /// <summary>
        /// Get default class dropdown
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetDefaultClassDropdown()
        /// </summary>
        [HttpGet("cbt/dropdowns/class/default")]
        public async Task<IActionResult> GetDefaultClassDropdown(CancellationToken ct)
        {
            try
            {
                var dropdown = await _teachersAndOthersService.GetDefaultClassDropdown(ct);
                return Ok(dropdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default class dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving default class dropdown" });
            }
        }

        /// <summary>
        /// Get subject dropdown
        /// Legacy: Uses ServiceLocator.Get&lt;ITeachersAndOthersService&gt;().GetSubjectDropdown()
        /// </summary>
        [HttpGet("cbt/dropdowns/subject")]
        public async Task<IActionResult> GetSubjectDropdown(CancellationToken ct)
        {
            try
            {
                var dropdown = await _teachersAndOthersService.GetSubjectDropdown(ct);
                return Ok(dropdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving subject dropdown" });
            }
        }

        #endregion
    }
}
