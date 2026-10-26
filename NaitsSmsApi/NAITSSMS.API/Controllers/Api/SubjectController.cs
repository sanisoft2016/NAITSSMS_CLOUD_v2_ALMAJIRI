using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.API.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubjectController : ControllerBase
    {
        private readonly ISubjectService _subjectService;
        private readonly ILogger<SubjectController> _logger;

        public SubjectController(
            ISubjectService subjectService,
            ILogger<SubjectController> logger)
        {
            _subjectService = subjectService ?? throw new ArgumentNullException(nameof(subjectService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("labels")]
        public async Task<IActionResult> GetSubjectLabels(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var labels = await _subjectService.GetSchoolLabel(userId, ct);
                return Ok(labels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject labels");
                return StatusCode(500, new { message = "An error occurred while retrieving subject labels." });
            }
        }

        [HttpPut("labels")]
        public async Task<IActionResult> UpdateSubjectLabels([FromBody] SchoolLevelCategoriesLabelDto model, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var status = await _subjectService.UpdateSchoolLabel(userId, model, ct);
                if (!status)
                {
                    return BadRequest(new { message = "Failed! you need to first register your school before this." });
                }

                return Ok(new { message = "The changes has been succcessfully saved." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating subject labels");
                return StatusCode(500, new { message = "An error occurred while updating subject labels." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects(
            [FromQuery] string? subjectLevelStatus,
            [FromQuery] string subjectStatus,
            CancellationToken ct)
        {
            try
            {
                if (!TryParseSubjectStatus(subjectStatus, out var parsedSubjectStatus))
                {
                    return BadRequest(new { message = $"Invalid subjectStatus '{subjectStatus}'." });
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var subjects = await _subjectService.GetSubjects(userId, parsedSubjectStatus, ct);
                var resolvedSubjectLevelStatus = ResolveSubjectLevelStatus(parsedSubjectStatus, subjectLevelStatus);

                return Ok(new
                {
                    subjectLevelStatus = resolvedSubjectLevelStatus,
                    subjectStatus = parsedSubjectStatus,
                    totalNoOfSubjects = subjects.Count,
                    title = BuildSubjectListingTitle(parsedSubjectStatus, resolvedSubjectLevelStatus),
                    shouldCreate = subjects.Count == 0,
                    subjects,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subjects for status {SubjectStatus}", subjectStatus);
                return StatusCode(500, new { message = "An error occurred while retrieving subjects." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSubject([FromBody] SubjectDto model, [FromQuery] SubjectStatus subjectStatus, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return ValidationProblem(ModelState);
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var isUnique = await _subjectService.ValidateSubjectNameForCreate(userId, model.SubjectName, subjectStatus, ct);
                if (!isUnique)
                {
                    return Conflict(new { message = $"This Subject \"{model.SubjectName}\" already exist." });
                }

                model.TimeStatus = TimeStatus.Current;
                var status = await _subjectService.CreatClassSubject(userId, model, subjectStatus, ct);
                if (!status)
                {
                    return BadRequest(new { message = "Failed! Unable to create subject." });
                }

                return Ok(new { message = "Subject created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subject for status {SubjectStatus}", subjectStatus);
                return StatusCode(500, new { message = "An error occurred while creating the subject." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSubject(string id, [FromBody] SubjectDto model, [FromQuery] SubjectStatus subjectStatus, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return ValidationProblem(ModelState);
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                model.Id = id;
                var isUnique = await _subjectService.ValidateSubjectNameForUpdate(userId, model.SubjectName, id, subjectStatus, ct);
                var isTimeStatusChangeRequest = await _subjectService.IsRequestToUpdateSubjectTimeStatus(userId, id, model.TimeStatus, ct);

                if (!isUnique && !isTimeStatusChangeRequest)
                {
                    return Conflict(new { message = "Failed! Subject Name exist against another record." });
                }

                var status = await _subjectService.UpdateSubject(userId, model, subjectStatus, ct);
                if (!status)
                {
                    return NotFound(new { message = "Subject record was not found." });
                }

                return Ok(new { message = "The changes has been succcessfully saved." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating subject {SubjectId}", id);
                return StatusCode(500, new { message = "An error occurred while updating the subject." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubject(string id, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var status = await _subjectService.DeleteSubject(userId, id, ct);
                if (!status)
                {
                    return Conflict(new { message = "Failed! Unable to complete the process." });
                }

                return Ok(new { message = "Record successfully Deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting subject {SubjectId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the subject." });
            }
        }

        [HttpGet("all/dropdown")]
        public async Task<IActionResult> GetAllSubjectDropdown(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var subjects = await _subjectService.GetSubjectForDropDown(userId, ct);
                subjects.Add(new SelectListItem
                {
                    Text = "AllSubjects",
                    Value = "AllSubjects"
                });

                return Ok(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all-subject dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving subject options." });
            }
        }

        [HttpGet("all/levels")]
        public async Task<IActionResult> GetLevelsThatOfferThisSubject([FromQuery] string subjectName, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var levels = await _subjectService.GetLevelsThatOfferThisSubject(userId, subjectName, ct);
                return Ok(levels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject levels for {SubjectName}", subjectName);
                return StatusCode(500, new { message = "An error occurred while retrieving subject levels." });
            }
        }

        [HttpGet("all/levels/search")]
        public async Task<IActionResult> SearchLevelsThatOfferThisSubject([FromQuery] string searchParameter, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var levels = await _subjectService.SearchLevelsThatOfferThisSubject(userId, searchParameter, ct);
                return Ok(levels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching subject levels with parameter {SearchParameter}", searchParameter);
                return StatusCode(500, new { message = "An error occurred while searching subject levels." });
            }
        }

        [HttpGet("student-assignment/context")]
        public async Task<IActionResult> GetSubjectStudentAssignmentContext(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var electiveSubjectsDropdown = await _subjectService.ElectiveSubjectDropDown(userId, ct);
                var classLabelDropdown = await _subjectService.ClassDropDown(userId, ct);

                return Ok(new
                {
                    electiveSubjectsDropdown,
                    classLabelDropdown,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject-student assignment context");
                return StatusCode(500, new { message = "An error occurred while retrieving subject assignment context." });
            }
        }

        [HttpGet("student-assignment/arms")]
        [HttpGet("student-assignment/arms/{selectedClassId}")]
        public async Task<IActionResult> GetSubjectStudentAssignmentArms(
            [FromQuery] string? selectedClassId,
            [FromRoute] string? selectedClassIdFromRoute,
            [FromQuery] string? subjectIdd,
            CancellationToken ct)
        {
            try
            {
                var resolvedClassId = !string.IsNullOrWhiteSpace(selectedClassIdFromRoute)
                    ? selectedClassIdFromRoute
                    : selectedClassId;

                if (string.IsNullOrWhiteSpace(resolvedClassId))
                {
                    return BadRequest(new { message = "selectedClassId is required." });
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var arms = await _subjectService.ArmDropDown(userId, resolvedClassId.Trim(), ct);
                return Ok(new
                {
                    arms,
                    subjectId = subjectIdd,
                    message = arms.Count == 0 ? "The selected Class does not contain Arm" : string.Empty,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving arms for class {ClassId}", selectedClassIdFromRoute ?? selectedClassId);
                return StatusCode(500, new { message = "An error occurred while retrieving arm options." });
            }
        }

        [HttpGet("student-assignment/students")]
        public async Task<IActionResult> GetSubjectStudentAssignmentStudents([FromQuery] string selectedArmId, [FromQuery] string subjectIdd, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _subjectService.GetStudentForAnArm2Nd(userId, selectedArmId, subjectIdd, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject-student assignment students for arm {ArmId}", selectedArmId);
                return StatusCode(500, new { message = "An error occurred while retrieving students for the selected arm." });
            }
        }

        [HttpGet("student-assignment/students/by-category")]
        public async Task<IActionResult> GetSubjectStudentAssignmentStudentsByCategory(
            [FromQuery] byte catStatus,
            [FromQuery] string armId,
            [FromQuery] string subjectId,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _subjectService.GetCategoryStudentsInTheSelectedArm(userId, catStatus, armId, subjectId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category-filtered subject assignment students for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving students for the selected category." });
            }
        }

        [HttpPost("student-assignment")]
        public async Task<IActionResult> AssignSubjectToStudents([FromBody] List<StudentIdsDto> studentIds, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _subjectService.AssignSubjectToArmStudentsOrStudentsInCategory(userId, studentIds, ct);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning subject to selected students");
                return StatusCode(500, new { message = "An error occurred while assigning the subject to students." });
            }
        }

        [HttpPost("student-assignment/unassign")]
        public async Task<IActionResult> UnAssignSubjectFromStudents([FromBody] List<StudentIdsDto> studentIds, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _subjectService.UnAssignSubjectToArmStudentsOrStudentsInCategory(userId, studentIds, ct);
                if (!result)
                {
                    return BadRequest(new { message = "Failed! Un-able to complete the process." });
                }

                return Ok(new { message = "succeed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning subject from selected students");
                return StatusCode(500, new { message = "An error occurred while un-assigning the subject from students." });
            }
        }

        private string? GetCurrentUserId()
        {
            return User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.PrimarySid)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
        }

        private static bool TryParseSubjectStatus(string? value, out SubjectStatus status)
        {
            status = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (Enum.TryParse(trimmed, ignoreCase: true, out status) && Enum.IsDefined(typeof(SubjectStatus), status))
            {
                return true;
            }

            if (byte.TryParse(trimmed, out var numeric) && Enum.IsDefined(typeof(SubjectStatus), numeric))
            {
                status = (SubjectStatus)numeric;
                return true;
            }

            return false;
        }

        private static string ResolveSubjectLevelStatus(SubjectStatus subjectStatus, string? subjectLevelStatus)
        {
            if (!string.IsNullOrWhiteSpace(subjectLevelStatus))
            {
                return subjectLevelStatus;
            }

            return subjectStatus switch
            {
                SubjectStatus.PlayClass => "Play Class Subjects",
                SubjectStatus.PreNursery => "Pre Nursery Subjects",
                SubjectStatus.Nursery => "Nursery Subjects",
                SubjectStatus.Primary => "Primary Subjects",
                SubjectStatus.Jss => "Junior Secondary Subjects",
                SubjectStatus.AllSeniors => "Senior General Subjects",
                SubjectStatus.Science => "Science Subjects",
                SubjectStatus.Art => "Art Subjects",
                SubjectStatus.Commercial => "Commercial Subjects",
                SubjectStatus.Elective => "Elective Subjects",
                SubjectStatus.General => "General Subjects",
                _ => subjectStatus.ToString(),
            };
        }

        private static string BuildSubjectListingTitle(SubjectStatus subjectStatus, string subjectLevelStatus)
        {
            return subjectStatus == SubjectStatus.General
                ? "List of Registered \"General\" Subjects for all Class(es)"
                : $"List of Registered Subjects for \"{subjectLevelStatus}\" Class(es)";
        }

    }
}
