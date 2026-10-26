using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAITSSMS.API.Helpers;
using NAITSSMS.API.Models;
using NAITSSMS.API.Services;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Application.Service;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;

namespace NAITSSMS.API.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RegistryController : ControllerBase
    {
        private readonly IRegistrationService _registrationService;
        private readonly IRegistrySmsService _registrySmsService;
        private readonly ISecurityService _securityService;
        private readonly ISecurityExtensionService _securityExtensionService;
        private readonly IEmailServiceExtension _emailServiceExtension;
        private readonly IDailyAttendanceService _dailyAttendanceService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<RegistryController> _logger;

        /// <summary>
        /// Constructor with dependency injection
        /// Replaces legacy: _registrationService = ServiceLocator.Get<IRegistrationServise>();
        /// </summary>
        public RegistryController(
            IRegistrationService registrationService,
            IRegistrySmsService registrySmsService,
            ISecurityService securityService,
            IEmailServiceExtension emailServiceExtension,
            IUnitOfWork unitOfWork,
            IWebHostEnvironment environment,
            ISecurityExtensionService securityExtensionService,
            IDailyAttendanceService dailyAttendanceService,
            ILogger<RegistryController> logger)
        {
            _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
            _registrySmsService = registrySmsService ?? throw new ArgumentNullException(nameof(registrySmsService));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            _emailServiceExtension = emailServiceExtension ?? throw new ArgumentNullException(nameof(emailServiceExtension));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _securityExtensionService = securityExtensionService;
            _dailyAttendanceService = dailyAttendanceService ?? throw new ArgumentNullException(nameof(dailyAttendanceService));

             _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        private static object MapLegacyRedirect(string? action, string? controller, string? area)
        {
            // Legacy BashLogOff / Account → Angular auth login
            if (string.Equals(action, "BashLogOff", StringComparison.OrdinalIgnoreCase)
                || string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
            {
                return new { path = "/auth/login" };
            }

            return new
            {
                path = "/auth/login",
                action,
                controller,
                area
            };
        }

        /// <summary>
        /// Legacy RegistryController.Index algorithm for the Angular /admin landing page.
        /// </summary>
        [HttpGet("index")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            // 1) teachersAndOthersService.B() — VerifyAccess / checkIfUserAccessIsActive
            string userId = GetCurrentUserId();
            var aStatus = await _securityExtensionService.B(userId, ct);
            if (aStatus != null)
            {
                return Ok(new
                {
                    canContinue = false,
                    message = aStatus.Value1,
                    alertType = "danger",
                    redirect = MapLegacyRedirect(aStatus.Value2, aStatus.Value3, aStatus.Value4)
                });
            }

            // 2) securityService.B() — VerifyAccess
            var bStatus = await _securityService.B(ct);
            if (bStatus.AccessVerifyReturnValues != null)
            {
                var redirectValues = bStatus.AccessVerifyReturnValues;
                return Ok(new
                {
                    canContinue = false,
                    message = redirectValues.Value1,
                    alertType = "danger",
                    redirect = MapLegacyRedirect(redirectValues.Value2, redirectValues.Value3, redirectValues.Value4)
                });
            }

            string? message = null;
            var alertType = "info";

            // 3) Legacy: if ReturnMessage != "1" OR StatusCode == "1" → ShowMessage(..., Danger) and skip subscription confirm.
            if ((bStatus.LoginReturnStatus?.ReturnMessage != "1") || (bStatus.LoginReturnStatus?.StatusCode == "1"))
            {
                await _unitOfWork.SaveChangesAsync(ct);
                message = bStatus.LoginReturnStatus?.ReturnMessage;
                alertType = "danger";
            }
            else
            {
                // 4) Subscription confirmation for registrar (legacy Index else-branch)
                var registrationConfirmationStatus =
                    await _securityService.GetSubscriptionConfirmationStatusForRegistrar(ct);
                if (!registrationConfirmationStatus)
                {
                    var returnStatus = string.Empty;
                    if (InternetConnectivityHelper.CheckForInternetConnection())
                    {
                        var credentialEncoded = Uri.EscapeDataString(
                            await _securityExtensionService.GetAndEncodeCredentialsAndExpiryDate(userId, ct));
                        var rightHostName = await TheRightNaitsHostName(ct);
                        if (!string.IsNullOrEmpty(rightHostName))
                        {
                            var url = rightHostName + "/home/VerifySubscription?credentialEncoded=" + credentialEncoded;
                            try
                            {
                                var balance = await GetRemoteString(url, ct);
                                if (!string.IsNullOrEmpty(balance))
                                {
                                    returnStatus = balance.Replace("\"", string.Empty);
                                    if (returnStatus == "true")
                                    {
                                        await _securityService.ConfirmSubscription(ct);
                                    }
                                    else
                                    {
                                        var expiredOn = await CurrentLicenseExpiredOnAsync(userId, ct);
                                        message = expiredOn == null
                                            ? "Failed! Unable to verify your credentials. Contact administrator on 08035550938."
                                            : "Failed! Your subscription expired on " + expiredOn.Value.ToString("dd MMMM yyyy") + ". Contact administrator on 08035550938.";
                                        alertType = "warning";
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Index VerifySubscription remote call failed.");
                            }
                        }
                        else
                        {
                            message = "NAITS-ASMS online platform is temporarily unavailanle!!!";
                            alertType = "warning";
                        }
                    }
                    else
                    {
                        message = "You need to provide internet in order to confirm your registration!!!";
                        alertType = "warning";
                    }

                    if (returnStatus == "true")
                    {
                        message = "Good! Your Subscription has successfully been confirmed.";
                        alertType = "success";
                    }
                }
            }

            // Legacy always returns View() — Angular keeps tiles visible (canContinue=true).
            return Ok(new
            {
                canContinue = true,
                message,
                alertType,
                redirect = (object?)null
            });
        }

        private async Task<DateTime?> CurrentLicenseExpiredOnAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var school = await _unitOfWork.Repository<SchoolTyInfo, string>().FindAsync(
                entry => entry.SchoolRegistrationOfficer != null && entry.SchoolRegistrationOfficer.UserId == userId,
                ct,
                entry => entry.SchoolRegistrationOfficer!);
            if (school == null
                || string.IsNullOrWhiteSpace(school.SmsApiToken)
                || string.IsNullOrWhiteSpace(school.SmsApiKey))
            {
                return null;
            }

            var licenses = await _unitOfWork.Repository<LicensTying>().WhereAsync(
                license => license.SchoolSmsId == school.SchoolRegistrationOfficerId
                    && license.TimeStatus == TimeStatus.Current,
                ct);
            var current = licenses.OrderByDescending(license => license.ExpiryDate).FirstOrDefault();
            if (current == null)
            {
                return null;
            }

            var expiry = current.ExpiryDate.Kind == DateTimeKind.Utc
                ? current.ExpiryDate.ToLocalTime()
                : current.ExpiryDate;
            return expiry < DateTime.Now ? expiry : null;
        }

        [HttpGet("teachers/registered")]
        public async Task<IActionResult> GetRegisteredTeachers(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var teachers = await _registrationService.GetAllTeachers(userId, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registered employees");
                return StatusCode(500, new { message = "An error occurred while retrieving registered employees." });
            }
        }

        [HttpGet("teachers/registered/search")]
        public async Task<IActionResult> SearchTeacher([FromQuery] string userName, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Ok(Array.Empty<EmployeeDto>());
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var teachers = await _registrationService.GetTeacherByUserName(userId, userName, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching registered employees by {UserName}", userName);
                return StatusCode(500, new { message = "An error occurred while searching registered employees." });
            }
        }

        [HttpGet("teachers/subject-teachers")]
        public async Task<IActionResult> GetSubjectTeachers(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var teachers = await _registrationService.GetSubjectTeachers(userId, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject teachers");
                return StatusCode(500, new { message = "An error occurred while retrieving subject teachers." });
            }
        }

        [HttpGet("teachers/subject-teachers/search")]
        public async Task<IActionResult> SearchSubjectTeacher([FromQuery] string userName, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Ok(Array.Empty<TeacherSubjectArmAssignmentDto>());
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var teachers = await _registrationService.GetSubjectTeacherByUserName(userId, userName, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching subject teachers by {UserName}", userName);
                return StatusCode(500, new { message = "An error occurred while searching subject teachers." });
            }
        }

        [HttpDelete("teachers/subject-teachers/{id}")]
        public async Task<IActionResult> UnAssignSubjectTeacher(string id, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var teachers = await _registrationService.UnAssignSubjectTeacher(userId, id, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning subject teacher {AssignmentId}", id);
                return StatusCode(500, new { message = "An error occurred while un-assigning the subject teacher." });
            }
        }

        [HttpGet("teachers/form-teachers")]
        public async Task<IActionResult> GetFormTeachers([FromQuery] string? teacherId, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                if (!string.IsNullOrWhiteSpace(teacherId))
                {
                    var assignedArms = await _registrationService.GetTeacherAssignedArmsByTeacherId(userId, teacherId, ct);
                    return Ok(assignedArms);
                }

                var teachers = await _registrationService.GetClassTeachers(userId, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form teachers");
                return StatusCode(500, new { message = "An error occurred while retrieving form teachers." });
            }
        }

        [HttpGet("teachers/form-teachers/search")]
        public async Task<IActionResult> SearchFormMaster([FromQuery] string userName, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Ok(Array.Empty<TeacherClassArmAssignmentDto>());
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var teachers = await _registrationService.GetFormMasterByUserName(userId, userName, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching form teachers by {UserName}", userName);
                return StatusCode(500, new { message = "An error occurred while searching form teachers." });
            }
        }

        [HttpDelete("teachers/form-teachers/{id}")]
        public async Task<IActionResult> UnAssignClassTeacher(string id, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var teachers = await _registrationService.UnAssignClassTeacher(userId, id, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning form teacher {AssignmentId}", id);
                return StatusCode(500, new { message = "An error occurred while un-assigning the form teacher." });
            }
        }

        [HttpPost("teachers/{id}/enable")]
        public async Task<IActionResult> EnableUser(string id, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var status = await _registrationService.EnableUserById(id, ct);
                if (!status)
                {
                    return NotFound(new { message = "Failed! We were unable to complete the request." });
                }

                var teachers = await _registrationService.GetAllTeachers(userId, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling employee {EmployeeId}", id);
                return StatusCode(500, new { message = "An error occurred while enabling the employee account." });
            }
        }

        [HttpPost("teachers/{id}/disable")]
        public async Task<IActionResult> DisableUser(string id, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var status = await _registrationService.DisableUserById(id, ct);
                if (!status)
                {
                    return NotFound(new { message = "Failed! We were unable to complete the request." });
                }

                var teachers = await _registrationService.GetAllTeachers(userId, ct);
                return Ok(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling employee {EmployeeId}", id);
                return StatusCode(500, new { message = "An error occurred while disabling the employee account." });
            }
        }

        [HttpGet("teachers/subject-assignment/options")]
        public async Task<IActionResult> GetSubjectAssignmentOptions(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var options = await _registrationService.GetSubjectAssignmentOptions(userId, ct);
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject assignment options");
                return StatusCode(500, new { message = "An error occurred while retrieving subject assignment options." });
            }
        }

        [HttpGet("teachers/subject-assignment/subjects")]
        public async Task<IActionResult> GetSubjectAssignmentSubjects([FromQuery] string classId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(classId))
                {
                    return Ok(Array.Empty<SelectListItem>());
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var subjects = await _registrationService.GetSubjectAssignmentSubjects(userId, classId, ct);
                return Ok(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subjects for class {ClassId}", classId);
                return StatusCode(500, new { message = "An error occurred while retrieving subject options." });
            }
        }

        [HttpGet("teachers/subject-assignment/arms")]
        public async Task<IActionResult> GetSubjectAssignmentArms([FromQuery] string subjectId, [FromQuery] string classId, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var arms = await _registrationService.GetSubjectAssignmentArms(userId, subjectId, classId, ct);
                return Ok(arms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assignment arms for subject {SubjectId} and class {ClassId}", subjectId, classId);
                return StatusCode(500, new { message = "An error occurred while retrieving available arms." });
            }
        }

        [HttpPost("teachers/subject-assignment")]
        public async Task<IActionResult> AssignSubjectTeacherToArm([FromBody] SubjectTeacherArmAssignmentRequest model, CancellationToken ct)
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

                var result = await _registrationService.AssignSubjectTeacherToArm(userId, model.TeacherId, model.SubjectId, model.ArmId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning subject teacher to arm {ArmId}", model?.ArmId);
                return StatusCode(500, new { message = "An error occurred while assigning the subject teacher." });
            }
        }

        [HttpGet("teachers/subject-assignment/unassigned")]
        public async Task<IActionResult> GetSubjectUnAssignedTeachers(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.GetSubjectUnAssignedTeachers(userId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subject unassigned teacher list");
                return StatusCode(500, new { message = "An error occurred while retrieving subject un-assigned teachers." });
            }
        }

        [HttpGet("teachers/subject-assignment/assigned")]
        public async Task<IActionResult> GetTeacherAssignedSubjects([FromQuery] string teacherId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(teacherId))
                {
                    return Ok(Array.Empty<TeacherSubjectArmAssignmentDto>());
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.GetTeacherAssignedSubjectsByTeacherId(userId, teacherId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assigned subjects for teacher {TeacherId}", teacherId);
                return StatusCode(500, new { message = "An error occurred while retrieving teacher subject assignments." });
            }
        }

        [HttpPost("teachers/subject-assignment/transfer")]
        public async Task<IActionResult> TransferSubjectAssignments(
            [FromBody] List<SubjectIdsBto> subjectIds,
            [FromQuery] string? type,
            CancellationToken ct)
        {
            try
            {
                if (subjectIds == null || subjectIds.Count < 2)
                {
                    return BadRequest(new { message = "Select at least one assignment and a proposed teacher." });
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var isClassArmTransfer = string.Equals(type, "class-arm", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(type, "arm", StringComparison.OrdinalIgnoreCase);
                var status = isClassArmTransfer
                    ? await _registrationService.TransferArmTeacherArmAssignment(userId, subjectIds, ct)
                    : await _registrationService.TransferSubjectTeacherSubjectAssignment(userId, subjectIds, ct);
                if (!status)
                {
                    return BadRequest(new { message = "Failed! We were unable to complete the transfer." });
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring teacher assignments");
                return StatusCode(500, new { message = "An error occurred while transferring assignments." });
            }
        }

        [HttpGet("teachers/class-arm-assignment/options")]
        public async Task<IActionResult> GetClassArmAssignmentOptions(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var options = await _registrationService.GetClassArmAssignmentOptions(userId, ct);
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving class-arm assignment options");
                return StatusCode(500, new { message = "An error occurred while retrieving class-arm assignment options." });
            }
        }

        [HttpGet("teachers/class-arm-assignment/arms")]
        public async Task<IActionResult> GetClassArmAssignmentArms([FromQuery] string classId, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var arms = await _registrationService.GetClassArmAssignmentArms(userId, classId, ct);
                return Ok(arms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving class-arm assignment arms for class {ClassId}", classId);
                return StatusCode(500, new { message = "An error occurred while retrieving available class arms." });
            }
        }

        [HttpPost("teachers/class-arm-assignment")]
        public async Task<IActionResult> AssignClassArmTeacher([FromBody] FormMasterArmAssignmentRequest model, CancellationToken ct)
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

                var result = await _registrationService.AssignClassArmTeacher(userId, model.TeacherId, model.ArmId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning form master to arm {ArmId}", model?.ArmId);
                return StatusCode(500, new { message = "An error occurred while assigning the form master." });
            }
        }

        [HttpGet("teachers/class-arm-assignment/assigned")]
        public async Task<IActionResult> GetTeacherAssignedArms([FromQuery] string teacherId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(teacherId))
                {
                    return Ok(Array.Empty<TeacherClassArmAssignmentDto>());
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.GetTeacherAssignedArmsByTeacherId(userId, teacherId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assigned arms for teacher {TeacherId}", teacherId);
                return StatusCode(500, new { message = "An error occurred while retrieving teacher class-arm assignments." });
            }
        }

        [HttpPost("teachers/class-arm-assignment/transfer")]
        public async Task<IActionResult> TransferArmAssignments([FromBody] List<SubjectIdsBto> subjectIds, CancellationToken ct)
        {
            try
            {
                if (subjectIds == null || subjectIds.Count < 2)
                {
                    return BadRequest(new { message = "Select at least one assignment and a proposed teacher." });
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var status = await _registrationService.TransferArmTeacherArmAssignment(userId, subjectIds, ct);
                if (!status)
                {
                    return BadRequest(new { message = "Failed! We were unable to complete the transfer." });
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring class-arm teacher assignments");
                return StatusCode(500, new { message = "An error occurred while transferring class-arm assignments." });
            }
        }

        [HttpGet("teachers/cbt-users")]
        public async Task<IActionResult> GetRegisteredTeachersForCbt([FromQuery] string userName, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.GetRegisteredTeachersForCbt(userId, userName, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving CBT user registry list");
                return StatusCode(500, new { message = "An error occurred while retrieving CBT user data." });
            }
        }

        [HttpPost("teachers/cbt-users/{targetUserId}/assign")]
        public async Task<IActionResult> AddToCbtUser(string targetUserId, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.AddUserToCbtRole(userId, targetUserId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {TargetUserId} to CBT role", targetUserId);
                return StatusCode(500, new { message = "An error occurred while assigning the CBT role." });
            }
        }

        [HttpPost("teachers/cbt-users/{targetUserId}/remove")]
        public async Task<IActionResult> RemoveFromCbtUser(string targetUserId, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.RemoveUserFromCbtRole(userId, targetUserId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {TargetUserId} from CBT role", targetUserId);
                return StatusCode(500, new { message = "An error occurred while removing the CBT role." });
            }
        }

        [HttpPost("auto-promotion")]
        public async Task<IActionResult> AutoPromotion(CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Starting auto promotion (JSS3 → SS1)");

                await _registrationService.AutoPromotion(ct);
                await _unitOfWork.SaveChangesAsync(ct);

                _logger.LogInformation("Auto promotion completed successfully");
                return Ok(new { message = "Auto promotion completed successfully" });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Auto promotion was cancelled");
                return StatusCode(499, new { message = "Operation cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto promotion");
                return StatusCode(500, new { message = "An error occurred during auto promotion" });
            }
        }

        [HttpPost("automatic-promotion")]
        public async Task<IActionResult> StudentsAutomaticPromotion(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "User is not authenticated" });
                }

                _logger.LogInformation("Starting comprehensive school-wide promotion");

                await _registrationService.StudentsAutomaticPromotion(userId, ct);

                _logger.LogInformation("School-wide promotion completed successfully");
                return Ok(new
                {
                    message = "School-wide promotion completed successfully",
                    details = "All students promoted, new academic session created"
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("School-wide promotion was cancelled");
                return StatusCode(499, new { message = "Operation cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during school-wide promotion");
                return StatusCode(500, new { message = "An error occurred during school-wide promotion" });
            }
        }

        [HttpGet("promotion/validate")]
        public async Task<IActionResult> RunPromotionValidation(CancellationToken ct)
        {
            try
            {
                var status = await _registrationService.StudentsPromotionValidations(GetCurrentUserId(), ct);
                return Ok(new { status, message = status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating student promotion");
                return StatusCode(500, new { message = "An error occurred while validating promotion." });
            }
        }

        [HttpPost("promotion/confirm")]
        public async Task<IActionResult> ConfirmPromotionRun(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "User is not authenticated" });
                }

                // Legacy ConfirmPromotionRun -> ConfirmStudentsPromotion (pass-mark gated).
                var status = await _registrationService.ConfirmStudentsPromotion(userId, ct);
                if (status == "Succeeded")
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Ok(new { status = "Succeeded", message = "Succeeded" });
                }

                if (status == "Failed")
                {
                    return Ok(new
                    {
                        status = "Some form of serious validation error has occured",
                        message = "Some form of serious validation error has occured"
                    });
                }

                return Ok(new { status, message = status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming student promotion");
                return StatusCode(500, new { message = "Some form of serious validation error has occured" });
            }
        }

        [HttpPost("promotion/automatic/confirm")]
        public async Task<IActionResult> ConfirmAutomaticPromotionRun(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "User is not authenticated" });
                }

                // Legacy ConfirmAutomaticPromotionRun calls StudentsAutomaticPromotion directly
                // (no pass-mark / ConfirmStudentsPromotion path). UI may still validate first.
                await _registrationService.StudentsAutomaticPromotion(userId, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { status = "Succeeded", message = "Succeeded" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming automatic student promotion");
                return StatusCode(500, new { message = "An error occurred during automatic promotion." });
            }
        }

        [HttpGet("subscription/category")]
        public async Task<IActionResult> PerTermSubscriptionCategory(CancellationToken ct)
        {
            var rightHostName = await TheRightNaitsHostName(ct);
            if (string.IsNullOrEmpty(rightHostName))
            {
                return Ok(new { message = "Sorry! You need to provide internet in order to Accomplish this." });
            }

            try
            {
                var credentialEncoded = Uri.EscapeDataString(await _registrationService.GetAndEncodeCredentials(GetCurrentUserId(), ct));
                var response = await GetRemoteString(rightHostName + "/home/SubscriptionCategoryStatus?credentialEncoded=" + credentialEncoded, ct);

                if (string.IsNullOrEmpty(response))
                {
                    return Ok(new { message = "Failed! Un-able to complete the process. Contact 08035550938 for assistance." });
                }

                response = response.Replace("\"", string.Empty);
                if (response == "2")
                {
                    return Ok(new { message = "Failed! Un-able to Verify your Credential. Contact 08035550938 for assistance." });
                }

                if (response == "9")
                {
                    return Ok(new { message = "Your subscription plan has a wrong value." });
                }

                if (response == "0" || response == "1")
                {
                    // Legacy: securityService.UpdateSystemImplementationType(...) — no pre-load of school.
                    var updated = await _registrationService.ChangeSystemImplementationType(
                        $"{GetCurrentUserId()},{response}", ct);
                    if (!updated)
                    {
                        return Ok(new { message = "Failed! Un-able to update subscription plan." });
                    }

                    var message = response == "0"
                        ? "You are on per-term subscription plan."
                        : "You are on per-student-per-term subscription plan.";
                    return Ok(new { message });
                }

                return Ok(new { message = response });
            }
            catch (Exception ex)
            {
                return Ok(new { message = "Failed! " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        [HttpGet("subscription/renewal")]
        public async Task<IActionResult> SubscriptionRenewal(CancellationToken ct)
        {
            var rightHostName = await TheRightNaitsHostName(ct);
            if (string.IsNullOrEmpty(rightHostName))
            {
                return Ok(new { message = "Failed! Internet is required to enable your system." });
            }

            var retrialStatus = await NoOfReTrial(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            if (retrialStatus != "Operation was successfully completed.")
            {
                return Ok(new { message = retrialStatus });
            }

            try
            {
                var credentialEncoded = Uri.EscapeDataString(await _registrationService.GetAndEncodeCredentials(GetCurrentUserId(), ct));
                var response = await GetRemoteString(rightHostName + "/home/SubscriptionRewal?credentialEncoded=" + credentialEncoded, ct);
                if (response == null)
                {
                    return Ok(new { message = "Failed! Unable to complete the process." });
                }

                // An empty reply means no current, unexpired licence; handled below.
                var responseStatus = response.Replace("\"", string.Empty).Trim();
                if (responseStatus == "000")
                {
                    return Ok(new { message = "Failed! unable to verify your credentials. Contact NAITS-ASMS Admin." });
                }

                if (responseStatus == string.Empty)
                {
                    return Ok(new { message = "Failed! Your subscription has expired. Contact NAITS-ASMS Admin." });
                }

                if (responseStatus == "1")
                {
                    return Ok(new { message = "Failed! Your online account is disable. Contact NAITS-ASMS Admin.", shouldLogOff = true });
                }

                if (responseStatus.StartsWith("2", StringComparison.Ordinal))
                {
                    var parts = responseStatus.Split(':').ToList();
                    if (parts.Count >= 3)
                    {
                        await UpdateFeatureImplementationTypeAndNoOfStudent(parts[1], parts[2], ct);
                    }

                    // The remote key was already consumed, so the new expiry is on the local
                    // current licence. ProgramYInfo Status is still what /admin reads.
                    await _securityService.SyncProgramExpiryFromCurrentLicenseAsync(ct);

                    return Ok(new { message = "Sorry! The renewal had already been consumed. However, your system has been updated." });
                }

                var status = await ExtendSubscriptionValidity(responseStatus, ct);
                return status.status == 0
                    ? Ok(new { message = "Your software renewal was successful.", expiryDate = status.expiryDate })
                    : Ok(new { message = RenewalFailureMessage(status.status) });
            }
            catch (Exception ex)
            {
                return Ok(new { message = "Failed! " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }


        /// <summary>
        /// Get school summary for dashboard
        /// Legacy: Dashboard()
        /// </summary>
        [HttpGet("dashboard/summary")]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var summary = await _registrationService.GetSchoolSummaryForRegistrarDashboard(userId, ct);
                return Ok(summary);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Dashboard summary unavailable for current user");
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Dashboard summary unauthorized for current user");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard summary");
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving dashboard summary",
                    detail = ex.Message,
                });
            }
        }

        [HttpPost("create-school-head")]
        public async Task<IActionResult> CreateSchoolHead([FromBody] SchoolHeadUpsertRequest model, CancellationToken ct)
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

                var signatureValidation = ValidateSignatureImagePayload(model.SignatureImageString);
                if (!string.IsNullOrWhiteSpace(signatureValidation))
                {
                    return BadRequest(new { message = signatureValidation });
                }

                var dto = new SchoolHeadDto
                {
                    HeadsName = model.HeadsName,
                    PhoneNumber = model.PhoneNumber,
                    SignatureImageString = await NormalizeSignatureImageAsync(model.SignatureImageString, ct),
                    Status = model.Status,
                    SchoolHeadCategoryId = model.SchoolHeadCategoryId,
                    HeadStatus = model.HeadStatus,
                    Gender = model.Gender,
                };

                var result = await _registrationService.CreatSchoolHead(userId, dto, ct);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to create school head." });
                }

                return Ok(new { message = "The Principal information has successfully been posted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating school head");
                return StatusCode(500, new { message = "An error occurred while creating school head." });
            }
        }

        [HttpGet("get-school-heads")]
        public async Task<IActionResult> GetSchoolHeads(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var heads = await _registrationService.GetPrincals(userId, ct);
                return Ok(heads);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving school heads");
                return StatusCode(500, new { message = "An error occurred while retrieving school heads." });
            }
        }

        [HttpPut("update-school-head/{id}")]
        public async Task<IActionResult> UpdateSchoolHead(string id, [FromBody] SchoolHeadUpsertRequest model, CancellationToken ct)
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

                var signatureValidation = ValidateSignatureImagePayload(model.SignatureImageString);
                if (!string.IsNullOrWhiteSpace(signatureValidation))
                {
                    return BadRequest(new { message = signatureValidation });
                }

                var dto = new SchoolHeadDto
                {
                    Id = id,
                    HeadsName = model.HeadsName,
                    PhoneNumber = model.PhoneNumber,
                    SignatureImageString = await NormalizeSignatureImageAsync(model.SignatureImageString, ct),
                    Status = model.Status,
                    SchoolHeadCategoryId = model.SchoolHeadCategoryId,
                    HeadStatus = model.HeadStatus,
                    Gender = model.Gender,
                };

                var result = await _registrationService.UpdateSchoolHeadInfo(userId, dto, ct);
                if (!result)
                {
                    return NotFound(new { message = "School head record was not found." });
                }

                return Ok(new { message = "School head details updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating school head {SchoolHeadId}", id);
                return StatusCode(500, new { message = "An error occurred while updating school head." });
            }
        }

        [HttpPost("create-school-head-assignment")]
        public async Task<IActionResult> CreateSchoolHeadAssignment(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var status = await _registrationService.CreateSchoolHeadAssignment(userId, ct);
                var message = status == 0
                    ? "School head(s) has successfully been set."
                    : "Sorry you need to run Student assessment preparation before this.";

                return Ok(new { status, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating school head assignment");
                return StatusCode(500, new { message = "An error occurred while setting the school head." });
            }
        }

        [HttpGet("get-school-head-names")]
        public async Task<IActionResult> GetSchoolHeadNames(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var names = await _registrationService.GetSchoolHeadNames(userId, ct);
                return Ok(new { data = names });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving school head names");
                return StatusCode(500, new { message = "An error occurred while retrieving school head names." });
            }
        }

        [HttpGet("school/resumption-date")]
        public async Task<IActionResult> GetResumptionDate(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                return Ok(new { resumptionDate = currentTerm?.NextTermBeginsDate });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving next-term resumption date");
                return StatusCode(500, new { message = "An error occurred while retrieving the resumption date." });
            }
        }

        [HttpPost("school/resumption-date")]
        public async Task<IActionResult> SetResumptionDate([FromBody] DateTime resumptionDate, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                if (currentTerm == null)
                {
                    return BadRequest(new { message = "Continuous assessment intitial background preparation needs to be done before this." });
                }

                currentTerm.NextTermBeginsDate = resumptionDate;
                _unitOfWork.Repository<TxYearlyAndTermlyStatus>().Update(currentTerm);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { message = "The next term date has successfully been submited." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting next-term resumption date");
                return StatusCode(500, new { message = "An error occurred while setting the resumption date." });
            }
        }

        [HttpGet("assessment/pass-mark")]
        public async Task<IActionResult> GetPassMark(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                return Ok(new { passMark = currentTerm?.MinPassMark });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pass mark");
                return StatusCode(500, new { message = "An error occurred while retrieving pass mark." });
            }
        }

        [HttpPost("assessment/pass-mark")]
        public async Task<IActionResult> SetPassMark([FromBody] byte passMark, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                if (currentTerm == null)
                {
                    return BadRequest(new { message = "Failed! Other registration first need to be done before here." });
                }

                currentTerm.MinPassMark = passMark;
                _unitOfWork.Repository<TxYearlyAndTermlyStatus>().Update(currentTerm);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { message = "Pass mark has successfully been saved.", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting pass mark");
                return StatusCode(500, new { message = "An error occurred while setting pass mark." });
            }
        }

        [HttpGet("attendance/total")]
        public async Task<IActionResult> GetTotalAttendance(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                return Ok(new { totalAttendance = currentTerm?.TotalAttendance });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total attendance");
                return StatusCode(500, new { message = "An error occurred while retrieving total attendance." });
            }
        }

        [HttpPost("attendance/total")]
        public async Task<IActionResult> SetTotalAttendance([FromBody] byte noOfDaysInThisTerm, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                if (currentTerm == null)
                {
                    return BadRequest(new { message = "Continuous assessment intitial background preparation needs to be done before this." });
                }

                currentTerm.TotalAttendance = noOfDaysInThisTerm;
                _unitOfWork.Repository<TxYearlyAndTermlyStatus>().Update(currentTerm);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { message = "The next term date has successfully been submited." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting total attendance");
                return StatusCode(500, new { message = "An error occurred while setting total attendance." });
            }
        }

        // ── Daily attendance register (learners + facilitators) ──────────────────

        [HttpGet("daily-attendance/learners")]
        public async Task<IActionResult> GetLearnerDailyRegister(
            [FromQuery] string armId,
            [FromQuery] DateTime? date,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(armId))
            {
                return BadRequest(new { message = "armId is required." });
            }

            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var roster = await _dailyAttendanceService.GetLearnerRosterAsync(
                    userId, armId, date ?? DateTime.Today, ct);
                return Ok(roster);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading the learner register for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while loading the register." });
            }
        }

        [HttpPost("daily-attendance/learners")]
        public async Task<IActionResult> SaveLearnerDailyRegister(
            [FromBody] DailyAttendanceSubmitDto model,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _dailyAttendanceService.SaveLearnerRegisterAsync(userId, model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving the learner register");
                return StatusCode(500, new { message = "An error occurred while saving the register." });
            }
        }

        [HttpGet("daily-attendance/facilitators")]
        public async Task<IActionResult> GetFacilitatorDailyRegister(
            [FromQuery] DateTime? date,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var roster = await _dailyAttendanceService.GetFacilitatorRosterAsync(
                    userId, date ?? DateTime.Today, ct);
                return Ok(roster);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading the facilitator register");
                return StatusCode(500, new { message = "An error occurred while loading the register." });
            }
        }

        [HttpPost("daily-attendance/facilitators")]
        public async Task<IActionResult> SaveFacilitatorDailyRegister(
            [FromBody] DailyAttendanceSubmitDto model,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _dailyAttendanceService.SaveFacilitatorRegisterAsync(userId, model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving the facilitator register");
                return StatusCode(500, new { message = "An error occurred while saving the register." });
            }
        }

        [HttpGet("daily-attendance/summary")]
        public async Task<IActionResult> GetDailyAttendanceSummary(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var toDate = to ?? DateTime.Today;
                var fromDate = from ?? toDate.AddDays(-29);
                var summary = await _dailyAttendanceService.GetSummaryAsync(userId, fromDate, toDate, ct);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading the attendance summary");
                return StatusCode(500, new { message = "An error occurred while loading the attendance summary." });
            }
        }

        [HttpPost("daily-attendance/sync-term-totals")]
        public async Task<IActionResult> SyncTermAttendanceTotals(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _dailyAttendanceService.SyncTermTotalsAsync(userId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing term attendance totals");
                return StatusCode(500, new { message = "An error occurred while syncing attendance to report cards." });
            }
        }

        [HttpGet("electives/access-status")]
        public async Task<IActionResult> GetElectiveAccessStatus(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var schoolInfo = await _unitOfWork.Repository<SchoolTyInfo>().FindAsync(userId, ct);
                var status = schoolInfo?.ElectiveAccessStatus ?? AccessStatus.Disable;

                var dropDown = new List<object>
                {
                    new
                    {
                        text = status == AccessStatus.Active ? "Enable" : status.ToString(),
                        value = ((int)status).ToString()
                    }
                };

                dropDown.AddRange(
                    Enum.GetValues<AccessStatus>()
                        .Where(item => item != status)
                        .Select(item => (object)new
                        {
                            text = item == AccessStatus.Active ? "Enable" : item.ToString(),
                            value = ((int)item).ToString()
                        }));

                return Ok(dropDown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving elective access status");
                return StatusCode(500, new { message = "An error occurred while retrieving elective access status." });
            }
        }

        [HttpPost("electives/access-status")]
        public async Task<IActionResult> SetElectiveAccessStatus([FromBody] byte? selectionId, CancellationToken ct)
        {
            try
            {
                if (selectionId == null)
                {
                    return BadRequest(new { message = "Selection status is required." });
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var schoolInfoRepository = _unitOfWork.Repository<SchoolTyInfo>();
                var schoolInfo = await schoolInfoRepository.FindAsync(userId, ct);
                if (schoolInfo == null)
                {
                    return NotFound(new { message = "School record was not found." });
                }

                schoolInfo.ElectiveAccessStatus = selectionId == 0 ? AccessStatus.Active : AccessStatus.Disable;
                schoolInfoRepository.Update(schoolInfo);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting elective access status");
                return StatusCode(500, new { message = "An error occurred while setting elective access status." });
            }
        }

        [HttpGet("assessment/ca-settings")]
        public async Task<IActionResult> GetCurrentCaSettings(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                if (currentTerm == null)
                {
                    return Ok(new
                    {
                        numberOfCasCategoyDropdown = Array.Empty<object>(),
                        perCasValue = (byte?)null
                    });
                }

                var numberOfCasList = new List<object>
                {
                    new
                    {
                        text = currentTerm.CasCategory.ToString(),
                        value = ((int)currentTerm.CasCategory).ToString()
                    }
                };

                numberOfCasList.AddRange(
                    Enum.GetValues<CasCategory>()
                        .Where(item => item != currentTerm.CasCategory)
                        .Select(item => (object)new
                        {
                            text = item.ToString(),
                            value = ((int)item).ToString()
                        }));

                return Ok(new
                {
                    numberOfCasCategoyDropdown = numberOfCasList,
                    perCasValue = currentTerm.PerCasValue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current CA settings");
                return StatusCode(500, new { message = "An error occurred while retrieving CA settings." });
            }
        }

        [HttpPost("assessment/ca-settings")]
        public async Task<IActionResult> SubmitCaSetting([FromBody] SubmitCaSettingRequest request, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                if (currentTerm == null)
                {
                    return BadRequest(new { message = "Failed! Other registration first need to be done before here." });
                }

                currentTerm.CasCategory = request.CasCategory;
                currentTerm.PerCasValue = request.PerCasValue;
                _unitOfWork.Repository<TxYearlyAndTermlyStatus>().Update(currentTerm);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { message = "Your changes has successfully been saved.", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting CA settings");
                return StatusCode(500, new { message = "An error occurred while saving CA settings." });
            }
        }

        [HttpGet("assessment/deadline")]
        public async Task<IActionResult> GetAssessmentDeadline(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                return Ok(new { deadline = currentTerm?.AssessmtEndDate });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assessment deadline");
                return StatusCode(500, new { message = "An error occurred while retrieving assessment deadline." });
            }
        }

        [HttpPost("assessment/deadline/set")]
        public async Task<IActionResult> SetAssessmentDeadline([FromBody] DateTime deadline, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var currentTerm = await GetCurrentOrResultProcessingTermAsync(ct);
                if (currentTerm == null)
                {
                    return BadRequest(new { message = "Continuous assessment intitial background preparation needs to be done before this." });
                }

                currentTerm.AssessmtEndDate = deadline;
                _unitOfWork.Repository<TxYearlyAndTermlyStatus>().Update(currentTerm);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { message = "The assessment deadline has successfully been created." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting assessment deadline");
                return StatusCode(500, new { message = "An error occurred while setting assessment deadline." });
            }
        }

        [HttpGet("assessment/completion-status/current-term")]
        public async Task<IActionResult> GetCurrentTermAssessmentCompletionStatus(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var assignments = await _unitOfWork.Repository<TeacherClassArmTxAssignment>().WhereAsync(
                    assignment => assignment.Employee != null && assignment.Employee.SchoolId == userId,
                    ct,
                    assignment => assignment.Employee!);

                var hasUncompleted = assignments.Any(item => item.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted);
                var status = hasUncompleted ? ArmTermlyAssessmentStatus.Uncompleted : ArmTermlyAssessmentStatus.Completed;
                var message = hasUncompleted
                    ? "Current term assessment is marked as Uncompleted."
                    : "Current term assessment is marked as Completed.";

                return Ok(new { status = (byte)status, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current-term assessment completion status");
                return StatusCode(500, new { message = "An error occurred while retrieving assessment completion status." });
            }
        }

        [HttpPost("assessment/completion-status/current-term/set")]
        public async Task<IActionResult> SetCurrentTermAssessmentCompletionStatus([FromBody] ArmTermlyAssessmentStatus status, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var assignmentRepository = _unitOfWork.Repository<TeacherClassArmTxAssignment>();
                var assignments = await assignmentRepository.WhereAsync(
                    assignment => assignment.Employee != null && assignment.Employee.SchoolId == userId,
                    ct,
                    assignment => assignment.Employee!);

                foreach (var assignment in assignments)
                {
                    assignment.ArmTermlyAssessmentStatus = status;
                    assignmentRepository.Update(assignment);
                }

                await _unitOfWork.SaveChangesAsync(ct);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting current-term assessment completion status");
                return StatusCode(500, new { message = "An error occurred while setting assessment completion status." });
            }
        }

        [HttpGet("term/revert-to-last")]
        [HttpPost("term/revert-to-last")]
        public async Task<IActionResult> RevertToLastTerm(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var yearlyAndTermlyStatusRepository = _unitOfWork.Repository<TxYearlyAndTermlyStatus>();
                var currentTerm = await yearlyAndTermlyStatusRepository.FindAsync(
                    item => item.Session != null
                        && item.Session.SchoolInfoId == userId
                        && item.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    ct,
                    item => item.Session!);

                if (currentTerm == null)
                {
                    return Ok(new { status = string.Empty, message = "No current term record found." });
                }

                var studentAccessmentRepository = _unitOfWork.Repository<StudenXtAccessment>();
                var currentTermAssessmentList = await studentAccessmentRepository.WhereAsync(
                    item => item.YearlyAndTermlyStatus != null
                        && item.YearlyAndTermlyStatus.Session != null
                        && item.YearlyAndTermlyStatus.Session.SchoolInfoId == userId
                        && item.YearlyAndTermlyStatusId == currentTerm.Id,
                    ct,
                    item => item.YearlyAndTermlyStatus!);

                var hasAssessmentRecorded = currentTermAssessmentList.FirstOrDefault(item => item.Ca1 > 0 || item.Ca2 > 0 || item.Ca3 > 0);
                if (hasAssessmentRecorded != null)
                {
                    return Ok(new { status = string.Empty, message = "You need to first clear all the students assessment scores you have entered for this term" });
                }

                if (currentTermAssessmentList.Any() && currentTermAssessmentList[0].YearlyAndTermlyStatus?.TermStatus == TermStatus.First)
                {
                    return Ok(new { status = "FistTerm", message = "Revert failed. First term cannot be reverted." });
                }

                foreach (var item in currentTermAssessmentList)
                {
                    studentAccessmentRepository.Delete(item);
                }

                var affectiveRepository = _unitOfWork.Repository<AffectiveAndPsychomoTxtorAssessment>();
                var affectiveList = await affectiveRepository.WhereAsync(
                    item => item.YearlyAndTermlyStatus != null
                        && item.YearlyAndTermlyStatus.Session != null
                        && item.YearlyAndTermlyStatus.Session.SchoolInfoId == userId
                        && item.YearlyAndTermlyStatusId == currentTerm.Id,
                    ct,
                    item => item.YearlyAndTermlyStatus!);

                foreach (var item in affectiveList)
                {
                    affectiveRepository.Delete(item);
                }

                var progressRepository = _unitOfWork.Repository<StudentAcademicTxProgressHistory>();
                var progressList = await progressRepository.WhereAsync(
                    item => item.YearlyAndTermlyStatus != null
                        && item.YearlyAndTermlyStatus.Session != null
                        && item.YearlyAndTermlyStatus.Session.SchoolInfoId == userId
                        && item.YearlyAndTermlyStatusId == currentTerm.Id,
                    ct,
                    item => item.YearlyAndTermlyStatus!);

                foreach (var item in progressList)
                {
                    progressRepository.Delete(item);
                }

                yearlyAndTermlyStatusRepository.Delete(currentTerm);
                await _unitOfWork.SaveChangesAsync(ct);

                var terms = await yearlyAndTermlyStatusRepository.WhereAsync(
                    item => item.Session != null && item.Session.SchoolInfoId == userId,
                    ct,
                    item => item.Session!);

                var lastTerm = terms.OrderBy(item => item.Id).LastOrDefault();
                if (lastTerm != null)
                {
                    lastTerm.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;
                    yearlyAndTermlyStatusRepository.Update(lastTerm);
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                return Ok(new { status = "Success", message = "System has reverted to the last term successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reverting to the last term");
                return StatusCode(500, new { message = "An error occurred while reverting to the last term." });
            }
        }

        private string? GetCurrentUserId()
        {
            var user = User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // JWT may keep short names (primarysid/sub) or long ClaimTypes URIs depending on MapInboundClaims.
            return user.FindFirstValue(ClaimTypes.PrimarySid)
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? user.FindFirstValue("primarysid")
                ?? user.FindFirstValue("sub")
                ?? user.Claims.FirstOrDefault(claim =>
                    claim.Type.EndsWith("/primarysid", StringComparison.OrdinalIgnoreCase)
                    || claim.Type.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private async Task<SchoolTyInfo?> ResolveRegistrySchool(CancellationToken ct)
        {
            return await ResolveRegistrySchoolEntityAsync(ct);
        }

        private async Task<string?> ResolveRegistrySchoolIdAsync(CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var schoolRepo = _unitOfWork.Repository<SchoolTyInfo, string>();
            var direct = await schoolRepo.FindAsync(
                entry => entry.SchoolRegistrationOfficerId == userId
                    || (entry.SchoolRegistrationOfficer != null && entry.SchoolRegistrationOfficer.UserId == userId),
                ct);
            if (direct != null && !string.IsNullOrWhiteSpace(direct.SchoolRegistrationOfficerId))
            {
                return direct.SchoolRegistrationOfficerId.Trim();
            }

            var schoolInfo = await _registrationService.GetSchoolModelForSchoolRegistrationOfficer(userId, ct);
            return string.IsNullOrWhiteSpace(schoolInfo?.SchoolRegistrationOfficerId)
                ? null
                : schoolInfo.SchoolRegistrationOfficerId.Trim();
        }

        private async Task<SchoolTyInfo?> ResolveRegistrySchoolEntityAsync(CancellationToken ct)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var schoolRepo = _unitOfWork.Repository<SchoolTyInfo, string>();
            return await schoolRepo.FindAsync(schoolId, ct)
                ?? await schoolRepo.FindAsync(entry => entry.SchoolRegistrationOfficerId == schoolId, ct);
        }

        private async Task<SchoolTyInfo?> ResolveRegistrySchoolEntityForUpdateAsync(CancellationToken ct)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            return await _unitOfWork.Repository<SchoolTyInfo, string>().FindAsync(schoolId, ct);
        }

        private static async Task<string?> TheRightNaitsHostName(CancellationToken ct)
        {
            foreach (var hostName in new[] { "http://www.ourschoolonline.ng", "https://www.ourschoolonline.ng" })
            {
                try
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    using var response = await httpClient.GetAsync(hostName, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        return hostName;
                    }
                }
                catch(Exception ex)
                {

                }
            }

            return null;
        }

        private static async Task<string> GetRemoteString(string url, CancellationToken ct)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            return await httpClient.GetStringAsync(url, ct);
        }

        private async Task<string> NoOfReTrial(CancellationToken ct)
        {
            var programInfoRepository = _unitOfWork.Repository<ProgramYInfo, byte>();
            var attemptRow = await programInfoRepository.FindAsync(row => row.Machines == "Attempt", ct);
            var valueRow = await programInfoRepository.FindAsync(row => row.Machines == "Value", ct);
            if (attemptRow == null || valueRow == null)
            {
                return "Failed, Unable to complete the process!";
            }

            if (!short.TryParse(FreePlugIn.Decrypt(valueRow.Matches, true), out var noOfTrial))
            {
                return "Failed, Unable to complete the process!";
            }

            DateTime lastAttempt;
            try
            {
                lastAttempt = DateTimeFromJson(FreePlugIn.Decrypt(attemptRow.Matches, true));
            }
            catch
            {
                return "Failed, Unable to complete the process!";
            }

            noOfTrial++;
            if (DateTime.Today != lastAttempt.Date)
            {
                noOfTrial = 1;
                attemptRow.Matches = FreePlugIn.Encrypt(DateTimeToJson(DateTime.Today), true);
                valueRow.Matches = FreePlugIn.Encrypt(noOfTrial.ToString(), true);
                programInfoRepository.Update(attemptRow);
                programInfoRepository.Update(valueRow);
                return "Operation was successfully completed.";
            }

            if (noOfTrial >= 5)
            {
                return "You exhausted the maximum number of registration attempt per day." + Environment.NewLine + " Therefore, you cannot register today again.";
            }

            valueRow.Matches = FreePlugIn.Encrypt(noOfTrial.ToString(), true);
            programInfoRepository.Update(valueRow);
            return "Operation was successfully completed.";
        }

        private async Task UpdateMachineInfo(string response, CancellationToken ct)
        {
            // Legacy SecurityService.UpdateMachineInfo
            var encodedNic = FreePlugIn.GetUniqueSystemId();
            var compCodeArrayPublicValue = FreePlugIn.Encrypt(encodedNic, true);

            var programInfoRepository = _unitOfWork.Repository<ProgramYInfo, byte>();
            var machineRow = await programInfoRepository.FindAsync((byte)19, ct);
            if (machineRow != null)
            {
                machineRow.Machines = compCodeArrayPublicValue;
                programInfoRepository.Update(machineRow);
            }
            else
            {
                programInfoRepository.Add(new ProgramYInfo
                {
                    Id = 19,
                    Machines = compCodeArrayPublicValue,
                });
            }

            var credentialString = FreePlugIn.Decrypt(response, true);
            var splitCredentialDecoded = credentialString.Split(':').ToList();
            if (splitCredentialDecoded.Count == 0 || string.IsNullOrWhiteSpace(splitCredentialDecoded[0]))
            {
                return;
            }

            var token = splitCredentialDecoded[0];
            var userId = GetCurrentUserId();
            SchoolTyInfo? school = null;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var officer = await _unitOfWork.Repository<SchoolRegistrationOfficer>()
                    .FindAsync(entry => entry.UserId == userId, ct, entry => entry.SchoolInfo!);
                school = officer?.SchoolInfo;
            }

            school ??= await ResolveRegistrySchool(ct);
            if (school == null)
            {
                return;
            }

            school.SmsApiToken = token;
            school.SmsApiKey = compCodeArrayPublicValue;
            _unitOfWork.Repository<SchoolTyInfo, string>().Update(school);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        private async Task UpdateFeatureImplementationTypeAndNoOfStudent(string featureType, string noOfStudents, CancellationToken ct)
        {
            var school = await ResolveRegistrySchool(ct);
            if (school == null)
            {
                return;
            }

            school.FeatureImplementationType = featureType;
            school.TotalStudents = noOfStudents;
            _unitOfWork.Repository<SchoolTyInfo, string>().Update(school);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        private async Task<(byte status, DateTime? expiryDate)> ExtendSubscriptionValidity(string licenseExtractedObject, CancellationToken ct)
        {
            var splitString = licenseExtractedObject.Split('|').ToList();
            if (splitString.Count < 3)
            {
                return (1, null);
            }

            var decodeToList = FreePlugIn.Decrypt(splitString[0], true).Split('~').ToList();
            if (decodeToList.Count < 4)
            {
                return (1, null);
            }

            DateTime paymentDate;
            DateTime expiryDate;
            try
            {
                paymentDate = DateTimeFromJson(decodeToList[0]);
                expiryDate = DateTimeFromJson(decodeToList[1]);
            }
            catch (Exception)
            {
                return (4, null);
            }

            var subscriptionId = int.TryParse(decodeToList[3], out var parsedSubscriptionId) ? parsedSubscriptionId : 0;
            var credential = splitString[1];
            var featureImplementationType = splitString[2];

            await UpdateMachineInfo(credential, ct);

            var school = await ResolveRegistrySchool(ct);
            if (school == null)
            {
                return (2, null);
            }

            school.MyStatus = SubscriptionStatus.HasConfirmed;
            school.FeatureImplementationType = featureImplementationType;
            _unitOfWork.Repository<SchoolTyInfo, string>().Update(school);

            var licenseRepository = _unitOfWork.Repository<LicensTying, string>();
            var currentLicense = await licenseRepository.FindAsync(
                license => license.SchoolSmsId == school.SchoolRegistrationOfficerId && license.TimeStatus == TimeStatus.Current,
                ct);

            if (currentLicense != null)
            {
                currentLicense.PaymentDate = paymentDate;
                currentLicense.ExpiryDate = expiryDate;
                currentLicense.SubscriptionId = subscriptionId;
                currentLicense.RenewalConsumptionStatus = RenewalConsumptionStatus.HasAreadyBeenConsummed;
                licenseRepository.Update(currentLicense);
            }

            // Legacy SecurityService.ExtendSubscriptionValidity also rewrites ProgramYInfo.
            // Row 10 holds the expiry; Status "123" is the active code Authentication() accepts.
            // Leaving Status as Encrypt("2") makes /admin keep showing the expired subscription.
            await _securityService.ApplyRenewedProgramExpiryAsync(expiryDate, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            return (0, expiryDate);
        }

        private static string DateTimeToJson(DateTime date) =>
            LegacyJsonDate.ToJson(date);

        private static DateTime DateTimeFromJson(string dateString) =>
            LegacyJsonDate.FromJson(dateString);

        /// <summary>
        /// Admission numbers often contain '/' (e.g. AMA/PS/24/0859). Path binding / %2F handling
        /// can leave them escaped; normalize before IdNo lookup (legacy used query-string studentId).
        /// </summary>
        private static string NormalizeAdmissionNumber(string? studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return string.Empty;
            }

            var trimmed = studentId.Trim();
            try
            {
                var once = Uri.UnescapeDataString(trimmed);
                var twice = Uri.UnescapeDataString(once);
                return twice.Trim();
            }
            catch (UriFormatException)
            {
                return trimmed;
            }
        }

        private static string RenewalFailureMessage(byte status)
        {
            return status switch
            {
                1 => "Failed! Wrong registration Key.",
                2 => "Failed! You need to have an enabled 'network interface card' to register this application.",
                3 => "You have exhausted the maximum number of registration attempt per day." + Environment.NewLine + " Therefore, you cannot register today again.",
                4 => "Failed! Your computer date is not in the right format.",
                _ => "Failed! Unable to complete the renewal process."
            };
        }

        private async Task<TxYearlyAndTermlyStatus?> GetCurrentOrResultProcessingTermAsync(CancellationToken ct)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            return await _unitOfWork.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                item => item.Session != null
                    && item.Session.SchoolInfoId == schoolId
                    && (item.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                        || item.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct,
                item => item.Session!);
        }

        public sealed class SubmitCaSettingRequest
        {
            public CasCategory CasCategory { get; set; }
            public byte PerCasValue { get; set; }
        }

        private static readonly HashSet<string> AllowedSignatureContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png"
        };

        private static string? ValidateSignatureImagePayload(string? signatureImageString)
        {
            if (string.IsNullOrWhiteSpace(signatureImageString))
            {
                return null;
            }

            var value = signatureImageString.Trim();
            if (!value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var dataUrlMatch = Regex.Match(
                value,
                @"^data:image/(?<ext>[a-zA-Z0-9+]+);base64,(?<data>.+)$",
                RegexOptions.Singleline);

            if (!dataUrlMatch.Success)
            {
                return "Failed to process the signature image. Please try again.";
            }

            var contentType = dataUrlMatch.Groups["ext"].Value.ToLowerInvariant() switch
            {
                "jpg" => "image/jpeg",
                "jpeg" => "image/jpeg",
                "png" => "image/png",
                var ext => $"image/{ext}"
            };

            if (!AllowedSignatureContentTypes.Contains(contentType))
            {
                return "Only 'JPEG' and 'png' image are allowed.";
            }

            try
            {
                var imageBytes = Convert.FromBase64String(dataUrlMatch.Groups["data"].Value);
                if ((imageBytes.Length / 1024d) > 13)
                {
                    return "Your signature size is too big. The maximum size is 10kb";
                }
            }
            catch (FormatException)
            {
                return "Failed to process the signature image. Please try again.";
            }

            return null;
        }

        private async Task<string> NormalizeSignatureImageAsync(
            string? signatureImageString,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(signatureImageString))
            {
                return string.Empty;
            }

            var value = signatureImageString.Trim();
            if (!value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return value.Length <= 40 ? value : value[..40];
            }

            var dataUrlMatch = Regex.Match(
                value,
                @"^data:image/(?<ext>[a-zA-Z0-9+]+);base64,(?<data>.+)$",
                RegexOptions.Singleline);

            if (!dataUrlMatch.Success)
            {
                return string.Empty;
            }

            try
            {
                var ext = dataUrlMatch.Groups["ext"].Value.ToLowerInvariant() switch
                {
                    "jpeg" => "jpg",
                    "svg+xml" => "svg",
                    _ => dataUrlMatch.Groups["ext"].Value.ToLowerInvariant()
                };

                var bytes = Convert.FromBase64String(dataUrlMatch.Groups["data"].Value);

                var webRoot = _environment.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                {
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var folder = Path.Combine(webRoot, "Signatures");
                Directory.CreateDirectory(folder);

                var fileName = $"{Guid.NewGuid():N}.{ext}";
                if (fileName.Length > 40)
                {
                    fileName = $"{Guid.NewGuid():N}.png";
                }

                await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, fileName), bytes, ct);
                return fileName;
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Invalid base64 signature image payload received.");
                return string.Empty;
            }
        }

        #region Student Results

        [HttpGet("student-results/sessions")]
        public async Task<IActionResult> GetStudentResultsSessions(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var sessions = await _registrationService.GetStudentResultsSessionsDropdown(userId, ct);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student-results sessions");
                return StatusCode(500, new { message = "An error occurred while retrieving sessions." });
            }
        }

        [HttpGet("student-results/sessions/{sessionId}/terms")]
        public async Task<IActionResult> GetStudentResultsTerms(string sessionId, CancellationToken ct)
        {
            try
            {
                var terms = await _registrationService.GetStudentResultsTermsDropdown(sessionId, ct);
                return Ok(terms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student-results terms for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while retrieving terms." });
            }
        }

        [HttpGet("student-results/classes")]
        public async Task<IActionResult> GetStudentResultsClasses(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var classes = (await _registrationService.GetStudentResultsClasses(userId, ct)).ToList();
                return Ok(new { totalNoOfClasses = classes.Count, data = classes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student-results classes");
                return StatusCode(500, new { message = "An error occurred while retrieving classes." });
            }
        }

        [HttpGet("student-results/classes/{classId}/arms")]
        public async Task<IActionResult> GetStudentResultsClassArms(string classId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return BadRequest(new { message = "classId is required." });
            }

            try
            {
                var arms = (await _registrationService.GetStudentResultsClassArms(classId, ct)).ToList();
                if (arms.Count == 0)
                {
                    return NoContent();
                }

                return Ok(new { totalNoOfArms = arms.Count, data = arms });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student-results arms for class {ClassId}", classId);
                return StatusCode(500, new { message = "An error occurred while retrieving class arms." });
            }
        }

        /// <summary>
        /// Legacy SelectedStudentTermlyReportSheet — studentId is a query param because admission
        /// numbers contain slashes (e.g. AMA/PS/24/0859) that break path routing.
        /// </summary>
        [HttpGet("student-results/termly")]
        public async Task<IActionResult> GetStudentResultsTermlyAssessment(
            [FromQuery] string studentId,
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var normalizedStudentId = NormalizeAdmissionNumber(studentId);
                if (string.IsNullOrWhiteSpace(normalizedStudentId))
                {
                    return BadRequest(new { message = "studentId is required." });
                }

                var assessment = await _registrationService.GetTermlyAssessmentForSelectedStudent(
                    userId, normalizedStudentId, sessionId, termStatus, ct);
                return Ok(assessment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving termly assessment for student {StudentId}", studentId);
                return StatusCode(500, new { message = "An error occurred while retrieving termly assessment." });
            }
        }

        /// <summary>
        /// Legacy SelectedStudentYearlyReportSheet — studentId as query param (slash-safe).
        /// </summary>
        [HttpGet("student-results/yearly")]
        public async Task<IActionResult> GetStudentResultsYearlyAssessment(
            [FromQuery] string studentId,
            [FromQuery] string selectedSessionId,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var normalizedStudentId = NormalizeAdmissionNumber(studentId);
                if (string.IsNullOrWhiteSpace(normalizedStudentId))
                {
                    return BadRequest(new { message = "studentId is required." });
                }

                var assessment = await _registrationService.GetYearlyAssessmentForSelectedStudent(
                    userId, normalizedStudentId, selectedSessionId, ct);
                return Ok(assessment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving yearly assessment for student {StudentId}", studentId);
                return StatusCode(500, new { message = "An error occurred while retrieving yearly assessment." });
            }
        }

        [HttpPost("student-results/process-all")]
        public async Task<IActionResult> ProcessAllStudentResults(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.GeneralResultProcessing(userId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing all student results");
                return StatusCode(500, new { message = "An error occurred while processing all student results." });
            }
        }

        /// <summary>
        /// Legacy: PerClassAndThirdTermCumulativeResultProcessing
        /// </summary>
        [HttpGet("student-results/per-class/cumulative")]
        public async Task<IActionResult> PerClassAndThirdTermCumulativeResultProcessing(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            CancellationToken ct)
            => await RunClassCollectionResultAsync(
                (userId, token) => _registrationService.PerClassAndThirdTermCumulativeResultProcessing(
                    userId, sessionId, termStatus, className, token),
                className,
                ct);

        /// <summary>
        /// Legacy: PerClassThirdTermResultProcessing
        /// </summary>
        [HttpGet("student-results/per-class/third-term")]
        public async Task<IActionResult> PerClassThirdTermResultProcessing(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            CancellationToken ct)
            => await RunClassCollectionResultAsync(
                (userId, token) => _registrationService.PerClassThirdTermResultProcessing(
                    userId, sessionId, termStatus, className, token),
                className,
                ct);

        /// <summary>
        /// Legacy: PerArmAndThirdTermCumulativeResultProcessing
        /// </summary>
        [HttpGet("student-results/per-arm/cumulative")]
        public async Task<IActionResult> PerArmAndThirdTermCumulativeResultProcessing(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            [FromQuery] ArmName armName,
            CancellationToken ct)
            => await RunArmCollectionResultAsync(
                (userId, token) => _registrationService.PerArmAndThirdTermCumulativeResultProcessing(
                    userId, sessionId, termStatus, className, armName, token),
                className,
                armName,
                ct);

        /// <summary>
        /// Legacy: PerArmThirdTermResultProcessing
        /// </summary>
        [HttpGet("student-results/per-arm/third-term")]
        public async Task<IActionResult> PerArmThirdTermResultProcessing(
            [FromQuery] CurrentClass className,
            [FromQuery] ArmName armName,
            CancellationToken ct)
            => await RunArmCollectionResultAsync(
                (userId, token) => _registrationService.PerArmThirdTermResultProcessing(
                    userId, className, armName, token),
                className,
                armName,
                ct);

        /// <summary>
        /// Backward-compatible alias for older clients.
        /// </summary>
        [HttpGet("results/per-class-third-term-cumulative")]
        public Task<IActionResult> PerClassAndThirdTermCumulativeResultProcessingLegacy(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            CancellationToken ct)
            => PerClassAndThirdTermCumulativeResultProcessing(sessionId, termStatus, className, ct);

        [HttpGet("broadsheet-report/arms")]
        public async Task<IActionResult> GetBroadSheetArms(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var arms = await _registrationService.GetBroadSheetArmsDropdownAsync(userId, ct);
                return Ok(arms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving broadsheet arm dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving arm options." });
            }
        }

        [HttpGet("broadsheet-report/class")]
        public async Task<IActionResult> GetBroadSheetReportForClass(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(new { message = "Session is required." });
                }

                var result = await _registrationService.GetBroadSheetReportForClassAsync(
                    userId,
                    sessionId,
                    termStatus,
                    className,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building class broadsheet for {ClassName}", className);
                return StatusCode(500, new { message = "An error occurred while building the broadsheet report." });
            }
        }

        /// <summary>
        /// Legacy RegistryController.NewAssessmentHandler ViewBag (session/term/class/arm).
        /// </summary>
        [HttpGet("new-assessment-handler")]
        public async Task<IActionResult> GetNewAssessmentHandlerBootstrap(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.GetNewAssessmentHandlerBootstrapAsync(userId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading new assessment handler bootstrap");
                return StatusCode(500, new { message = "An error occurred while loading the assessment handler." });
            }
        }

        [HttpPost("new-assessment-handler/process")]
        public async Task<IActionResult> ProcessNewAssessment(
            [FromBody] NewAssessmentProcessRequestDto request,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                if (request == null)
                {
                    return BadRequest(new { message = "Request body is required." });
                }

                var result = await _registrationService.ProcessNewAssessmentAsync(userId, request, ct);
                var message = result?.Message ?? "Assessment processing completed.";
                var isSuccess = message.StartsWith("Success", StringComparison.OrdinalIgnoreCase);

                if (isSuccess)
                {
                    return Ok(new { message, data = result });
                }

                return BadRequest(new { message, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing new assessment handler request");
                return StatusCode(500, new { message = "An error occurred while processing the assessment request." });
            }
        }

        /// <summary>
        /// Legacy RegistryController.GeneralNewAssessmentProcessing(concatParameters).
        /// Invoked when Collection Size = All Students.
        /// </summary>
        [HttpGet("new-assessment-handler/general")]
        public async Task<IActionResult> GeneralNewAssessmentProcessing(
            [FromQuery] string concatParameters,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Ok(new { message = "Unable to resolve the current user. Please sign in again." });
                }

                if (_unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                }
                else
                {
                    _unitOfWork.RejectChanges();
                }

                var bStatus = await _securityService.B(ct);
                if (bStatus.AccessVerifyReturnValues != null)
                {
                    return Ok(new { message = "Sorry! Your account has been disabled." });
                }

                if (string.Equals(bStatus.LoginReturnStatus?.StatusCode, "Failure", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new { message = bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty });
                }

                await _unitOfWork.SaveChangesAsync(ct);

                if (string.Equals(bStatus.LoginReturnStatus?.StatusCode, "1", StringComparison.Ordinal))
                {
                    return Ok(new { message = bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty });
                }

                if (!string.Equals(bStatus.LoginReturnStatus?.StatusCode, "2", StringComparison.Ordinal))
                {
                    return Ok(new { message = bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty });
                }

                var implementationStatus = await _registrationService.GetSystemImplementationStatusAsync(userId, ct);
                var isPerTermImplementation =
                    implementationStatus == "0" ||
                    FreePlugIn.Decrypt(implementationStatus ?? string.Empty, true)
                        .Contains(":0", StringComparison.Ordinal);

                if (!isPerTermImplementation)
                {
                    var checkStatus = await _registrationService.GetAppropriatePaymentStatusAsync(userId, ct);
                    if (checkStatus != 0)
                    {
                        return Ok(new
                        {
                            message = "Sorry! You need to pay for the extra " + checkStatus + " students you have recently added."
                        });
                    }
                }

                if (_unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                }
                else
                {
                    _unitOfWork.RejectChanges();
                }

                var status = await _registrationService.PrepareAssessmentEnvironmentForTheWholeStudentAsync(
                    userId,
                    concatParameters,
                    ct);

                if (status == null)
                {
                    // Whole-school wire is always assessmentCategory,actionType[,psycho] — fixed indices.
                    var itemList = (concatParameters ?? string.Empty).Split(',');
                    var actionType = itemList.Length > 1 ? itemList[1] : string.Empty;
                    if (actionType == "1")
                    {
                        return Ok(new { message = "promotion has just been done and therefore, no psychomotor to delete" });
                    }

                    return Ok(new
                    {
                        message =
                            "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."
                    });
                }

                if (string.IsNullOrEmpty(status.Message))
                {
                    return Ok(new { message = string.Empty, data = status });
                }

                // Legacy commits for both exact "Success" and "Success. Note: ..." style messages.
                if (status.Message.StartsWith("Success", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "General assessment commit failed");
                        if (_unitOfWork.HasActiveTransaction)
                        {
                            await _unitOfWork.RollbackTransactionAsync();
                        }
                        else
                        {
                            _unitOfWork.RejectChanges();
                        }

                        return Ok(new { message = "Failed! Unable to complete the process." });
                    }

                    var total = status.TotalStudentListToPdf?.Count;
                    if (total != null && total <= 0)
                    {
                        return Ok(new
                        {
                            message = "Sorry! No Fresher/Returning student was found in the selected class.",
                            data = status
                        });
                    }

                    return Ok(new { message = status.Message, data = status });
                }

                await _unitOfWork.SaveChangesAsync(ct);
                return Ok(new { message = status.Message, data = status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GeneralNewAssessmentProcessing");
                var detail = ex.ToString();
                try
                {
                    var dumpPath = Path.Combine(Path.GetTempPath(), "naits-general-assessment-error.txt");
                    await System.IO.File.WriteAllTextAsync(dumpPath, detail, ct);
                }
                catch
                {
                    // ignore dump failures
                }

                return StatusCode(500, new
                {
                    message = "An error occurred while processing the whole-school assessment request.",
                    detail = ex.GetBaseException().Message
                });
            }
        }

        /// <summary>
        /// Legacy RegistryController.PerClassNewAssessmentProcessing(concatParameters).
        /// Invoked by the Class change event when Collection Size = Per Class Students.
        /// </summary>
        [HttpGet("new-assessment-handler/per-class")]
        public async Task<IActionResult> PerClassNewAssessmentProcessing(
            [FromQuery] string concatParameters,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    // Prefer Ok+message (legacy-style) over 401 so the SPA does not clear the JWT / bounce to login.
                    return Ok(AssessmentMessage("Unable to resolve the current user. Please sign in again."));
                }

                if (_unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                }
                else
                {
                    _unitOfWork.RejectChanges();
                }

                var bStatus = await _securityService.B(ct);
                if (bStatus.AccessVerifyReturnValues != null)
                {
                    return Ok(AssessmentMessage("Sorry! Your account has been disabled."));
                }

                if (string.Equals(bStatus.LoginReturnStatus?.StatusCode, "Failure", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(AssessmentMessage(bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty));
                }

                await _unitOfWork.SaveChangesAsync(ct);

                if (string.Equals(bStatus.LoginReturnStatus?.StatusCode, "1", StringComparison.Ordinal))
                {
                    return Ok(AssessmentMessage(bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty));
                }

                if (!string.Equals(bStatus.LoginReturnStatus?.StatusCode, "2", StringComparison.Ordinal))
                {
                    return Ok(AssessmentMessage(bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty));
                }

                var implementationStatus = await _registrationService.GetSystemImplementationStatusAsync(userId, ct);
                var isPerTermImplementation =
                    implementationStatus == "0" ||
                    FreePlugIn.Decrypt(implementationStatus, true).Contains(":0", StringComparison.Ordinal);

                if (isPerTermImplementation)
                {
                    return await ExecutePerClassAssessmentAsync(userId, concatParameters, ct);
                }

                var checkStatus = await _registrationService.GetAppropriatePaymentStatusAsync(userId, ct);
                if (checkStatus == 0)
                {
                    return await ExecutePerClassAssessmentAsync(userId, concatParameters, ct);
                }

                return Ok(AssessmentMessage(
                    "Sorry! You need to pay for the extra " + checkStatus + " students you have recently added."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PerClassNewAssessmentProcessing");
                return StatusCode(500, AssessmentMessage("An error occurred while processing the per-class assessment request."));
            }
        }

        /// <summary>
        /// Legacy PerClassNewAssessmentProcessing service call path.
        /// Returns PrepareEnvironmentForStudentAssessmentReturnDto exactly (same type as
        /// PrepareAssessmentEnvironmentForaClassStudents).
        /// </summary>
        private async Task<IActionResult> ExecutePerClassAssessmentAsync(
            string userId,
            string concatParameters,
            CancellationToken ct)
        {
            var status = await _registrationService.PrepareAssessmentEnvironmentForaClassStudentsAsync(
                userId,
                concatParameters,
                ct);

            if (status == null)
            {
                // Wire: ClassEnum,ClassLabel,Session,assessmentCategory,actionType[,psycho]
                // ClassLabel may contain commas — read actionType from the end (same as service parse).
                var actionType = ReadNewAssessmentActionTypeFromConcat(concatParameters);
                if (actionType == "1")
                {
                    return Ok(AssessmentMessage("promotion has just been done and therefore, no psychomotor to delete"));
                }

                return Ok(AssessmentMessage(
                    "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."));
            }

            if (string.IsNullOrEmpty(status.Message))
            {
                return Ok(status);
            }

            if (status.Message == "Success")
            {
                var total = status.TotalStudentListToPdf?.Count;
                await _unitOfWork.SaveChangesAsync(ct);
                if (total != null && total <= 0)
                {
                    status.Message = "Sorry! No Fresher/Returning student was found in the selected class.";
                }

                return Ok(status);
            }

            if (status.Message.StartsWith("Success", StringComparison.Ordinal))
            {
                var total = status.TotalStudentListToPdf?.Count;
                await _unitOfWork.SaveChangesAsync(ct);
                if (total != null && total <= 0)
                {
                    status.Message = "Sorry! No Fresher/Returning student was found in the selected class.";
                }

                return Ok(status);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(status);
        }

        /// <summary>
        /// Legacy RegistryController.PerArmNewAssessmentProcessing(concatParameters).
        /// Invoked by the Arm change event when Collection Size = Per Arm Students.
        /// </summary>
        [HttpGet("new-assessment-handler/per-arm")]
        public async Task<IActionResult> PerArmNewAssessmentProcessing(
            [FromQuery] string concatParameters,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Ok(AssessmentMessage("Unable to resolve the current user. Please sign in again."));
                }

                if (_unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                }
                else
                {
                    _unitOfWork.RejectChanges();
                }

                var bStatus = await _securityService.B(ct);
                if (bStatus.AccessVerifyReturnValues != null)
                {
                    return Ok(AssessmentMessage("Sorry! Your account has been disabled."));
                }

                if (string.Equals(bStatus.LoginReturnStatus?.StatusCode, "Failure", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(AssessmentMessage(bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty));
                }

                await _unitOfWork.SaveChangesAsync(ct);

                if (string.Equals(bStatus.LoginReturnStatus?.StatusCode, "1", StringComparison.Ordinal))
                {
                    return Ok(AssessmentMessage(bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty));
                }

                if (!string.Equals(bStatus.LoginReturnStatus?.StatusCode, "2", StringComparison.Ordinal))
                {
                    return Ok(AssessmentMessage(bStatus.LoginReturnStatus?.ReturnMessage ?? string.Empty));
                }

                var implementationStatus = await _registrationService.GetSystemImplementationStatusAsync(userId, ct);
                var isPerTermImplementation =
                    implementationStatus == "0" ||
                    FreePlugIn.Decrypt(implementationStatus, true).Contains(":0", StringComparison.Ordinal);

                if (isPerTermImplementation)
                {
                    return await ExecutePerArmAssessmentAsync(userId, concatParameters, requireSuccessCommitGuard: false, ct);
                }

                var checkStatus = await _registrationService.GetAppropriatePaymentStatusAsync(userId, ct);
                if (checkStatus == 0)
                {
                    return await ExecutePerArmAssessmentAsync(userId, concatParameters, requireSuccessCommitGuard: true, ct);
                }

                return Ok(AssessmentMessage(
                    "Sorry! You need to pay for the extra " + checkStatus + " students you have recently added."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PerArmNewAssessmentProcessing");
                return StatusCode(500, AssessmentMessage("An error occurred while processing the per-arm assessment request."));
            }
        }

        /// <summary>
        /// Legacy PerArmNewAssessmentProcessing service call path.
        /// Returns PrepareEnvironmentForStudentAssessmentReturnDto exactly (same type as
        /// PrepareAssessmentEnvironmentForAnArmStudents).
        /// </summary>
        private async Task<IActionResult> ExecutePerArmAssessmentAsync(
            string userId,
            string concatParameters,
            bool requireSuccessCommitGuard,
            CancellationToken ct)
        {
            var status = await _registrationService.PrepareAssessmentEnvironmentForAnArmStudentsAsync(
                userId,
                concatParameters,
                ct);

            if (status == null)
            {
                // Wire: Arm,ClassEnum,ClassLabel,Session,assessmentCategory,actionType[,psycho]
                // ClassLabel may contain commas — read actionType from the end (same as service parse).
                var actionType = ReadNewAssessmentActionTypeFromConcat(concatParameters);
                if (actionType == "1")
                {
                    return Ok(AssessmentMessage("promotion has just been done and therefore, no psychomotor to delete"));
                }

                return Ok(AssessmentMessage(
                    "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."));
            }

            // Legacy Arm JS only substitutes "Success" when the HTTP body is null/undefined,
            // not when Message is intentionally empty.
            if (string.IsNullOrEmpty(status.Message))
            {
                return Ok(status);
            }

            if (requireSuccessCommitGuard && status.Message.StartsWith("Success", StringComparison.Ordinal))
            {
                try
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PerArm assessment commit failed");
                    if (_unitOfWork.HasActiveTransaction)
                    {
                        await _unitOfWork.RollbackTransactionAsync();
                    }
                    else
                    {
                        _unitOfWork.RejectChanges();
                    }

                    return Ok(AssessmentMessage("Failed! Unable to complete the process."));
                }

                var total = status.TotalStudentListToPdf?.Count;
                if (total != null && total <= 0)
                {
                    status.Message = "Sorry! No Fresher/Returning student was found in the selected class-arm.";
                }

                return Ok(status);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PerArm assessment commit failed");
                if (_unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                }
                else
                {
                    _unitOfWork.RejectChanges();
                }

                return Ok(AssessmentMessage("Failed! Unable to complete the process."));
            }

            return Ok(status);
        }

        private static PrepareEnvironmentForStudentAssessmentReturnDto AssessmentMessage(string message) =>
            new() { Message = message ?? string.Empty };

        /// <summary>
        /// Reads actionType from New Assessment Class/Arm concat wires where ClassLabel may contain commas.
        /// With psycho: last token is 0–6 and the prior two tokens are category/action ("0"/"1").
        /// Without psycho: action is the last token.
        /// </summary>
        private static string ReadNewAssessmentActionTypeFromConcat(string? concatParameters)
        {
            var itemList = (concatParameters ?? string.Empty).Split(',');
            if (itemList.Length == 0)
            {
                return string.Empty;
            }

            static bool IsZeroOrOne(string value) => value == "0" || value == "1";

            // Mirror RegistrationService.ParseCategoryActionPsychoFromEnd (with-psycho branch).
            // Trailing 0/1 is only psycho for Psychomotor Create (category=1, action=0).
            if (itemList.Length >= 3
                && byte.TryParse(itemList[^1], out var psycho)
                && psycho <= 6
                && IsZeroOrOne(itemList[^2])
                && IsZeroOrOne(itemList[^3])
                && (psycho > 1 || (itemList[^3] == "1" && itemList[^2] == "0")))
            {
                return itemList[^2];
            }

            return itemList[^1];
        }

        [HttpGet("new-assessment-handler/defaulted-teachers")]
        public async Task<IActionResult> GetAssessmentCompletionDefaultedTeachers(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await _registrationService.GetAssessmentCompletionDefaultedTeachersAsync(userId, ct);
                return Ok(new
                {
                    message = result?.Message ?? string.Empty,
                    teachers = result?.UncompletedFormMastersReport ?? new List<UncompletedFormMastersReportDto>(),
                    data = result,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assessment completion defaulted teachers");
                return StatusCode(500, new { message = "An error occurred while retrieving defaulted teachers." });
            }
        }

        [HttpGet("broadsheet-report/arm")]
        public async Task<IActionResult> GetBroadSheetReportForArm(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] CurrentClass className,
            [FromQuery] ArmName armName,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(new { message = "Session is required." });
                }

                var result = await _registrationService.GetBroadSheetReportForArmAsync(
                    userId,
                    sessionId,
                    termStatus,
                    className,
                    armName,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building arm broadsheet for {ClassName} {ArmName}", className, armName);
                return StatusCode(500, new { message = "An error occurred while building the broadsheet report." });
            }
        }



        private async Task<IActionResult> RunClassCollectionResultAsync(
            Func<string, CancellationToken, Task<PerClassAndThirdTermCumulativeResultProcessingDto>> action,
            CurrentClass className,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                if (string.IsNullOrWhiteSpace(className.ToString()))
                {
                    return BadRequest(new { message = "Class is required." });
                }

                var result = await action(userId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing class collection result for {ClassName}", className);
                return StatusCode(500, new { message = "An error occurred while processing class collection result." });
            }
        }

        private async Task<IActionResult> RunArmCollectionResultAsync(
            Func<string, CancellationToken, Task<PerClassAndThirdTermCumulativeResultProcessingDto>> action,
            CurrentClass className,
            ArmName armName,
            CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var result = await action(userId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing arm collection result for {ClassName} {ArmName}", className, armName);
                return StatusCode(500, new { message = "An error occurred while processing arm collection result." });
            }
        }

        #endregion

        [HttpGet("school/registration")]
        public async Task<IActionResult> GetSchoolRegistration(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var schoolInfo = await _registrationService.GetSchoolModelForSchoolRegistrationOfficer(userId, ct);
                if (schoolInfo == null)
                {
                    return NotFound(new { message = "School record was not found for the current user." });
                }

                string proprietorCode = string.Empty;
                try
                {
                    var school = await ResolveRegistrySchoolWithAgentAsync(ct);
                    proprietorCode = school?.SchoolRegistrationOfficer?.AgentUser?.SchoolRegId ?? string.Empty;
                }
                catch (Exception agentEx)
                {
                    _logger.LogWarning(agentEx, "Unable to load proprietor agent details for school registration profile");
                }

                return Ok(new
                {
                    proprietorCode,
                    schoolInfo,
                    logoUrl = BuildSchoolLogoUrl(schoolInfo.SchoolLogoString),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving school registration profile");
                return StatusCode(500, new { message = "An error occurred while retrieving school registration profile." });
            }
        }

        [HttpPut("school/registration")]
        public async Task<IActionResult> UpdateSchoolRegistration([FromForm] SchoolRegistrationUpdateRequest request, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                if (string.IsNullOrWhiteSpace(request.SchoolName))
                {
                    return BadRequest(new { message = "School name is required." });
                }

                var school = await ResolveRegistrySchoolEntityForUpdateAsync(ct);
                if (school == null)
                {
                    return NotFound(new { message = "School record was not found." });
                }

                if (request.SchoolLogo != null)
                {
                    if (request.SchoolLogo.Length > 16 * 1024)
                    {
                        return BadRequest(new { message = "Your school logo size is too big. The maximum size is 16kb." });
                    }

                    var contentType = request.SchoolLogo.ContentType?.ToLowerInvariant() ?? string.Empty;
                    if (contentType is not ("image/jpeg" or "image/png"))
                    {
                        return BadRequest(new { message = "Only 'JPEG' and 'png' image are allowed." });
                    }

                    school.SchoolLogoString = await SaveSchoolLogoAsync(request.SchoolLogo, request.SchoolLogoStringOld, ct);
                }
                else if (!string.IsNullOrWhiteSpace(request.SchoolLogoStringOld))
                {
                    school.SchoolLogoString = request.SchoolLogoStringOld.Trim();
                }

                school.SchoolName = request.SchoolName.Trim();
                school.SchoolAddress = request.SchoolAddress?.Trim() ?? string.Empty;
                school.Telephone = request.Telephone?.Trim() ?? string.Empty;
                school.PoAddress = request.PoAddress?.Trim() ?? string.Empty;
                school.EmailAddress = request.EmailAddress?.Trim() ?? string.Empty;
                school.Website = request.Website?.Trim() ?? string.Empty;
                school.Motto = request.Motto?.Trim() ?? string.Empty;

                _unitOfWork.Repository<SchoolTyInfo, string>().Update(school);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { message = "School registration updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating school registration profile");
                return StatusCode(500, new { message = "An error occurred while updating school registration profile." });
            }
        }

        [HttpGet("sms-balance")]
        public async Task<IActionResult> GetSmsBalance(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                var balance = await _registrySmsService.GetSmsBalanceAsync(userId, ct);
                return Ok(new { balance });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "SMS balance unavailable for current user");
                return Ok(new { balance = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SMS balance");
                return StatusCode(500, new { message = "An error occurred while retrieving SMS balance." });
            }
        }

        [HttpPost("send-sms")]
        public async Task<IActionResult> SendMultipleSms([FromBody] SmsInfoDto smsInfo, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _registrySmsService.SendMultipleSmsAsync(userId, smsInfo, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS");
                return StatusCode(500, new { message = "An error occurred while sending SMS." });
            }
        }

        [HttpPost("send-sms-express")]
        public async Task<IActionResult> SendMultipleSmsExpress([FromBody] SmsInfoDto smsInfo, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                var message = await _registrySmsService.SendMultipleSmsExpressAsync(userId, smsInfo, ct);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending express SMS");
                return StatusCode(500, new { message = "An error occurred while sending express SMS." });
            }
        }

        [HttpGet("update-sms-credentials")]
        public async Task<IActionResult> UpdateSmsCredentials(CancellationToken ct)
        {
            try
            {
                var message = await _registrySmsService.UpdateSmsCredentialsAsync(ct);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SMS credentials");
                return StatusCode(500, new { message = "An error occurred while updating SMS credentials." });
            }
        }

        [HttpGet("system-migration")]
        public async Task<IActionResult> NewSystemMigration(CancellationToken ct)
        {
            if (!InternetConnectivityHelper.CheckForInternetConnection())
            {
                return Ok(new { message = "Failed! Internet is required to enable your system." });
            }

            var rightHostName = await TheRightNaitsHostName(ct);
            if (string.IsNullOrEmpty(rightHostName))
            {
                return Ok(new { message = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!" });
            }

            try
            {
                string userId = GetCurrentUserId();
                var credentialEncoded = Uri.EscapeDataString(
                    await _securityExtensionService.GetAndEncodeCredentials(userId,ct));
                var response = await GetRemoteString(
                    rightHostName + "/home/NewSystemMigration?credentialEncoded=" + credentialEncoded,
                    ct);

                if (string.IsNullOrEmpty(response))
                {
                    return Ok(new { message = "Failed! Unable to verify your credentials." });
                }

                var responseStatus = response.Replace("\"", string.Empty).Trim();
                if (responseStatus == "000")
                {
                    return Ok(new { message = "Failed! unable to verify your credentials. Contact NAITS-ASMS Admin." });
                }

                await UpdateMachineInfo(responseStatus, ct);
                return Ok(new { message = "Good! Your System has successfully been enabled." });
            }
            catch (Exception ex)
            {
                return Ok(new { message = "Failed! " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        private async Task<SchoolTyInfo?> ResolveRegistrySchoolWithAgentAsync(CancellationToken ct)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var schoolRepo = _unitOfWork.Repository<SchoolTyInfo, string>();
            return await schoolRepo.FindAsync(
                entry => entry.SchoolRegistrationOfficerId == schoolId,
                ct,
                entry => entry.SchoolRegistrationOfficer!,
                entry => entry.SchoolRegistrationOfficer!.AgentUser!)
                ?? await schoolRepo.FindAsync(entry => entry.SchoolRegistrationOfficerId == schoolId, ct);
        }

        private static string? BuildSchoolLogoUrl(string? logoFileName)
        {
            if (string.IsNullOrWhiteSpace(logoFileName))
            {
                return null;
            }

            return $"/school-logos/{logoFileName.Trim()}";
        }

        private async Task<string> SaveSchoolLogoAsync(IFormFile logoFile, string? existingLogo, CancellationToken ct)
        {
            var extension = logoFile.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
            var fileName = $"{Guid.NewGuid():N}{extension}";
            if (fileName.Length > 40)
            {
                fileName = $"{Guid.NewGuid():N}.png";
            }

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var folder = Path.Combine(webRoot, "school-logos");
            Directory.CreateDirectory(folder);

            if (!string.IsNullOrWhiteSpace(existingLogo))
            {
                var oldPath = Path.Combine(folder, existingLogo.Trim());
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            var filePath = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(filePath);
            await logoFile.CopyToAsync(stream, ct);
            return fileName;
        }

        [HttpGet("sms-system-info")]
        public async Task<IActionResult> GetSmsSystemInfo(CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "Unable to resolve the current user." });
                }

                var systemInfo = await _securityExtensionService.GetSystemInfoForSms(userId, ct);
                return Ok(systemInfo);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "SMS system info unavailable for current user");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SMS system info");
                return StatusCode(500, new { message = "An error occurred while retrieving SMS system info." });
            }
        }


        public sealed class SchoolRegistrationUpdateRequest
        {
            [Required]
            [StringLength(70)]
            public string SchoolName { get; set; } = string.Empty;

            [StringLength(150)]
            public string? SchoolAddress { get; set; }

            [StringLength(50)]
            public string? Telephone { get; set; }

            [StringLength(100)]
            public string? PoAddress { get; set; }

            [StringLength(50)]
            public string? EmailAddress { get; set; }

            [StringLength(70)]
            public string? Website { get; set; }

            [StringLength(70)]
            public string? Motto { get; set; }

            public string? SchoolLogoStringOld { get; set; }

            public IFormFile? SchoolLogo { get; set; }
        }

        public sealed class SchoolHeadUpsertRequest
        {
            [Required]
            [StringLength(40)]
            public string HeadsName { get; set; } = string.Empty;

            [StringLength(15)]
            public string PhoneNumber { get; set; } = string.Empty;

            // Accept image data-url/base64 string from the UI file picker.
            public string SignatureImageString { get; set; } = string.Empty;

            public TimeStatus Status { get; set; }

            public byte SchoolHeadCategoryId { get; set; }

            public HeadStatus HeadStatus { get; set; }

            public Gender Gender { get; set; }
        }
    }
}

