using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.API.Controllers.Api
{
    /// <summary>
    /// Student / Parent self-service portal.
    /// Mirrors legacy Areas/StudentsAndParents/StudentsAndParentsController algorithms via ITeachersAndOthersService.
    /// </summary>
    [Route("api/StudentPortal")]
    [ApiController]
    [Authorize]
    public class StudentPortalController : ControllerBase
    {
        private readonly ITeachersAndOthersService _teachersAndOthersService;
        private readonly ISessionAndTermService _sessionAndTermService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<StudentPortalController> _logger;

        public StudentPortalController(
            ITeachersAndOthersService teachersAndOthersService,
            ISessionAndTermService sessionAndTermService,
            IUnitOfWork unitOfWork,
            ILogger<StudentPortalController> logger)
        {
            _teachersAndOthersService = teachersAndOthersService
                ?? throw new ArgumentNullException(nameof(teachersAndOthersService));
            _sessionAndTermService = sessionAndTermService
                ?? throw new ArgumentNullException(nameof(sessionAndTermService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Legacy: Index → VerifyAndGetSchoolInfo
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(CancellationToken ct)
        {
            try
            {
                var school = await _teachersAndOthersService.VerifyAndGetSchoolInfo(ct)
                    ?? await _teachersAndOthersService.GetSchoolModelForStudentOrParent(ct);

                if (school == null)
                {
                    return NotFound(new { message = "School profile was not found for the current student/parent." });
                }

                return Ok(school);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student/parent dashboard");
                return StatusCode(500, new { message = "An error occurred while loading the dashboard." });
            }
        }

        /// <summary>
        /// Legacy: StudentDetails → GetStudentDetailsByStudentId()
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetStudentProfile(CancellationToken ct)
        {
            try
            {
                var profile = await _teachersAndOthersService.GetStudentDetailsByStudentId(ct);
                return Ok(profile ?? new StudentDetailsDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student profile");
                return StatusCode(500, new { message = "An error occurred while loading the student profile." });
            }
        }

        /// <summary>
        /// Students sharing the logged-in student's Contact MobileNumber (same school).
        /// </summary>
        [HttpGet("siblings")]
        public async Task<IActionResult> GetSiblingStudents(CancellationToken ct)
        {
            try
            {
                var siblings = await _teachersAndOthersService.GetSiblingStudentsBySharedMobileNumber(ct);
                return Ok(siblings ?? new List<StudentPortalSiblingDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sibling students for portal switcher");
                return StatusCode(500, new { message = "An error occurred while loading related students." });
            }
        }

        /// <summary>
        /// Legacy: StudentSubjectListIndex → GetSubjectCategoryForDropDown + All-subjects / My-subject-list
        /// </summary>
        [HttpGet("subjects/categories")]
        public async Task<IActionResult> GetSubjectCategories(CancellationToken ct)
        {
            try
            {
                var categories = await _teachersAndOthersService.GetSubjectCategoryForDropDown(ct) ?? new List<SelectListItem>();
                var options = categories
                    .Select(item => new SelectListItem
                    {
                        Text = item.Text,
                        Value = item.Value,
                    })
                    .ToList();

                // Legacy appends these after category options.
                options.Add(new SelectListItem { Text = "All-subjects", Value = "AllSubjects" });
                options.Add(new SelectListItem { Text = "My-subject-list", Value = "My-subject-list" });

                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading subject categories for student portal");
                return StatusCode(500, new { message = "An error occurred while loading subject categories." });
            }
        }

        /// <summary>
        /// Legacy: RenderListOfSujectsUnderTheSelectedCategory → GetLevelsThatOfferThisSubject(subjectStatus)
        /// </summary>
        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjectsForSelectedCategory([FromQuery] string subjectStatus, CancellationToken ct)
        {
            try
            {
                var result = await _teachersAndOthersService.GetLevelsThatOfferThisSubject(subjectStatus, ct);
                return Ok(result ?? new SubjectsAndClassName());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading subjects for status {SubjectStatus}", subjectStatus);
                return StatusCode(500, new { message = "An error occurred while loading subjects." });
            }
        }

        /// <summary>
        /// Legacy: SearchLevelsOfferingThisSuject → registrationService.SearchLevelsThatOfferThisSubject
        /// Resolved against the current student/parent school (not registry-officer scope).
        /// </summary>
        [HttpGet("subjects/search")]
        public async Task<IActionResult> SearchSubjects([FromQuery] string searchParameter, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchParameter))
                {
                    return BadRequest(new { message = "Search Parameter must be provided to continue." });
                }

                var profile = await _teachersAndOthersService.GetStudentDetailsByStudentId(ct);
                if (profile == null || string.IsNullOrWhiteSpace(profile.SchoolInfoId))
                {
                    return Ok(new SubjectsAndClassName
                    {
                        SubjectDtos = new List<SubjectDto>(),
                    });
                }

                var needle = searchParameter.Trim().ToLowerInvariant();
                var subjectRepo = _unitOfWork.Repository<NAITSSMS.Domain.Entities.RxSubject>();
                var subjects = await subjectRepo.WhereAsync(
                    s => s.SubjectCategory != null
                         && s.SubjectCategory.SchoolId == profile.SchoolInfoId
                         && s.SubjectName.Trim().ToLower().Contains(needle),
                    ct,
                    s => s.SubjectCategory!);

                var mapped = subjects
                    .OrderBy(s => s.SubjectName)
                    .Select(s => new SubjectDto
                    {
                        Id = s.Id,
                        SubjectName = s.SubjectName,
                        TimeStatus = s.TimeStatus,
                        SubjectCategoryId = s.SubjectCategoryId,
                        SubjectCategory = s.SubjectCategory == null
                            ? null
                            : new SubjectCategoriesDto
                            {
                                SubjectStatus = s.SubjectCategory.SubjectStatus,
                                SubjectStatusLabel = s.SubjectCategory.SubjectStatusLabel ?? string.Empty,
                                SchoolId = s.SubjectCategory.SchoolId,
                            },
                    })
                    .ToList();

                var school = await _unitOfWork.Repository<NAITSSMS.Domain.Entities.SchoolTyInfo, string>()
                    .FindAsync(profile.SchoolInfoId, ct);

                return Ok(new SubjectsAndClassName
                {
                    SubjectDtos = mapped,
                    StudentStatus = profile.ClassCategory,
                    ClassName = profile.CurrentClass,
                    AccessStatu = school?.ElectiveAccessStatus ?? AccessStatus.Active,
                    ElectiveStatus = new List<ElectiveStatusDto>(),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching subjects for student portal");
                return StatusCode(500, new { message = "An error occurred while searching subjects." });
            }
        }

        /// <summary>
        /// Legacy: SubmitSubjectSelectios → CreateSubjectStudentAssignment
        /// </summary>
        [HttpPost("subjects/assign")]
        public async Task<IActionResult> AssignSubjects([FromBody] List<SubjectIdsBto> subjectIds, CancellationToken ct)
        {
            try
            {
                var result = await _teachersAndOthersService.CreateSubjectStudentAssignment(subjectIds ?? new List<SubjectIdsBto>(), ct);
                if (!string.Equals(result, "succeed", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(result ?? "Failed");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning elective subjects for student portal");
                return StatusCode(500, new { message = "An error occurred while assigning subjects." });
            }
        }

        /// <summary>
        /// Legacy: RemoveSubjectSelections → RemoveSubjectStudentAssignment
        /// </summary>
        [HttpPost("subjects/unassign")]
        public async Task<IActionResult> UnassignSubjects([FromBody] List<SubjectIdsBto> subjectIds, CancellationToken ct)
        {
            try
            {
                var result = await _teachersAndOthersService.RemoveSubjectStudentAssignment(subjectIds ?? new List<SubjectIdsBto>(), ct);
                return Ok(result ? "succeed" : "Failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing elective subjects for student portal");
                return StatusCode(500, new { message = "An error occurred while removing subjects." });
            }
        }

        /// <summary>
        /// Sessions that have assessment/result rows for the resolved portal student
        /// (including the sibling selected via <c>X-Selected-Student-Id</c>).
        /// </summary>
        [HttpGet("results/sessions")]
        public async Task<IActionResult> GetResultSessions(CancellationToken ct)
        {
            try
            {
                if (await IsPortalStudentAccountDisabledAsync(ct))
                {
                    return Ok(new { message = DisabledAccountMessage });
                }

                var sessions = await _teachersAndOthersService.GetSessionsDropdownWithResultsForPortalStudent(ct)
                    ?? new List<SelectListItem>();
                return Ok(PadDropdown(sessions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sessions for student results");
                return StatusCode(500, new { message = "An error occurred while loading sessions." });
            }
        }

        /// <summary>
        /// Legacy: TermDropdownForSelectedSession → GetTermsEnumTypeDropdownForSelectedSession
        /// </summary>
        [HttpGet("results/sessions/{sessionId}/terms")]
        public async Task<IActionResult> GetResultTermsForSession(string sessionId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(new { message = "sessionId is required." });
                }

                var terms = await _sessionAndTermService.GetTermsDropdownForSession(sessionId, ct)
                    ?? new List<SelectListItem>();
                return Ok(PadDropdown(terms));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading terms for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while loading terms." });
            }
        }

        /// <summary>
        /// Legacy: SelectedStudentTermlyReportSheet → GetTermlyAssessmentForSelectedStudent(sessionId, termStatus)
        /// </summary>
        [HttpGet("results/termly")]
        public async Task<IActionResult> GetTermlyResult(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(new { message = "sessionId is required." });
                }

                if (await IsPortalStudentAccountDisabledAsync(ct))
                {
                    return Ok(new { message = DisabledAccountMessage });
                }

                var result = await _teachersAndOthersService.GetTermlyAssessmentForSelectedStudent(sessionId, termStatus, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading termly result for student portal. SessionId={SessionId}, TermStatus={TermStatus}", sessionId, termStatus);
                return StatusCode(500, new
                {
                    message = "An error occurred while loading the termly result.",
                    detail = ex.GetBaseException().Message,
                });
            }
        }

        /// <summary>
        /// Legacy: SelectedStudentYearlyReportSheet → GetYearlyAssessmentForSelectedStudent(selectedSessionId)
        /// </summary>
        [HttpGet("results/yearly")]
        public async Task<IActionResult> GetYearlyResult([FromQuery] string selectedSessionId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(selectedSessionId))
                {
                    return BadRequest(new { message = "selectedSessionId is required." });
                }

                if (await IsPortalStudentAccountDisabledAsync(ct))
                {
                    return Ok(new { message = DisabledAccountMessage });
                }

                var result = await _teachersAndOthersService.GetYearlyAssessmentForSelectedStudent(selectedSessionId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading yearly result for student portal");
                return StatusCode(500, new { message = "An error occurred while loading the yearly result." });
            }
        }

        private const string DisabledAccountMessage = "Sorry! Your account has been disabled.";

        private async Task<bool> IsPortalStudentAccountDisabledAsync(CancellationToken ct)
        {
            var profile = await _teachersAndOthersService.GetStudentDetailsByStudentId(ct);
            return profile?.User?.AccessStatus == AccessStatus.Disable;
        }

        /// <summary>
        /// Legacy pad: when only one option exists, prepend "Select" (-1) and keep the real option last.
        /// </summary>
        private static List<SelectListItem> PadDropdown(IList<SelectListItem> items)
        {
            var list = items?.ToList() ?? new List<SelectListItem>();
            if (list.Count != 1)
            {
                return list;
            }

            var holder = list[0];
            return new List<SelectListItem>
            {
                new() { Text = "Select", Value = "-1" },
                new() { Text = holder.Text, Value = holder.Value },
            };
        }
    }
}
