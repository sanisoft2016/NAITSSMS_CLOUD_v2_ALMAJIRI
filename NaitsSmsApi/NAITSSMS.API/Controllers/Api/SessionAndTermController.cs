using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Claims;

namespace NAITSSMS.API.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionAndTermController : ControllerBase
    {
        private readonly ISessionAndTermService _SessionAndTermService;
        private readonly IRegistrationService _registrationService;
        private readonly ILogger<SessionAndTermController> _logger;

        private readonly ISecurityService _securityService;

        public SessionAndTermController(
            ISessionAndTermService sessionAndTermService,
            IRegistrationService registrationService,
            ILogger<SessionAndTermController> logger,
            ISecurityService securityService)
        {
            _SessionAndTermService = sessionAndTermService ?? throw new ArgumentNullException(nameof(sessionAndTermService));
            _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }


        private string GetUserId()
        {
            var claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            var userId = claimsIdentity?.FindFirst(ClaimTypes.PrimarySid)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("User is not authenticated");

            return userId;
        }

        [HttpGet("get-sessions")]
        public async Task<IActionResult> GetAcademicSessions(CancellationToken ct)
        {
            try
            {
                 var userId = GetUserId();
                var sessions = await _SessionAndTermService.GetAcademicSessions(userId, ct);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving academic sessions");
                return StatusCode(500, new { message = "An error occurred while retrieving academic sessions" });
            }
        }

        /// <summary>
        /// Legacy-style sessions dropdown for result pages.
        /// </summary>
        [HttpGet("dropdown/sessions")]
        public async Task<IActionResult> GetSessionsDropdown(CancellationToken ct)
        {
            try
            {
                var userId = GetUserId();
                var sessions = await _SessionAndTermService.GetSessionsDropdown(userId, ct);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions dropdown");
                return StatusCode(500, new { message = "An error occurred while retrieving sessions dropdown" });
            }
        }

        /// <summary>
        /// Legacy-style terms dropdown for a selected session.
        /// </summary>
        [HttpGet("dropdown/sessions/{sessionId}/terms")]
        public async Task<IActionResult> GetTermsDropdownForSession(string sessionId, CancellationToken ct)
        {
            try
            {
                var terms = await _SessionAndTermService.GetTermsDropdownForSession(sessionId, ct);
                return Ok(terms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving terms dropdown for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while retrieving terms dropdown" });
            }
        }

        /// <summary>
        /// Get an academic session by id
        /// Descriptive alias for session lookup
        /// </summary>
        [HttpGet("get-session-by-id/{id}")]
        public async Task<IActionResult> GetAcademicSessionById(string id, CancellationToken ct)
        {
            try
            {
                var userId = GetUserId();
                var sessions = await _SessionAndTermService.GetAcademicSessions(userId, ct);
                var session = sessions.Sessions?.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

                if (session == null)
                    return NotFound(new { message = "Academic session not found" });

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving academic session {Id}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving academic session" });
            }
        }

        /// <summary>
        /// Create academic session
        /// Legacy: CreateAcademicSession(AcademicSessionDto model)
        /// </summary>
        [HttpPost("create-session")]
        public async Task<IActionResult> CreateAcademicSession(
            [FromBody] AcademicSessionDto model,
            CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);
                var userId = GetUserId();
                var result = await _SessionAndTermService.CreateAcademicSession(userId, model, ct);

                if (result)
                {
                    return Ok(new { message = "Academic session created successfully" });
                }

                return BadRequest(new { message = "Failed to create academic session" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating academic session");
                return StatusCode(500, new { message = "An error occurred while creating academic session" });
            }
        }

        /// <summary>
        /// Update academic session
        /// Legacy: EditSession(AcademicSessionDto model)
        /// </summary>
        [HttpPut("update-session/{id}")]
        public async Task<IActionResult> UpdateAcademicSession(
            string id,
            [FromBody] AcademicSessionDto model,
            CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                model.Id = id;
                var userId = GetUserId();
                var result = await _SessionAndTermService.UpdateAcademicSessionInfo(userId, model, ct);

                if (result)
                {
                    return Ok(new { message = "Academic session updated successfully" });
                }

                return BadRequest(new { message = "Academic session was not found or the session name already exists for this school." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating academic session {Id}", id);
                return StatusCode(500, new { message = "An error occurred while updating academic session" });
            }
        }

        /// <summary>
        /// Delete academic session
        /// Legacy: DeleteAcademicSession(byte id)
        /// </summary>
        [HttpDelete("delete-session/{id}")]
        public async Task<IActionResult> DeleteAcademicSession(string id, CancellationToken ct)
        {
            try
            {
                var result = await _SessionAndTermService.DeleteAcademicSession(id, ct);

                if (result)
                {
                    return Ok(new { message = "Academic session deleted successfully" });
                }

                return NotFound(new { message = "Academic session not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting academic session {Id}", id);
                return StatusCode(500, new { message = "An error occurred while deleting academic session" });
            }
        }

        /// <summary>
        /// Get terms for a session
        /// Legacy: TermDropdownForSelectedSession(int selectedSessionId)
        /// </summary>
        [HttpGet("sessions/{sessionId}/terms")]
        public async Task<IActionResult> GetTermsForSession(string sessionId, CancellationToken ct)
        {
            try
            {
                var terms = await _SessionAndTermService.GetTermsBySession(sessionId, ct);
                return Ok(terms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving terms for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while retrieving terms" });
            }
        }

        /// <summary>
        /// Initial whole-school student assessment setup.
        /// Legacy: StudentAccessmentSystemInitialPreparationPost(TermStatus term)
        /// </summary>
        [HttpPost("student-assessment-system-initial-preparation")]
        public async Task<IActionResult> StudentAssessmentSystemInitialPreparation(
            [FromQuery] TermStatus term,
            CancellationToken ct = default)
        {
            try
            {
                var bStatus = await _securityService.B(ct);

                if (bStatus.AccessVerifyReturnValues != null)
                {
                    _logger.LogWarning(
                        "Student assessment system initial preparation blocked by security redirect to {Controller}/{Action}",
                        bStatus.AccessVerifyReturnValues.Value3,
                        bStatus.AccessVerifyReturnValues.Value2);

                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message = bStatus.AccessVerifyReturnValues.Value1,
                        redirect = new
                        {
                            action = bStatus.AccessVerifyReturnValues.Value2,
                            controller = bStatus.AccessVerifyReturnValues.Value3,
                            area = bStatus.AccessVerifyReturnValues.Value4
                        }
                    });
                }
                // http://localhost:53225
                var baseUrl = $"{Request.Scheme}://{Request.Host.Value}";

                if (!string.Equals(bStatus.LoginReturnStatus?.ReturnMessage, "1", StringComparison.Ordinal) && !baseUrl.Contains(":"))
                {
                    var message = bStatus.LoginReturnStatus?.ReturnMessage ?? "Access verification failed.";

                    _logger.LogWarning(
                        "Student assessment system initial preparation denied by security service: {Message}",
                        message);

                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message,
                        data = bStatus.LoginReturnStatus
                    });
                }

                var userId = GetUserId();

                if (!await HasInternetConnectionAsync(ct))
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        message = "Sorry! Internet Connection is required in order to prepare result processing environment."
                    });
                }

                _logger.LogInformation(
                    "Running student assessment system initial preparation for user {UserId} and term {Term}",
                    userId,
                    term);

                var status = await _registrationService.DoSystemInitialPreparationForStudentAccessment(userId, term, ct);
                if (!string.Equals(status?.StatusCode, "Succeed", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message = status?.StatusCode ?? "Failed to prepare student assessment environment.",
                        data = status
                    });
                }

                var successfulStatus = status!;
                var studentList = (successfulStatus.TotalStudentListToPdf ?? Array.Empty<TotalStudentListToPdfDto>())
                    .OrderBy(d => d.CurrentClass)
                    .ThenBy(f => f.Arm)
                    .ToList();

                var total = successfulStatus.TotalStudentListToPdf?.Count;

                if (total != null && total <= 0)
                {
                    var checkConnectivition = await HasInternetConnectionAsync(ct);
                    var msg = _securityService.ReturnMessage1();

                    if (checkConnectivition)
                    {
                        try
                        {
                            _ = _securityService.GetPv();

                            if (studentList.Any())
                            {
                                return Ok(new
                                {
                                    message = "Students assessment environment has successfully been prepared.",
                                    data = new
                                    {
                                        successfulStatus.StatusCode,
                                        totalStudents = total,
                                        students = studentList
                                    }
                                });
                            }

                            return Ok(new
                            {
                                message = "Students assessment environment has successfully been prepared. You can now start adding student.",
                                data = new
                                {
                                    successfulStatus.StatusCode,
                                    totalStudents = total,
                                    students = studentList
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Legacy post-preparation notification branch failed");

                            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                            {
                                message = msg,
                                data = new
                                {
                                    successfulStatus.StatusCode,
                                    totalStudents = total,
                                    students = studentList
                                }
                            });
                        }
                    }

                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        message = msg,
                        data = new
                        {
                            successfulStatus.StatusCode,
                            totalStudents = total,
                            students = studentList
                        }
                    });
                }

                return Ok(new
                {
                    message = "Successful.",
                    data = new
                    {
                        successfulStatus.StatusCode,
                        totalStudents = total ?? studentList.Count,
                        students = studentList
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized request for student assessment system initial preparation");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running student assessment system initial preparation");
                return StatusCode(500, new { message = "An error occurred while preparing the student assessment environment" });
            }
        }

        /// <summary>
        /// Activate a term
        /// Legacy: ActivateTerm(string termId)
        /// </summary>

        /// <summary>
        /// Prepare initial assessment environment for the selected term.
        /// Alias for the legacy whole-school assessment preparation flow.
        /// </summary>
        [HttpPost("prepare-intitial-assessment")]
        public async Task<IActionResult> PrepareInitialAssessment(
            [FromQuery] byte? requestStatus = null,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Preparing initial assessment environment for whole school");
                var userId = GetUserId();
                var result = await _SessionAndTermService.PrepareAssessmentEnvironmentForTheWholeStudents(userId, requestStatus, ct);

                if (result != null)
                {
                    if (requestStatus == 1)
                    {
                        return Ok(new
                        {
                            message = result.Message ?? string.Empty,
                            teachers = result.UncompletedFormMastersReport ?? new List<UncompletedFormMastersReportDto>(),
                            data = result,
                        });
                    }

                    return Ok(new { message = "Assessment environment prepared successfully", data = result });
                }

                return BadRequest(new { message = "Failed to prepare assessment environment" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing initial assessment environment");
                return StatusCode(500, new { message = "An error occurred while preparing assessment environment" });
            }
        }

        private static async Task<bool> HasInternetConnectionAsync(CancellationToken ct)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("google.com", 1000);
                if (reply.Status == IPStatus.Success)
                {
                    return true;
                }
            }
            catch
            {
                // Fall through to the HTTP probe.
            }

            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(3)
                };

                using var response = await httpClient.GetAsync(
                    "http://www.google.com",
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }


    }
}
