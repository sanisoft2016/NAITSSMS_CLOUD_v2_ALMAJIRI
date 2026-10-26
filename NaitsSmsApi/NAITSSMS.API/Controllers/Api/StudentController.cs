using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAITSSMS.API.Models;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.API.Controllers.Api
{
    [Route("api/student")]
    [ApiController]
    [Authorize]
    public class StudentController : ControllerBase
    {
        private static readonly HashSet<string> AllowedPassportContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png"
        };

        //private readonly IRegistrationService _studentService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<StudentController> _logger;

        private readonly IStudentService _studentService;
    
        public StudentController(
            IStudentService studentService,
            IUnitOfWork unitOfWork,
            IWebHostEnvironment environment,
            ILogger<StudentController> logger)
        {
            _studentService = studentService ?? throw new ArgumentNullException(nameof(studentService));

            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("create-context")]
        public async Task<IActionResult> GetCreateStudentContext(
            [FromQuery] string classId,
            [FromQuery] string classLabel,
            [FromQuery] short totalNoOfStudents,
            [FromQuery] string armId,
            [FromQuery] string armName,
            [FromQuery] CurrentClass currentClass,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(classId) ||
                    string.IsNullOrWhiteSpace(classLabel) ||
                    string.IsNullOrWhiteSpace(armId))
                {
                    return BadRequest(new { message = "Class and arm context are required to register a student." });
                }
                var lastStudentAdmissionNo = await _studentService.GetLastStudentAdminssionNumber(ct);
                var nextStudentNumber = (short)(totalNoOfStudents + 1);

                return Ok(new
                {
                    classId,
                    classLabel,
                    armId,
                    armName,
                    currentClass,
                    totalNoOfStudents,
                    student = new
                    {
                        idNo = lastStudentAdmissionNo,
                        sNo = nextStudentNumber,
                        classArmId = armId,
                        currentClass,
                        dateOfAdmsn = DateTime.UtcNow.AddDays(1),
                        dateOfBirth = DateTime.UtcNow,
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing create-student context for class {ClassId} and arm {ArmId}", classId, armId);
                return StatusCode(500, new { message = "An error occurred while preparing the student registration form." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateStudent([FromBody] StudentDetailsDto model, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _studentService.CreatStudent(model, ct);

                if (result == 0)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Ok(new { message = "Student created successfully", statusCode = result });
                }

                return BadRequest(new { message = "Failed to create student", statusCode = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student");
                return StatusCode(500, new { message = "An error occurred while creating the student" });
            }
        }

        private string GetUserId()
        {
            var claimsIdentity = (ClaimsIdentity)HttpContext.User.Identity;
            return claimsIdentity.Claims.First(x => x.Type == ClaimTypes.PrimarySid).Value;
        }

        [HttpGet("get-students-by-armId/{armId}")]
        public async Task<IActionResult> GetStudentsForArm(string armId, CancellationToken ct)
        {
            try
            {
                var students = await _studentService.GetStudentForAnArm(armId, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students for arm {ArmId}", armId);
                return StatusCode(500, new { message = "An error occurred while retrieving students" });
            }
        }

       
        [HttpGet("search")]
        public async Task<IActionResult> SearchStudents([FromQuery] string admissionNo, CancellationToken ct)
        {
            try
            {
                var students = await _studentService.GetStudentByAdmissionNo(admissionNo, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students by admission number {AdmissionNo}", admissionNo);
                return StatusCode(500, new { message = "An error occurred while searching students" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudentById(string id, CancellationToken ct)
        {
            try
            {
                var student = await _studentService.GetStudentById(id, ct);

                if (student is null || string.IsNullOrWhiteSpace(student.UserId))
                    return NotFound(new { message = "Student not found." });

                return Ok(NormalizeStudentPassport(student));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student {StudentId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the student" });
            }
        }

       
        [HttpDelete("{id}")]
        // Legacy RegistrationController.DeleteStudent(id, userId): delete, remove the StudentOrParent role, commit.
        public async Task<IActionResult> DeleteStudent(string id, [FromQuery] string? userId, CancellationToken ct)
        {
            try
            {
                var status = await _studentService.DeleteStudent(id, ct);
                if (status)
                {
                    await RemoveUserFromRoleAsync(string.IsNullOrWhiteSpace(userId) ? id : userId, "StudentOrParent", ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Ok(new { message = "Record successfully Deleted." });
                }
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Student not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student {StudentId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the student" });
            }

            return BadRequest(new { message = "Failed! The student has one or more important record(s) and therefore we cannot delete it." });
        }

        private async Task RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken ct)
        {
            var role = await _unitOfWork.Repository<IdentityRole>().FindAsync(r => r.Name == roleName, ct);
            if (role == null)
            {
                return;
            }

            var userRoleRepository = _unitOfWork.Repository<IdentityUserRole<string>>();
            var userRoles = await userRoleRepository.WhereAsync(r => r.UserId == userId && r.RoleId == role.Id, ct);
            foreach (var userRole in userRoles)
            {
                userRoleRepository.Delete(userRole);
            }
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentStudents(CancellationToken ct)
        {
            try
            {
                var students = await _studentService.GetRecentlySavedStudents(ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent students");
                return StatusCode(500, new { message = "An error occurred while retrieving recent students" });
            }
        }

        [HttpGet("by-parameters")]
        public async Task<IActionResult> GetStudentsByParameters(
            [FromQuery] string searchParameter,
            CancellationToken ct)
        {
            try
            {
                var students = await _studentService.GetStudentsByParameter(searchParameter, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students by parameters");
                return StatusCode(500, new { message = "An error occurred while retrieving students" });
            }
        }

        [HttpPost("{userId}/enable")]
        public Task<IActionResult> EnableStudentAccount(string userId, CancellationToken ct)
            => SetStudentAccountStatusAsync(userId, AccessStatus.Active, "enabled", ct);

        [HttpPost("{userId}/disable")]
        public Task<IActionResult> DisableStudentAccount(string userId, CancellationToken ct)
            => SetStudentAccountStatusAsync(userId, AccessStatus.Disable, "disabled", ct);

        //[HttpPost("bulk-update-class")]
        //public async Task<IActionResult> BulkUpdateStudentClass(
        //    [FromBody] BulkUpdateClassRequest request,
        //    CancellationToken ct)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //            return BadRequest(ModelState);

        //        var count = await _studentService.BulkUpdateStudentClass(request.StudentIds, request.NewClassArmId, ct);

        //        await _unitOfWork.SaveChangesAsync(ct);
        //        return Ok(new { message = $"{count} students updated successfully", count });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error bulk updating student class");
        //        return StatusCode(500, new { message = "An error occurred while bulk updating student class" });
        //    }
        //}

        [HttpPost("distribute-evenly")]
        public async Task<IActionResult> DistributeStudentsEvenly(CancellationToken ct)
        {
            try
            {
                await _studentService.DistributeStudentEvenly(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return Ok(new { message = "Students distributed evenly successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error distributing students evenly");
                return StatusCode(500, new { message = "An error occurred while distributing students" });
            }
        }

        [HttpGet("search-for-sms")]
        [HttpGet("/api/Registry/search-students-for-sms")]
        public async Task<IActionResult> SearchStudentsForSms(
            [FromQuery] string admissionNo,
            CancellationToken ct)
        {
            try
            {
                var students = await _studentService.GetStudentByAdmissionNo(admissionNo, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students for SMS");
                return StatusCode(500, new { message = "An error occurred while searching students" });
            }
        }

        [HttpGet("all-for-sms")]
        [HttpGet("/api/Registry/all-students-for-sms")]
        public async Task<IActionResult> GetAllStudentsForSms(CancellationToken ct)
        {
            try
            {
                var schoolId = string.Empty;
                var students = await _studentService.GetAllStudents(schoolId, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all students for SMS");
                return StatusCode(500, new { message = "An error occurred while retrieving students" });
            }
        }

        [HttpGet("without-passport")]
        public async Task<IActionResult> GetStudentsWithoutPassport(
            [FromQuery] string className,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(className))
                {
                    return BadRequest(new { message = "Please select a class first." });
                }

                var students = await _studentService.GetStudentsWithoutPassportByClassLabel(className, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students without passport for class {ClassName}", className);
                return StatusCode(500, new { message = "An error occurred while retrieving students" });
            }
        }

        [HttpPost("{id}/status/change-to-previous")]
        public async Task<IActionResult> ChangeStudentStatusToPrevious(
            string id,
            [FromQuery] byte previousStatus,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Changing student {StudentId} status to previous with flag {PreviousStatus}", id, previousStatus);

                if (previousStatus != (byte)StudentStatus.Previous)
                {
                    return BadRequest(new { message = "Invalid status value for this operation." });
                }

                var changed = await _studentService.AlterStudentStatusToPrevious(id, ct);
                if (!changed)
                {
                    return NotFound(new { message = "Student not found." });
                }

                return Ok(new { message = "Student status changed to previous successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing student status to previous");
                return StatusCode(500, new { message = "An error occurred while changing student status to previous" });
            }
        }

        //private async Task<string> GenerateAvailableStudentUserNameAsync(string requestedUserName, CancellationToken ct)
        //{
        //    var candidate = requestedUserName;
        //    if (!await _studentService.DoesThisUserNameExist(candidate, ct))
        //    {
        //        return candidate;
        //    }

        //    var counter = 0;
        //    do
        //    {
        //        counter += 1;
        //        candidate = $"{requestedUserName}e{counter}";
        //    }
        //    while (await _studentService.DoesThisUserNameExist(candidate, ct));

        //    return candidate;
        //}

        private static bool IsValidClassCategoryForCurrentClass(CurrentClass currentClass, ClassCategoryStatus classCategory)
        {
            var isSeniorClass = currentClass is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3;
            var isSeniorCategory = classCategory is ClassCategoryStatus.Science or ClassCategoryStatus.Art or ClassCategoryStatus.Commercial;

            return isSeniorClass ? isSeniorCategory : !isSeniorCategory;
        }

        private static string? ValidatePassport(IFormFile? passport)
        {
            if (passport == null)
            {
                return null;
            }

            var fileSizeKb = passport.Length / 1024;
            if (fileSizeKb > 14)
            {
                return "Your passport size is too big. The maximum size is 14kb";
            }

            if (!AllowedPassportContentTypes.Contains(passport.ContentType))
            {
                return "Only 'JPEG' and 'png' image are allowed.";
            }

            return null;
        }

        private async Task<string> SavePassportAsync(IFormFile passport, CancellationToken ct)
        {
            var fileName = $"{Guid.NewGuid()}.jpg";
            var folder = GetPassportFolderPath();

            Directory.CreateDirectory(folder);

            var fullPath = Path.Combine(folder, fileName);
            await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await passport.CopyToAsync(stream, ct);

            return fileName;
        }

        private void DeletePassportIfExists(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var fullPath = Path.Combine(GetPassportFolderPath(), fileName);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private StudentDetailsDto NormalizeStudentPassport(StudentDetailsDto student)
        {
            if (!PassportFileExists(student.Passport))
            {
                student.Passport = string.Empty;
            }

            return student;
        }

        private bool PassportFileExists(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var fullPath = Path.Combine(GetPassportFolderPath(), fileName);
            return System.IO.File.Exists(fullPath);
        }

        private string GetPassportFolderPath()
        {
            var webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;

            return Path.Combine(webRootPath, "Passport");
        }

        private static string GetStudentRegistrationFailureMessage(byte statusCode)
        {
            return statusCode switch
            {
                1 => "Failed! You need to create academic-session and re-run environmental preparation first.",
                2 => "Failed! Teacher has not been assigned to some subjects in the selected class-Arm.",
                _ => "Subject needs to be created first for the selected class category before you can now add students.",
            };
        }

        private async Task<IActionResult> SetStudentAccountStatusAsync(
            string userId,
            AccessStatus accessStatus,
            string actionLabel,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return BadRequest(new { message = "userId is required." });
                }

                var studentRepo = _unitOfWork.Repository<TxStudentDetail>();
                var student = await studentRepo.FindAsync(s => s.UserId == userId, ct);
                if (student == null)
                {
                    return NotFound(new { message = "Student not found" });
                }

                var userRepo = _unitOfWork.Repository<ApplicationUser, string>();
                var user = await userRepo.FindAsync(userId, ct);
                if (user == null)
                {
                    return NotFound(new { message = "Student not found" });
                }

                user.AccessStatus = accessStatus;
                userRepo.Update(user);
                await _unitOfWork.SaveChangesAsync(ct);

                return Ok(new { message = $"Student account {actionLabel} successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting student account status for {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while updating the student account." });
            }
        }
    }
}