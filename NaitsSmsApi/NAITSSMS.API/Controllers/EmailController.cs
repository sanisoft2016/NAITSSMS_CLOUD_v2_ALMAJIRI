using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.API.Helpers;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using System.Security.Claims;

namespace NAITSSMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmailController : ControllerBase
    {
        private readonly IEmailServiceExtension _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EmailController> _logger;

        public EmailController(
            IEmailServiceExtension emailService,
            IWebHostEnvironment environment,
            ILogger<EmailController> logger)
        {
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string? GetCurrentUserId()
        {
            return User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.PrimarySid)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
        }

        [HttpGet("post-sms-system-information")]
        public async Task<IActionResult> PostSystemInformation(CancellationToken ct)
        {
            try
            {
                if (!InternetConnectivityHelper.CheckForInternetConnection())
                {
                    return Ok(new { message = "Internet Connection is required in order to submit the request." });
                }

                return Ok(await _emailService.PostSystemInformation(GetCurrentUserId() ?? string.Empty, ct));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting SMS system information");
                return Ok(new { message = "An error occurred while posting SMS system information." });
            }
        }

        /// <summary>
        /// Public contact-us enquiry (legacy Home/Contact). Sends mail to the configured NAITS inbox.
        /// </summary>
        [HttpPost("contact-enquiry")]
        [AllowAnonymous]
        public async Task<IActionResult> ContactEnquiry([FromBody] ContactUsDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                if (!InternetConnectivityHelper.CheckForInternetConnection())
                {
                    return BadRequest(new
                    {
                        status = "Failure",
                        message = "Internet Connection is required in order to submit the request.",
                    });
                }

                var result = await _emailService.SendContactEnquiry(model, ct);
                if (string.Equals(result.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new { status = "Success", message = result.Message });
                }

                return BadRequest(new { status = "Failure", message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending contact enquiry from {Email}", model.Email);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    status = "Failure",
                    message =
                        "Unable to complete the process. Kindly mail info@naits.com.ng or call 08035550938.",
                });
            }
        }
    }
}
