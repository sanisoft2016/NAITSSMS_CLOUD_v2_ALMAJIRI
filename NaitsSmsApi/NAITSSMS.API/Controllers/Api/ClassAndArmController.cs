using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Enum;
using System.Security.Claims;

namespace NAITSSMS.API.Controllers.Api
{
    /// <summary>
    /// Handles class and class-arm CRUD operations.
    /// All writes are scoped-per-request and safe under multi-user concurrency
    /// because SaveChangesAsync uses an EF Core DbContext (scoped lifetime) —
    /// each HTTP request gets its own context, so concurrent users never share state.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClassAndArmController : ControllerBase
    {
        private readonly IClassAndArmService _classAndArmService;
        private readonly ILogger<ClassAndArmController> _logger;

        public ClassAndArmController(
            IClassAndArmService classAndArmService,
            ILogger<ClassAndArmController> logger)
        {
            _classAndArmService = classAndArmService ?? throw new ArgumentNullException(nameof(classAndArmService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ─────────────────────────────────────────────────────────────────────
        // CLASSES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// GET api/classandarm/classes
        /// Legacy equivalent: ClassRegIndex() — returns all classes for the caller's school.
        /// </summary>
        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses(CancellationToken ct)
        {
            try
            {
                var userId = GetUserId();
                var classes = await _classAndArmService.GetClass(userId, ct);
                var classList = classes.ToList();
                return Ok(new
                {
                    totalNoOfClasses = classList.Count,
                    data             = classList
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving classes");
                return StatusCode(500, new { message = "An error occurred while retrieving classes" });
            }
        }

        private string GetUserId()
        {
            var claimsIdentity = (ClaimsIdentity)HttpContext.User.Identity;
            return claimsIdentity.Claims.First(x => x.Type == ClaimTypes.PrimarySid).Value;
        }

        /// <summary>
        /// POST api/classandarm/classes
        /// Legacy equivalent: ClassReg(ClassDto model) — creates a class after uniqueness check.
        /// SNo is computed server-side; clients must NOT send it.
        /// </summary>
        [HttpPost("classes")]
        public async Task<IActionResult> CreateClass([FromBody] ClassFormDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetUserId();

                // Uniqueness check — mirrors: _registrationService.IsClassAlreadyExist(model.ClassName)
                var alreadyExists = await _classAndArmService.IsClassAlreadyExist(model.ClassName, userId, ct);
                if (alreadyExists)
                    return Conflict(new { message = $"This Class ('{model.ClassName}') already exists." });

                // Default ClassLabel to ClassName when not supplied — mirrors legacy behaviour
                if (string.IsNullOrWhiteSpace(model.ClassLabel))
                    model.ClassLabel = model.ClassName.ToString();

                var created = await _classAndArmService.CreateClass(model, userId, ct);

                if (!created)
                    return BadRequest(new { message = "You need to first register your school before creating Classes." });

                return Ok(new { message = "Class created successfully" });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                // TOCTOU guard: another concurrent request created the same class
                // between our IsClassAlreadyExist check and the insert.
                return Conflict(new { message = $"This Class ('{model.ClassName}') already exists." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating class {ClassName}", model.ClassName);
                return StatusCode(500, new { message = "An error occurred while creating the class" });
            }
        }

        /// <summary>
        /// PUT api/classandarm/classes/{classId}
        /// Legacy equivalent: EditClass(ClassDto) — updates ClassName/ClassLabel after uniqueness checks.
        /// </summary>
        [HttpPut("classes/{classId}")]
        public async Task<IActionResult> UpdateClass(string classId, [FromBody] ClassFormDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(classId))
                return BadRequest(new { message = "classId is required." });

            model ??= new ClassFormDto();
            model.Id = classId;

            try
            {
                var userId = GetUserId();
                var updated = await _classAndArmService.UpdateClass(model, userId, ct);
                if (!updated)
                    return Conflict(new { message = "Failed! The selected class name or label already exists." });

                return Ok(new { message = "Class updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating class {ClassId}", classId);
                return StatusCode(500, new { message = "An error occurred while updating the class" });
            }
        }

        /// <summary>
        /// DELETE api/classandarm/classes/{classId}
        /// Legacy equivalent: DeleteClass(int id) — deletes arms first when empty of students.
        /// </summary>
        [HttpDelete("classes/{classId}")]
        public async Task<IActionResult> DeleteClass(string classId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(classId))
                return BadRequest(new { message = "classId is required." });

            try
            {
                var userId = GetUserId();
                var deleted = await _classAndArmService.DeleteClass(classId, userId, ct);
                if (!deleted)
                {
                    return Conflict(new
                    {
                        message = "Failed! The class has arm(s) containing student(s) and therefore cannot be deleted.",
                    });
                }

                return Ok(new { message = "Class deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class {ClassId}", classId);
                return StatusCode(500, new { message = "An error occurred while deleting the class" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CLASS ARMS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// GET api/classandarm/classes/{classId}/arms
        /// Legacy equivalent: ClassArmIndex(...) — returns all arms for a class.
        /// Returns 204 when no arms exist so the frontend can redirect to the create form.
        /// </summary>
        [HttpGet("classes-arms/{classId}")]
        public async Task<IActionResult> GetArmsForClass(string classId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(classId))
                return BadRequest(new { message = "classId is required" });

            try
            {
                var arms = await _classAndArmService.GetClasssArm(classId, ct);
                var armList = arms.ToList();

                // Mirrors legacy: if no arms → redirect to CreatClassArm
                if (!armList.Any())
                    return NoContent(); // 204 — frontend interprets as "fresh class, start adding arms"

                return Ok(new
                {
                    totalNoOfArms = armList.Count,
                    data          = armList
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving arms for class {ClassId}", classId);
                return StatusCode(500, new { message = "An error occurred while retrieving class arms" });
            }
        }

        /// <summary>
        /// POST api/classandarm/classes/arms
        /// Legacy equivalent: CreatClassArm(ClassArmDto model, ...) — creates an arm after uniqueness check.
        /// SNo is computed server-side.
        /// </summary>
        [HttpPost("classes/arms")]
        public async Task<IActionResult> CreateClassArm([FromBody] ClassArmDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.ClassId))
                return BadRequest(new { message = "ClassId is required" });

            try
            {
                // Uniqueness check — mirrors: _registrationService.IsClassArmAlreadyExist(model.ArmName, model.ClassId)
                var alreadyExists = await _classAndArmService.IsClassArmAlreadyExist(model.ArmName, model.ClassId, ct);
                if (alreadyExists)
                    return Conflict(new { message = $"This ClassArm ('{model.ArmName}') already exists." });

                var created = await _classAndArmService.CreatClassArm(model, ct);

                if (!created)
                    return StatusCode(500, new { message = "Failed to create class arm" });

                return Ok(new { message = "Class arm created successfully" });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                // TOCTOU guard: another concurrent request inserted the same arm
                // between our IsClassArmAlreadyExist check and the insert.
                return Conflict(new { message = $"This ClassArm ('{model.ArmName}') already exists." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating arm {ArmName} for class {ClassId}", model.ArmName, model.ClassId);
                return StatusCode(500, new { message = "An error occurred while creating the class arm" });
            }
        }

        /// <summary>
        /// DELETE api/classandarm/classes/arms/{armId}
        /// Legacy equivalent: DeleteClassArm(short id) — deletes an arm only when it has no students.
        /// </summary>
        [HttpDelete("classes/arms/{armId}")]
        public async Task<IActionResult> DeleteClassArm(string armId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(armId))
                return BadRequest(new { message = "armId is required" });

            try
            {
                var deleted = await _classAndArmService.DeleteClassArm(armId, ct);
                if (!deleted)
                    return Conflict(new { message = "Failed! The arm contains student(s) and therefore cannot be deleted." });

                return Ok(new { message = "Arm deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while deleting the class arm" });
            }
        }

        /// <summary>
        /// PUT api/classandarm/classes/arms/{armId}
        /// Legacy equivalent: UpdateClassArm(ClassArmDto) — updates ArmName/FormMaster.
        /// </summary>
        [HttpPut("classes/arms/{armId}")]
        public async Task<IActionResult> UpdateClassArm(string armId, [FromBody] ClassArmDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (armId != model.Id)
                return BadRequest(new { message = "Route armId does not match body Id." });

            try
            {
                var updated = await _classAndArmService.UpdateClassArm(model, ct);
                if (!updated)
                    return Conflict(new { message = $"Another arm with name '{model.ArmName}' already exists in this class." });

                return Ok(new { message = "Class arm updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while updating the class arm" });
            }
        }
    }
}

