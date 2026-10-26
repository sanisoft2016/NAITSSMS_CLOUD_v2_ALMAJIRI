using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.Application.IService;

namespace CustRewardMgtSys.API.Controllers
{
    /// <summary>
    /// Legacy MVC HomeController SMS / subscription gateway routes
    /// (<c>/home/SmsBalance</c>, <c>/home/UpdateSmsBalance</c>, …) for callers that still use those URLs.
    /// </summary>
    [Route("home")]
    [ApiController]
    [AllowAnonymous]
    public class HomeController : ControllerBase
    {
        private readonly IRegistrationService _registrationService;

        public HomeController(IRegistrationService registrationService)
        {
            _registrationService = registrationService;
        }

        [HttpGet("SmsBalance")]
        public async Task<IActionResult> SmsBalance([FromQuery] string credentialEncoded, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credentialEncoded))
            {
                return Ok("000");
            }

            try
            {
                var credentialDecoded = _registrationService.DecodeCredentials(credentialEncoded);
                var parts = credentialDecoded.Split(':');
                if (parts.Length < 2)
                {
                    return Ok("000");
                }

                // Legacy gateway callers (HttpClient self-call / desktop) may abort the request;
                // do not fail the balance lookup on RequestAborted mid-query.
                var response = await _registrationService.GetSmsBalanceAsync(
                    parts[0],
                    parts[1],
                    CancellationToken.None);
                return Ok(response);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Ok("000");
            }
        }

        [HttpGet("UpdateSmsBalance")]
        public async Task<IActionResult> UpdateSmsBalance([FromQuery] string encodedParameter, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(encodedParameter))
            {
                return Ok("000");
            }

            var status = await _registrationService.UpdateSmsBalanceAsync(encodedParameter, ct);
            return Ok(status);
        }

        [HttpGet("UpdateSmsBalanceExpress")]
        public async Task<IActionResult> UpdateSmsBalanceExpress([FromQuery] string parameterEncoded, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(parameterEncoded))
            {
                return Ok("000");
            }

            var status = await _registrationService.UpdateSmsBalanceExpressAsync(parameterEncoded, ct);
            return Ok(status);
        }

        [HttpGet("ChangeCredentials")]
        public async Task<IActionResult> ChangeCredentials([FromQuery] string smsCredential, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(smsCredential))
            {
                return Ok("000");
            }

            var status = await _registrationService.UpdateSmsCredentialAsync(smsCredential, ct);
            return Ok(status);
        }

        [HttpGet("VerifySubscription")]
        public async Task<IActionResult> VerifySubscription([FromQuery] string credentialEncoded, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credentialEncoded))
            {
                return Ok(false);
            }

            var response = await _registrationService.VerifyCredentialsAsync(credentialEncoded, ct);
            return Ok(response);
        }

        [HttpGet("SubscriptionCategoryStatus")]
        public async Task<IActionResult> SubscriptionCategoryStatus([FromQuery] string credentialEncoded, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credentialEncoded))
            {
                return Ok((byte)2);
            }

            var response = await _registrationService.GetSubscriptionCategoryStatusAsync(credentialEncoded, ct);
            return Ok(response);
        }

        [HttpGet("NewSystemMigration")]
        public async Task<IActionResult> NewSystemMigration([FromQuery] string credentialEncoded, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credentialEncoded))
            {
                return Ok("000");
            }

            var response = await _registrationService.UpdateNewSystemInfoAsync(credentialEncoded, persist: true, ct);
            return Ok(response);
        }

        [HttpGet("SubscriptionRewal")]
        public async Task<IActionResult> SubscriptionRewal([FromQuery] string credentialEncoded, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credentialEncoded))
            {
                return Ok("000");
            }

            var response = await _registrationService.GetSubscriptionRenewalKeyAsync(credentialEncoded, ct);
            return Ok(response);
        }
    }
}
