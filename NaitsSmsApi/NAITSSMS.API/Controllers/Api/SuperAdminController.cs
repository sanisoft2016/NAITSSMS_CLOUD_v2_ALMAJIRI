using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Infrastructure.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.API.Controllers.Api
{
    /// <summary>
    /// Super Admin (SAOfficer) APIs — mirrors legacy SupperAdmin/SadminController SchoolList flow.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "SAOfficer")]
    public class SuperAdminController : ControllerBase
    {
        private readonly IRegistrationService _registrationService;
        private readonly ITeachersAndOthersService _teachersAndOthersService;
        private readonly ILogger<SuperAdminController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _services;

        public SuperAdminController(
            IRegistrationService registrationService,
            ITeachersAndOthersService teachersAndOthersService,
            ILogger<SuperAdminController> logger,
            IConfiguration configuration,
            IServiceProvider services)
        {
            _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
            _teachersAndOthersService = teachersAndOthersService ?? throw new ArgumentNullException(nameof(teachersAndOthersService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Seeds the Almajiri programme demo dataset. Idempotent, and disabled unless
        /// <c>Demo:AllowAlmajiriSeed</c> (or <c>DEMO__ALLOWALMAJIRISEED</c>) is true.
        /// </summary>
        [HttpPost("demo/seed-almajiri")]
        public async Task<IActionResult> SeedAlmajiriDemo(CancellationToken ct)
        {
            if (!_configuration.GetValue<bool>("Demo:AllowAlmajiriSeed"))
            {
                return StatusCode(403, new { message = "Demo seeding is disabled on this environment." });
            }

            try
            {
                var report = await AlmajiriDemoSeeder.SeedAsync(_services, ct);
                _logger.LogInformation("Almajiri demo seed complete: {Message}", report.Message);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding the Almajiri demo dataset");
                return StatusCode(500, new { message = "An error occurred while seeding the demo dataset.", detail = ex.Message });
            }
        }

        /// <summary>Platform-wide school, access, accountant, and subscription overview.</summary>
        [HttpGet("dashboard/summary")]
        public async Task<IActionResult> DashboardSummary(CancellationToken ct)
        {
            try
            {
                return Ok(await _registrationService.GetSuperAdminDashboard(ct));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading the super-admin dashboard");
                return StatusCode(500, new { message = "An error occurred while loading the dashboard." });
            }
        }

        /// <summary>Legacy SadminController.SchoolList → GetSchoolListForSuperAdmin</summary>
        [HttpGet("get-school-list-for-system-admin")]
        public async Task<IActionResult> SchoolList(CancellationToken ct)
        {
            try
            {
                var result = await _registrationService.GetSchoolListForSuperAdmin(ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading super-admin school list");
                return StatusCode(500, new { message = "An error occurred while loading the school list." });
            }
        }

        /// <summary>Legacy SadminController.SearchComplex → SearchSchoolByCertainParameter → SchoolInfoDto[]</summary>
        [HttpGet("search-school-info")]
        public async Task<IActionResult> SearchComplex(
            [FromQuery] string searchParameter,
            [FromQuery] int searchStatus,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchParameter))
                {
                    return BadRequest(new { message = "Search Parameter must be provided to continue." });
                }

                var result = await _registrationService.SearchSchoolByCertainParameter(
                    searchParameter, searchStatus, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching schools");
                return StatusCode(500, new { message = "An error occurred while searching schools." });
            }
        }

        /// <summary>Legacy SadminController.EnableRegistrarAccount → SchoolInfoDto[]</summary>
        [HttpGet("enable-school-users-by-registrar-id/{userId}")]
        public async Task<IActionResult> EnableRegistrarAccount(string userId, CancellationToken ct)
        {
            try
            {
                var status = await _registrationService.EnableSchoolUsersBySchoolRegistrarId(userId, ct);
                if (!status)
                {
                    return Ok(Array.Empty<object>());
                }

                var list = await _registrationService.GetAllRegistrarForSuperAdmin(ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling registrar {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while enabling the registrar." });
            }
        }

        /// <summary>Legacy SadminController.DisableRegistrarAccount → SchoolInfoDto[]</summary>
        [HttpGet("disable-school-users-by-registrar-id/{userId}")]
        public async Task<IActionResult> DisableRegistrarAccount(string userId, CancellationToken ct)
        {
            try
            {
                var status = await _registrationService.DisableSchoolUsersBySchoolRegistrarId(userId, ct);
                if (!status)
                {
                    return Ok(Array.Empty<object>());
                }

                var list = await _registrationService.GetAllRegistrarForSuperAdmin(ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling registrar {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while disabling the registrar." });
            }
        }

        /// <summary>
        /// Legacy SadminController.SystemImplementationType — returns bare bool like legacy Json(true/false).
        /// </summary>
        [HttpGet("change-system-implementation-type")]
        public async Task<IActionResult> SystemImplementationType(
            [FromQuery] string conCatParameters,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(conCatParameters))
                {
                    return Ok(false);
                }

                var status = await _teachersAndOthersService.ChangeSystemImplementationType(conCatParameters, ct);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system implementation type");
                return Ok(false);
            }
        }

        /// <summary>Legacy SadminController.AgentDetails → AgentInfoDto</summary>
        [HttpGet("get-agent-info-byId/{agentUserId}")]
        public async Task<IActionResult> AgentDetails(
            string agentUserId,
            [FromQuery] string? schoolName,
            CancellationToken ct)
        {
            try
            {
                var details = await _registrationService.GetAgentInfoById(agentUserId, ct);
                return Ok(new { schoolName = schoolName ?? string.Empty, agent = details });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading agent details {AgentUserId}", agentUserId);
                return StatusCode(500, new { message = "An error occurred while loading agent details." });
            }
        }

        /// <summary>Legacy SadminController.SchoolRegistrarDetails → SchoolInfoDto</summary>
        [HttpGet("get-school-details-byId/{userId}")]
        public async Task<IActionResult> SchoolRegistrarDetails(
            string userId,
            [FromQuery] string? fullName,
            CancellationToken ct)
        {
            try
            {
                var model = await _registrationService.GetSchoolRegistrarDetailsById(userId, ct);
                if (model == null)
                {
                    return NotFound(new { message = "School details not found." });
                }

                return Ok(new { fullName = fullName ?? string.Empty, school = model });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading school registrar details {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while loading school details." });
            }
        }

        /// <summary>
        /// Legacy RegistrationController.DeleteSchoolRegistrar — returns status byte 0..4.
        /// </summary>
        [HttpPost("delete-school-registrar-byId/{userId}")]
        public async Task<IActionResult> DeleteSchoolRegistrar(string userId, CancellationToken ct)
        {
            try
            {
                var status = await _registrationService.DeleteSchoolRegistrar(userId, ct);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting school registrar {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while deleting the registrar." });
            }
        }

        /// <summary>Legacy SadminController.AdminAccountantDetails → EmployeeDto</summary>
        [HttpGet("get-admin-accountant-details-by-schoolId/{schoolId}")]
        public async Task<IActionResult> AdminAccountantDetails(string schoolId, CancellationToken ct)
        {
            try
            {
                var details = await _registrationService.GetAdminAccountantDetails(schoolId, ct);
                if (details == null)
                {
                    return NotFound(new { message = "Admin accountant was not found for this school." });
                }

                return Ok(details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin accountant for school {SchoolId}", schoolId);
                return StatusCode(500, new { message = "An error occurred while loading accountant details." });
            }
        }

        /// <summary>Legacy SadminController.EnableAccountantAccount</summary>
        [HttpGet("enable-accountant-user-byId/{userId}")]
        public async Task<IActionResult> EnableAccountantAccount(string userId, CancellationToken ct)
        {
            var status = await _registrationService.EnableAccountantUserById(userId, ct);
            return Ok(status);
        }

        /// <summary>Legacy SadminController.DisableAccountantAccount</summary>
        [HttpGet("disable-accountant-user-byId/{userId}")]
        public async Task<IActionResult> DisableAccountantAccount(string userId, CancellationToken ct)
        {
            var status = await _registrationService.DisableAccountantUserById(userId, ct);
            return Ok(status);
        }

        /// <summary>Legacy RegistrationController.DeleteAllAccountants</summary>
        [HttpPost("delete-all-accountants-byId/{employeeId}")]
        public async Task<IActionResult> DeleteAllAccountants(string employeeId, CancellationToken ct)
        {
            try
            {
                var status = await _registrationService.DeleteAllAccountants(employeeId, ct);
                return Ok(!string.IsNullOrEmpty(status));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting accountants for employee {EmployeeId}", employeeId);
                return Ok(false);
            }
        }

        /// <summary>Legacy SadminController.EditSchoolRegistrar GET → GetSchoolRegistrarById</summary>
        [HttpGet("get-school-registrar-byId/{userId}")]
        public async Task<IActionResult> GetSchoolRegistrarById(string userId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return BadRequest(new { message = "User id is required." });
                }

                var model = await _registrationService.GetSchoolRegistrarById(userId, ct);
                if (model == null)
                {
                    return NotFound(new { message = "School registrar was not found." });
                }

                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading school registrar {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while loading the school registrar." });
            }
        }

        /// <summary>Legacy SadminController.EditSchoolRegistrar POST → UpdateSchoolRegistrarInfo</summary>
        [HttpPost("update-school-registrar-info")]
        public async Task<IActionResult> UpdateSchoolRegistrarInfo(
            [FromBody] EditSchoolRegistrarDto model,
            CancellationToken ct)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Id))
                {
                    return Ok(new UpdateSchoolRegistrarResultDto
                    {
                        Status = 2,
                        Success = false,
                        Message = "Failed! Unable to complete the process.",
                    });
                }

                if (!string.IsNullOrEmpty(model.Password)
                    && !string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
                {
                    return Ok(new UpdateSchoolRegistrarResultDto
                    {
                        Status = 255,
                        Success = false,
                        Message = "Password mis-matched.",
                    });
                }

                var hostName = Request.Host.Host;
                var status = await _registrationService.UpdateSchoolRegistrarInfo(model, hostName, ct);

                return Ok(status switch
                {
                    0 => new UpdateSchoolRegistrarResultDto
                    {
                        Status = 0,
                        Success = true,
                        Message = "School Registrar details were updated successfully.",
                        RedirectPath = "/supper-admin/school-list",
                    },
                    1 => new UpdateSchoolRegistrarResultDto
                    {
                        Status = 1,
                        Success = false,
                        Message = "Failed! Email exist against another record.",
                    },
                    2 => new UpdateSchoolRegistrarResultDto
                    {
                        Status = 2,
                        Success = false,
                        Message = "Failed! Username exist against another user.",
                    },
                    3 => new UpdateSchoolRegistrarResultDto
                    {
                        Status = 3,
                        Success = false,
                        Message = "Failed! The Token you have entered has already been used on another School.",
                    },
                    _ => new UpdateSchoolRegistrarResultDto
                    {
                        Status = status,
                        Success = false,
                        Message = "Failed! Unable to complete the process.",
                    },
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating school registrar {UserId}", model?.Id);
                return Ok(new UpdateSchoolRegistrarResultDto
                {
                    Status = 255,
                    Success = false,
                    Message = "Failed! Unable to complete the process.",
                });
            }
        }

        /// <summary>Legacy SadminController.LicensingIndex GET</summary>
        [HttpGet("get-licensing-index-page/{schoolId}")]
        public async Task<IActionResult> GetLicensingIndexPage(string schoolId, CancellationToken ct)
        {
            try
            {
                var page = await _registrationService.GetLicensingIndexPage(schoolId, ct);
                if (page == null)
                {
                    return NotFound(new { message = "School was not found for licensing." });
                }

                return Ok(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading licensing index for school {SchoolId}", schoolId);
                return StatusCode(500, new { message = "An error occurred while loading the licensing page." });
            }
        }

        /// <summary>Legacy SadminController.LoadLastSubscriptionInfo</summary>
        [HttpGet("get-last-subscription-info/{schoolId}")]
        public async Task<IActionResult> GetLastSubscriptionInfo(string schoolId, CancellationToken ct)
        {
            try
            {
                var info = await _registrationService.GetLastSubscriptionInfo(schoolId, ct);
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading last subscription for school {SchoolId}", schoolId);
                return StatusCode(500, new { message = "An error occurred while loading last subscription info." });
            }
        }

        /// <summary>Legacy SadminController.SubscriptionHistory</summary>
        [HttpGet("get-subscription-history/{schoolId}")]
        public async Task<IActionResult> GetSubscriptionHistory(string schoolId, CancellationToken ct)
        {
            try
            {
                var history = await _registrationService.GetSubscriptionHistory(schoolId, ct);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading subscription history for school {SchoolId}", schoolId);
                return StatusCode(500, new { message = "An error occurred while loading subscription history." });
            }
        }

        /// <summary>Legacy SadminController.CreateSubscriptionKey POST</summary>
        [HttpPost("create-subscription-key")]
        public async Task<IActionResult> CreateSubscriptionKey(
            [FromBody] CreateSubscriptionKeyRequestDto model,
            CancellationToken ct)
        {
            try
            {
                if (model == null
                    || string.IsNullOrWhiteSpace(model.SchoolSmsId)
                    || model.SubscriptionId <= 0
                    || model.Charges <= 0)
                {
                    return Ok(new CreateSubscriptionKeyResultDto
                    {
                        Success = false,
                        Message = "Failed! Unable to Complete the process.",
                    });
                }

                var keyResult = await _registrationService.GetSubscriptionKey(model, ct);
                if (string.IsNullOrWhiteSpace(keyResult))
                {
                    return Ok(new CreateSubscriptionKeyResultDto
                    {
                        Success = false,
                        Message =
                            "Failed! Unable to Complete the process. Check Subscription Id (7 or 9 digits), school id, and that an Agent exists for this school.",
                    });
                }

                var regKey = keyResult.Split(',')[0];
                var html =
                    $"The Registration Key is: <strong style=\"color:blue\">{regKey}</strong>, <br /> Expiry Date is: <strong>{model.ExpiryDate.ToLongDateString()}</strong>";

                return Ok(new CreateSubscriptionKeyResultDto
                {
                    Success = true,
                    HtmlMessage = html,
                    Message = $"The Registration Key is: {regKey}. Expiry Date is: {model.ExpiryDate.ToLongDateString()}",
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription key for school {SchoolId}", model?.SchoolSmsId);
                var detail = ex.InnerException?.Message ?? ex.Message;
                return Ok(new CreateSubscriptionKeyResultDto
                {
                    Success = false,
                    Message = "Failed! Unable to Complete the process. " + detail,
                });
            }
        }

        /// <summary>Legacy SadminController.UpdateFeatureImplementationType</summary>
        [HttpGet("update-feature-implementation-type")]
        public async Task<IActionResult> UpdateFeatureImplementationType(
            [FromQuery] byte implemetationOption,
            [FromQuery] string schoolId,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(schoolId))
                {
                    return Ok(false);
                }

                var status = await _teachersAndOthersService.UpdateFeatureImplementationType(
                    implemetationOption,
                    schoolId,
                    ct);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating feature implementation type for school {SchoolId}", schoolId);
                return Ok(false);
            }
        }

        /// <summary>Legacy SadminController.UpdateNumberOfStudentForm POST</summary>
        [HttpPost("update-number-of-students")]
        public async Task<IActionResult> UpdateNumberOfStudents(
            [FromBody] UpdateNumberOfStudentsRequestDto model,
            CancellationToken ct)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.SchoolId) || model.NoOfStudents <= 0)
                {
                    return Ok(new UpdateNumberOfStudentsResultDto
                    {
                        Success = false,
                        FailedSentinel = true,
                        Message = "Failed! Unable to Complete the process.",
                    });
                }

                var message = await _registrationService.UpdateNumberOfStudentsForLicensing(
                    model.SchoolId,
                    model.NoOfStudents,
                    ct);

                if (string.IsNullOrWhiteSpace(message))
                {
                    return Ok(new UpdateNumberOfStudentsResultDto
                    {
                        Success = false,
                        FailedSentinel = true,
                        Message = "Failed! Unable to Complete the process.",
                    });
                }

                return Ok(new UpdateNumberOfStudentsResultDto
                {
                    Success = true,
                    FailedSentinel = false,
                    Message = message,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student count for school {SchoolId}", model?.SchoolId);
                return Ok(new UpdateNumberOfStudentsResultDto
                {
                    Success = false,
                    FailedSentinel = true,
                    Message = "Failed! Unable to Complete the process.",
                });
            }
        }
    }
}
