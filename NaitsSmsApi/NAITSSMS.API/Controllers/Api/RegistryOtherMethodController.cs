using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAITSSMS.API.Helpers;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Infrastructure.Data.Security;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.API.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RegistryOtherMethodController : ControllerBase
    {
        private readonly IRegistryOtherMethodService _registryOtherMethodService;
        private readonly ISecurityService _securityService;
        private readonly IRegistrationService _registrationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RegistryOtherMethodController> _logger;

        public RegistryOtherMethodController(
            IRegistryOtherMethodService registryOtherMethodService,
            ISecurityService securityService,
            IRegistrationService registrationService,
            IUnitOfWork unitOfWork,
            ILogger<RegistryOtherMethodController> logger)
        {
            _registryOtherMethodService = registryOtherMethodService ?? throw new ArgumentNullException(nameof(registryOtherMethodService));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("system/migration")]
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
                var credentialEncoded = Uri.EscapeDataString(
                    await _registryOtherMethodService.GetAndEncodeCredentials(ct));
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

                var systemInfo = await _registryOtherMethodService.GetSystemInfoForSms(ct);
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

        private static object MapLegacyRedirect(string? action, string? controller, string? area)
        {
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

        private string? GetCurrentUserId()
        {
            return User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.PrimarySid)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value
                ?? User.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
        }

        private async Task UpdateMachineInfo(string response, CancellationToken ct)
        {
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

        private async Task<SchoolTyInfo?> ResolveRegistrySchool(CancellationToken ct)
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
                catch
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
    }
}
