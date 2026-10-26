using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.Application.IService;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.API.Controllers.Api
{
    /// <summary>
    /// Commission oversight of programme learning centres. The Commission role reaches only these
    /// endpoints; the service scopes every answer to centres with a programme profile.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Commission,SAOfficer")]
    public class CommissionController : ControllerBase
    {
        private readonly ICommissionService _commissionService;
        private readonly ILogger<CommissionController> _logger;

        public CommissionController(ICommissionService commissionService, ILogger<CommissionController> logger)
        {
            _commissionService = commissionService ?? throw new ArgumentNullException(nameof(commissionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("centres")]
        public Task<IActionResult> Centres(CancellationToken ct)
            => Run(() => _commissionService.GetCentresDirectoryAsync(ct), "the centres directory");

        [HttpGet("oversight")]
        public Task<IActionResult> Oversight([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
            => Run(() => _commissionService.GetOversightAsync(from, to, ct), "attendance oversight");

        [HttpGet("outcomes")]
        public Task<IActionResult> Outcomes([FromQuery] string? session, [FromQuery] byte? term, CancellationToken ct)
            => Run(() => _commissionService.GetOutcomesAsync(session, term, ct), "learning outcomes");

        [HttpGet("centres/{centreId}")]
        public async Task<IActionResult> Centre(
            string centreId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            CancellationToken ct)
        {
            try
            {
                var detail = await _commissionService.GetCentreDetailAsync(centreId, from, to, ct);
                return detail == null
                    ? NotFound(new { message = "That centre is not part of the programme." })
                    : Ok(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading centre {CentreId}", centreId);
                return StatusCode(500, new { message = "An error occurred while loading the centre." });
            }
        }

        [HttpGet("learners")]
        public Task<IActionResult> SearchLearners([FromQuery] string? q, CancellationToken ct)
            => Run(() => _commissionService.SearchLearnersAsync(q ?? string.Empty, ct), "the learner search");

        /// <summary>Learners who have left the programme; outcome = formal, vocational or withdrawn (omit for all).</summary>
        [HttpGet("leavers")]
        public Task<IActionResult> Leavers([FromQuery] string? outcome, CancellationToken ct)
            => Run(() => _commissionService.GetLeaversAsync(outcome, ct), "the leavers list");

        [HttpGet("learners/{studentId}")]
        public async Task<IActionResult> Learner(string studentId, CancellationToken ct)
        {
            try
            {
                var record = await _commissionService.GetLearnerRecordAsync(studentId, ct);
                return record == null
                    ? NotFound(new { message = "No programme learner has that record." })
                    : Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading learner {StudentId}", studentId);
                return StatusCode(500, new { message = "An error occurred while loading the learner record." });
            }
        }

        private async Task<IActionResult> Run<T>(Func<Task<T>> action, string what)
        {
            try
            {
                return Ok(await action());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading {What}", what);
                return StatusCode(500, new { message = $"An error occurred while loading {what}." });
            }
        }
    }
}
