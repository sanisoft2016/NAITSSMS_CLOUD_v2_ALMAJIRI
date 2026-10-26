using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.API.Models;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace NAITSSMS.API.Controllers.Api;

[ApiController]
[Route("api/user-registration"), Authorize]
public sealed class UserRegistrationController : ControllerBase
{
    private const string DefaultTeacherPassword = "Teacher@123";
    private const string DefaultAdminAccountantPassword = "REDACTED";

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    private readonly IUserRegistrationService _userRegistrationService;
    private readonly IRegistrationService _registrationService;
   // private readonly IStudentService _studentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UserRegistrationController> _logger;

    public UserRegistrationController(
        IUserRegistrationService userRegistrationService,
        IRegistrationService registrationService,
        IStudentService studentService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IUnitOfWork unitOfWork,
        IWebHostEnvironment environment,
        ILogger<UserRegistrationController> logger)
    {
        _userRegistrationService = userRegistrationService;
        _registrationService = registrationService;
       // _studentService = studentService;
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("create-school-registrar")]
    [AllowAnonymous]
    public async Task<IActionResult> SchoolRegistrar([FromForm] SchoolRegistrarRequest model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        //if (!string.IsNullOrWhiteSpace(model.SmsApiToken))
        //{
        //    var tokenStatus = await _registrationService.VerifyTokenUniqueness(model.SmsApiToken, ct);
        //    if (!tokenStatus)
        //    {
        //        return BadRequest(new
        //        {
        //            Status = "Failure",
        //            Data = "The Token you have entered has already been used on another School."
        //        });
        //    }
        //}

        //var tokenStatus = await _registrationService.VerifyTokenUniqueness(model.SmsApiToken, ct);
        
        var oldUser = await _userManager.FindByEmailAsync(model.Email);
        if (oldUser is not null)
            return BadRequest(new { Status = "Failure", Data = "Failed! The Email you entered is already registered." });

        //string? savedSignatureFileName = null;

        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    EmailConfirmed = true,
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName ?? "",
                    SurName = model.SurName ?? "",
                    Gender = model.Gender,
                    UserName = model.UserName,
                    CreatedDate = DateTime.UtcNow,
                    UserStatus = UserStatus.SchoolAdmin,
                    AccessStatus = AccessStatus.Active,
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(" ", result.Errors.Select(x => x.Description));
                    throw new InvalidOperationException(errors);
                }

                if (!await _roleManager.RoleExistsAsync("Registry"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Registry"));
                }

                var roleResult = await _userManager.AddToRoleAsync(user, "Registry");
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ", roleResult.Errors.Select(x => x.Description)));
                }

                var registrarDto = new UserDto2nd
                {
                    UserId = user.Id,
                    UserName = model.UserName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName ?? "",
                    SurName = model.SurName ?? "",
                    Gender = model.Gender,
                    EmployeeStatus = EmployeeStatus.Others,
                    Password = model.Password,
                    ConfirmPassword = model.ConfirmPassword,
                    SchoolName = model.SchoolName,
                    DateOfBirth = null,
                    Addresses = string.Empty,
                    SignatureImageString = "",
                    SmsApiToken = model.SmsApiToken ?? string.Empty,
                    SmsApiKey = model.SmsApiKey ?? string.Empty,
                    TimeStatus = TimeStatus.Current,
                    AccessStatus = AccessStatus.Active,
                    UserStatus = UserStatus.SchoolAdmin,
                    SchoolRegId = string.Empty,
                    EmployeeIdNo = string.Empty
                };

                var status = await _userRegistrationService.CreateSchoolRegistrar(registrarDto, ct);
                if (!status.Status)
                {
                    throw new InvalidOperationException(
                        "Failed! Unable to complete school registrar creation.");
                }

                return (IActionResult)Ok(new
                {
                    Status = "Success",
                    Data = new
                    {
                        status.CreatorStatus,
                        status.RegistrationOfficerId,
                        Message = status.CreatorStatus == CreatorStatus.SchoolAdmin
                            ? $"Your Registration is Successful. But you need to first register your school. Your Username is: {model.UserName} and your Password is: {model.Password}"
                            : "School registrar created successfully."
                    }
                });
            }, ct);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "School registrar creation rejected for {Email}", model.Email);
            return BadRequest(new
            {
                Status = "Failure",
                Data = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create school registrar for {Email}", model.Email);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Status = "Failure",
                Data = "Failed! We were unable to complete the process. Contact the system Administrator for solutions."
            });
        }
    }

    [HttpPost("create-teacher-registration")]
    public async Task<IActionResult> RegisterTeacher([FromBody] TeacherRegistrationRequest model, CancellationToken ct)
    {
        var savedSignatureFileName = string.Empty;

        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { message = "Unable to resolve the current user." });
            }

            if (!TryParseEnumValue(model.Gender, out Gender gender))
            {
                ModelState.AddModelError(nameof(model.Gender), "Gender is required.");
            }

            if (!TryParseEnumValue(model.EmployeeStatus, out EmployeeStatus employeeStatus))
            {
                ModelState.AddModelError(nameof(model.EmployeeStatus), "Employee status is required.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (employeeStatus == EmployeeStatus.Accountant)
            {
                return BadRequest(new { message = "Sorry! You are not eligible to create an accountant user." });
            }

            var signatureValidationMessage = ValidateSignatureImagePayload(model.SignatureImageString);
            if (!string.IsNullOrWhiteSpace(signatureValidationMessage))
            {
                return BadRequest(new { message = signatureValidationMessage });
            }

            var email = string.IsNullOrWhiteSpace(model.Email)
                ? "contact@gmail.com"
                : model.Email.Trim();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                EmailConfirmed = true,
                FirstName = model.FirstName.Trim(),
                MiddleName = model.MiddleName?.Trim() ?? string.Empty,
                SurName = model.SurName?.Trim() ?? string.Empty,
                Gender = gender,
                UserName = model.UserName.Trim(),
                CreatedDate = DateTime.UtcNow,
                UserStatus = UserStatus.Others,
                AccessStatus = AccessStatus.Active,
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var createResult = await _userManager.CreateAsync(user, DefaultTeacherPassword);
                if (!createResult.Succeeded)
                {
                    throw new RegistrationRejectedException(AggregateIdentityErrors(createResult));
                }

                var roleResult = await SyncTeacherRoleAsync(user, employeeStatus);
                if (!roleResult.Succeeded)
                {
                    throw new RegistrationRejectedException(AggregateIdentityErrors(roleResult));
                }

                savedSignatureFileName = await NormalizeSignatureImageAsync(model.SignatureImageString, ct, "teacher-signatures");

                var teacherModel = new UserDto2nd
                {
                    FirstName = model.FirstName.Trim(),
                    MiddleName = model.MiddleName?.Trim() ?? string.Empty,
                    SurName = model.SurName?.Trim() ?? string.Empty,
                    UserName = model.UserName.Trim(),
                    Email = email,
                    Gender = gender,
                    EmployeeStatus = employeeStatus,
                    Password = DefaultTeacherPassword,
                    ConfirmPassword = DefaultTeacherPassword,
                    CreatedDate = user.CreatedDate,
                    PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty,
                    DateOfBirth = model.DateOfBirth,
                    UserId = user.Id,
                    Addresses = string.Empty,
                    SignatureImageString = savedSignatureFileName,
                    SchoolRegId = string.Empty,
                    AccessStatus = AccessStatus.Active,
                    EmployeeIdNo = model.EmployeeIdNo?.Trim() ?? string.Empty,
                    TimeStatus = TimeStatus.Current,
                    UserStatus = UserStatus.Others,
                    SchoolName = string.Empty,
                    SmsApiKey = string.Empty,
                    SmsApiToken = string.Empty,
                };

                var status = await _userRegistrationService.CreateTeacherRecord(currentUserId, teacherModel, ct);
                if (!status)
                {
                    throw new RegistrationRejectedException("Failed to create teacher record.");
                }
            }, ct);

            return Ok(new
            {
                message = "Teacher Registration Succeeded.",
                userId = user.Id,
            });
        }
        catch (RegistrationRejectedException ex)
        {
            DeleteNormalizedSignatureIfExists(savedSignatureFileName, "teacher-signatures");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            DeleteNormalizedSignatureIfExists(savedSignatureFileName, "teacher-signatures");
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error registering teacher {UserName}", model?.UserName);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."
            });
        }
    }

    [HttpGet("get-teacher-registration-byId/{id}")]
    public async Task<IActionResult> GetTeacher(string id, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { message = "Unable to resolve the current user." });
            }

            var teacher = await _unitOfWork.Repository<TxEmployee>()
                .FindAsync(x => x.Id == id && x.SchoolId == currentUserId, ct, x => x.User!);

            if (teacher is null || teacher.User is null)
            {
                return NotFound(new { message = "Teacher not found." });
            }

            return Ok(MapTeacherRegistrationRequest(teacher));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving teacher {TeacherId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."
            });
        }
    }

    [HttpGet("get-student-details-byId/{id}")]
    public async Task<IActionResult> GetStudent(string id, CancellationToken ct)
    {
        try
        {
            var student = await _userRegistrationService.GetStudentById(id, ct);
            if (student is null || string.IsNullOrWhiteSpace(student.UserId))
            {
                return NotFound(new { message = "Student not found." });
            }

            return Ok(NormalizeStudentPassport(student));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student {StudentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."
            });
        }
    }

    [HttpPost("create-student-details")]
    public async Task<IActionResult> RegisterStudent([FromForm] StudentRegistrationRequest model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!string.IsNullOrWhiteSpace(model.EmailAddress) &&
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.EmailAddress.Trim()))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }

        model.EmailAddress = string.IsNullOrWhiteSpace(model.EmailAddress) ? null : model.EmailAddress.Trim();

        var currentUserId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized(new { message = "Unable to resolve the current user." });
        }

        if (!IsValidClassCategoryForCurrentClass(model.CurrentClass, model.ClassCategory))
        {
            return BadRequest(new
            {
                message = "Failed! Appropriate Senior class category like - Science or Art or Commercial needs to be selected."
            });
        }

        var classArmId = !string.IsNullOrWhiteSpace(model.ClassArmId)
            ? model.ClassArmId.Trim()
            : model.ArmId.Trim();

        if (string.IsNullOrWhiteSpace(classArmId))
        {
            return BadRequest(new { message = "A class arm is required before a student can be registered." });
        }

        var passportValidationMessage = ValidatePassport(model.Passport);
        if (!string.IsNullOrWhiteSpace(passportValidationMessage))
        {
            return BadRequest(new { message = passportValidationMessage });
        }

        var savedPassportFileName = string.Empty;
        ApplicationUser? createdUser = null;

        try
        {
            var validationStatus = await _userRegistrationService.ValidationChecks(
                model.IdNo,
                model.CurrentClass,
                model.ArmName,
                model.ClassCategory,
                ct);

            if (validationStatus == 1)
            {
                return BadRequest(new { message = $"This Id No. ('{model.IdNo}') already exist." });
            }

            if (!await _userRegistrationService.IsClassAlreadyExist(model.CurrentClass, ct))
            {
                return BadRequest(new
                {
                    message = $"You need to register the Class \"{model.ClassLabel}\" before adding Student to it."
                });
            }

            if (!await _userRegistrationService.IsClassArmAlreadyExist(model.ArmName, model.ClassId, ct))
            {
                return BadRequest(new
                {
                    message = $"You need to register the Arm \"{model.ArmName}\" in \"{model.ClassLabel}\" class before adding Student to it."
                });
            }

            if (!IsValidClassCategoryForCurrentClass(model.CurrentClass, model.ClassCategory))
            {
                return BadRequest(new
                {
                    message = "Failed! Appropriate Senior class category like - Science or Art or Commercial needs to be selected."
                });
            }

            var currentTermInfo = await _userRegistrationService.GetCurrentTermStatusAndId(ct);
            if (currentTermInfo is null)
            {
                return BadRequest(new
                {
                    message = "Sorry! The current term needs to first be created before you can start adding student."
                });
            }

            if (model.Passport is not null)
            {
                savedPassportFileName = await SavePassportAsync(model.Passport, ct);
            }

            var schoolSerialId = await _userRegistrationService.GetSchoolSerialId(ct);
            var studentAdmissionNumber = Regex.Replace(model.IdNo ?? string.Empty, @"[^0-9a-zA-Z]", string.Empty);
            var requestedUserName = $"{schoolSerialId}{studentAdmissionNumber}";
            var userName = await GenerateAvailableStudentUserNameAsync(requestedUserName, ct);
            var emailAddress = !string.IsNullOrWhiteSpace(model.EmailAddress)
                ? model.EmailAddress.Trim()
                : "contact@gmail.com";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = emailAddress,
                PhoneNumber = string.IsNullOrWhiteSpace(model.StudentMobileNo)
                    ? null
                    : model.StudentMobileNo.Trim(),
                EmailConfirmed = true,
                FirstName = model.FirstName.Trim(),
                MiddleName = model.MiddleName?.Trim() ?? string.Empty,
                SurName = model.SurName.Trim(),
                Gender = model.Gender,
                UserName = userName,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                UserStatus = UserStatus.Student,
                AccessStatus = AccessStatus.Active,
            };

            user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, "123456");
            var createUserResult = await _userManager.CreateAsync(user);
            if (!createUserResult.Succeeded)
            {
                DeletePassportIfExists(savedPassportFileName);
                return BadRequest(new
                {
                    message = string.Join(" ", createUserResult.Errors.Select(error => error.Description))
                });
            }

            createdUser = user;

            var addRoleResult = await _userManager.AddToRoleAsync(user, "StudentOrParent");
            if (!addRoleResult.Succeeded)
            {
                if (createdUser != null)
                {
                    await _userManager.DeleteAsync(createdUser);
                }

                DeletePassportIfExists(savedPassportFileName);
                return BadRequest(new
                {
                    message = string.Join(" ", addRoleResult.Errors.Select(error => error.Description))
                });
            }

            var studentModel = BuildStudentDetailsDto(model, user.Id, savedPassportFileName, currentTermInfo.Value.TermId);
            var status = await _userRegistrationService.CreatStudent(studentModel, model.ArmName, model.CurrentClass, ct);
            if (status != 0)
            {
                if (createdUser != null)
                {
                    await _userManager.DeleteAsync(createdUser);
                }

                _unitOfWork.RejectChanges();
                DeletePassportIfExists(savedPassportFileName);
                return BadRequest(new
                {
                    message = GetStudentRegistrationFailureMessage(status),
                    statusCode = status,
                });
            }

            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new
            {
                message = "Student Registration Successful.",
                data = new
                {
                    studentId = studentModel.Id,
                    userId = user.Id,
                    classId = model.ClassId,
                    classLabel = model.ClassLabel,
                    armId = classArmId,
                    armName = model.ArmName.ToString(),
                    currentClass = model.CurrentClass,
                }
            });
        }
        catch (Exception ex)
        {
            if (createdUser != null)
            {
                await _userManager.DeleteAsync(createdUser);
            }

            _unitOfWork.RejectChanges();
            DeletePassportIfExists(savedPassportFileName);
            _logger.LogError(ex, "Error registering student for class {ClassId} and arm {ArmId}", model.ClassId, classArmId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed! We were unable to complete the process. Contact the system Administrator for solutions."
            });
        }
    }

    [HttpPut("update-student-details-byId/{id}")]
    public async Task<IActionResult> UpdateStudent(string id, [FromForm] StudentRegistrationRequest model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!string.IsNullOrWhiteSpace(model.EmailAddress) &&
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.EmailAddress.Trim()))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }

        model.EmailAddress = string.IsNullOrWhiteSpace(model.EmailAddress) ? null : model.EmailAddress.Trim();

        var currentUserId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized(new { message = "Unable to resolve the current user." });
        }

        var classArmId = !string.IsNullOrWhiteSpace(model.ClassArmId)
            ? model.ClassArmId.Trim()
            : model.ArmId.Trim();

        if (string.IsNullOrWhiteSpace(classArmId))
        {
            return BadRequest(new { message = "A class arm is required before a student can be updated." });
        }

        if (!await _userRegistrationService.IsClassAlreadyExist(model.CurrentClass, ct))
        {
            return BadRequest(new
            {
                message = $"You need to register the Class \"{model.ClassLabel}\" before adding Student to it."
            });
        }

        if (!await _userRegistrationService.IsClassArmAlreadyExist(model.ArmName, model.ClassId, ct))
        {
            return BadRequest(new
            {
                message = $"You need to register the Arm \"{model.ArmName}\" in \"{model.ClassLabel}\" class before adding Student to it."
            });
        }

        if (!IsValidClassCategoryForCurrentClass(model.CurrentClass, model.ClassCategory))
        {
            return BadRequest(new
            {
                message = "Failed! Your class selection does not match the category. kindly correct the class and its category to match correctly."
            });
        }

        var passportValidationMessage = ValidatePassport(model.Passport);
        if (!string.IsNullOrWhiteSpace(passportValidationMessage))
        {
            return BadRequest(new { message = passportValidationMessage });
        }

        var existingStudent = await _userRegistrationService.GetStudentById(id, ct);
        if (existingStudent is null || string.IsNullOrWhiteSpace(existingStudent.UserId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var studentRepository = _unitOfWork.Repository<TxStudentDetail>();
        var duplicateStudent = await studentRepository.FindAsync(
            x => x.IdNo == model.IdNo.Trim() && x.UserId != existingStudent.UserId,
            ct);

        if (duplicateStudent is not null)
        {
            return BadRequest(new { message = $"Failed! This Id \"{model.IdNo}\" has been used for another student." });
        }

        var originalPassportFileName = existingStudent.Passport;
        var savedPassportFileName = existingStudent.Passport;
        var newPassportUploaded = false;
        if (model.Passport is not null)
        {
            savedPassportFileName = await SavePassportAsync(model.Passport, ct);
            newPassportUploaded = true;
        }

        try
        {
            var studentModel = BuildStudentDetailsDto(model, existingStudent.UserId, savedPassportFileName, existingStudent.AdmisnTermId);
            studentModel.Id = existingStudent.Id;
            studentModel.SchoolInfoId = existingStudent.SchoolInfoId;
            studentModel.StudentInitialRegStatus = existingStudent.StudentInitialRegStatus;

            var updateStatus = await _userRegistrationService.UpdateStudentDetails(studentModel, model.ArmName, ct);
            if (!string.Equals(updateStatus, "Success", StringComparison.OrdinalIgnoreCase))
            {
                if (newPassportUploaded)
                {
                    DeletePassportIfExists(savedPassportFileName);
                }

                return BadRequest(new { message = updateStatus });
            }

            await _unitOfWork.SaveChangesAsync(ct);

            if (newPassportUploaded
                && !string.IsNullOrWhiteSpace(originalPassportFileName)
                && !string.Equals(originalPassportFileName, savedPassportFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeletePassportIfExists(originalPassportFileName);
            }

            return Ok(new
            {
                message = "Student updated successfully.",
                data = new
                {
                    studentId = studentModel.Id,
                    userId = studentModel.UserId,
                    classId = model.ClassId,
                    classLabel = model.ClassLabel,
                    armId = classArmId,
                    armName = model.ArmName.ToString(),
                    currentClass = model.CurrentClass,
                }
            });
        }
        catch (Exception ex)
        {
            if (newPassportUploaded)
            {
                DeletePassportIfExists(savedPassportFileName);
            }

            _logger.LogError(ex, "Error updating student {StudentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."
            });
        }
    }

    [HttpPut("update-teacher-registration-byId/{id}")]
    public async Task<IActionResult> UpdateTeacher(string id, [FromBody] TeacherRegistrationRequest model, CancellationToken ct)
    {
        var savedSignatureFileName = string.Empty;
        string? originalSignatureFileName = null;

        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { message = "Unable to resolve the current user." });
            }

            if (!TryParseEnumValue(model.Gender, out Gender gender))
            {
                ModelState.AddModelError(nameof(model.Gender), "Gender is required.");
            }

            if (!TryParseEnumValue(model.EmployeeStatus, out EmployeeStatus employeeStatus))
            {
                ModelState.AddModelError(nameof(model.EmployeeStatus), "Employee status is required.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (employeeStatus == EmployeeStatus.Accountant)
            {
                return BadRequest(new { message = "Sorry! You are not eligible to create an accountant user." });
            }

            var signatureValidationMessage = ValidateSignatureImagePayload(model.SignatureImageString);
            if (!string.IsNullOrWhiteSpace(signatureValidationMessage))
            {
                return BadRequest(new { message = signatureValidationMessage });
            }

            var teacher = await _unitOfWork.Repository<TxEmployee>()
                .FindAsync(x => x.Id == id && x.SchoolId == currentUserId, ct);

            if (teacher is null)
            {
                return NotFound(new { message = "Teacher not found." });
            }

            var trimmedUserName = model.UserName.Trim();
            var oldUser = await _userManager.FindByNameAsync(trimmedUserName);
            if (oldUser is not null && !string.Equals(oldUser.Id, teacher.UserId, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "Failed! Username exist agaist another user." });
            }

            var user = oldUser;
            if (user is null)
            {
                user = await _userManager.FindByIdAsync(teacher.UserId);
            }

            if (user is null)
            {
                return NotFound(new { message = "Teacher user account not found." });
            }

            var email = string.IsNullOrWhiteSpace(model.Email)
                ? "contact@gmail.com"
                : model.Email.Trim();

            originalSignatureFileName = teacher.SignatureImageString;

            var normalizedSignatureFileName = originalSignatureFileName ?? string.Empty;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                user.UserName = trimmedUserName;
                user.Email = email;
                user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
                user.FirstName = model.FirstName.Trim();
                user.MiddleName = model.MiddleName?.Trim() ?? string.Empty;
                user.SurName = model.SurName?.Trim() ?? string.Empty;
                user.Gender = gender;

                var updateUserResult = await _userManager.UpdateAsync(user);
                if (!updateUserResult.Succeeded)
                {
                    throw new RegistrationRejectedException(AggregateIdentityErrors(updateUserResult));
                }

                var roleResult = await SyncTeacherRoleAsync(user, employeeStatus);
                if (!roleResult.Succeeded)
                {
                    throw new RegistrationRejectedException(AggregateIdentityErrors(roleResult));
                }

                if (!string.IsNullOrWhiteSpace(model.SignatureImageString))
                {
                    normalizedSignatureFileName = await NormalizeSignatureImageAsync(model.SignatureImageString, ct, "teacher-signatures");
                    savedSignatureFileName = normalizedSignatureFileName;
                }

                teacher.EmployeeIdNo = model.EmployeeIdNo?.Trim() ?? string.Empty;
                teacher.DateOfBirth = model.DateOfBirth;
                teacher.EmployeeStatus = employeeStatus;
                teacher.SignatureImageString = normalizedSignatureFileName;

                _unitOfWork.Repository<TxEmployee>().Update(teacher);
            }, ct);

            if (!string.IsNullOrWhiteSpace(originalSignatureFileName)
                && !string.IsNullOrWhiteSpace(savedSignatureFileName)
                && !string.Equals(originalSignatureFileName, savedSignatureFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteNormalizedSignatureIfExists(originalSignatureFileName, "teacher-signatures");
            }

            return Ok(new
            {
                message = "Teacher details updated successfully.",
                userId = teacher.UserId,
            });
        }
        catch (RegistrationRejectedException ex)
        {
            if (!string.IsNullOrWhiteSpace(savedSignatureFileName)
                && !string.Equals(savedSignatureFileName, originalSignatureFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteNormalizedSignatureIfExists(savedSignatureFileName, "teacher-signatures");
            }

            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(savedSignatureFileName)
                && !string.Equals(savedSignatureFileName, originalSignatureFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteNormalizedSignatureIfExists(savedSignatureFileName, "teacher-signatures");
            }

            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error updating teacher {TeacherId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."
            });
        }
    }
    [HttpDelete("delete-teacher-byId/{id}")]
    public async Task<IActionResult> DeleteTeacher(string id, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { message = "Unable to resolve the current user." });
            }

            var teacherRepository = _unitOfWork.Repository<TxEmployee>();
            var teacher = await teacherRepository
                .FindAsync(x => x.Id == id && x.SchoolId == currentUserId, ct, x => x.User!);

            if (teacher is null)
            {
                return NotFound(new { message = "Teacher not found." });
            }

            if (await _unitOfWork.Repository<TxEmployeeIncomeStructure>().FindAsync(x => x.Id == id, ct) is not null)
            {
                return BadRequest(new
                {
                    message = "Sorry! Could not continue because the corresponding payment has been set for this User, so go to Manage Salary to delete it before the user's information can be deleted."
                });
            }

            if (await _unitOfWork.Repository<TeachTxerSubjectArmAssignment>().FindAsync(x => x.EmployeeId == id, ct) is not null)
            {
                return BadRequest(new
                {
                    message = "Sorry! Could not continue. This teacher is a subject teacher, so he needs to be un-assigned as subject teacher before his/her information can be deleted."
                });
            }

            if (await _unitOfWork.Repository<StudenXtAccessment>().FindAsync(x => x.EmployeeId == id, ct) is not null)
            {
                return BadRequest(new
                {
                    message = "Sorry! Could not continue. This teacher has entered student's scores before, so the employee's information cannot be deleted."
                });
            }

            if (await _unitOfWork.Repository<TeacherClassArmTxAssignment>().FindAsync(x => x.EmployeeId == id, ct) is not null)
            {
                return BadRequest(new
                {
                    message = "Sorry! Could not continue. This teacher is a class teacher, so he needs to be un-assigned as a class teacher before his/her information can be deleted."
                });
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                teacherRepository.Delete(teacher);
                await _unitOfWork.SaveChangesAsync(ct);

                if (teacher.User is not null)
                {
                    var deleteUserResult = await _userManager.DeleteAsync(teacher.User);
                    if (!deleteUserResult.Succeeded)
                    {
                        throw new RegistrationRejectedException(AggregateIdentityErrors(deleteUserResult));
                    }
                }
            }, ct);

            DeleteNormalizedSignatureIfExists(teacher.SignatureImageString, "teacher-signatures");

            return Ok(new { message = "Teacher deleted successfully." });
        }
        catch (RegistrationRejectedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error deleting teacher {TeacherId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."
            });
        }
    }

    /// <summary>
    /// Legacy RegistrationController.CreateAdminAccountant (moved from SuperAdminController).
    /// Password is fixed to "REDACTED"; username is SuperAccountant{SsNo}.
    /// </summary>
    [HttpPost("create-admin-accountant-by-schoolId/{schoolId}")]
    [Authorize(Roles = "SAOfficer")]
    public async Task<IActionResult> CreateAdminAccountant(
        string schoolId,
        [FromBody] CreateAdminAccountantRequestDto model,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Ok(FailAdminAccountant("invalid", "School id is required."));
            }

            if (model == null || string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.SurName))
            {
                return Ok(FailAdminAccountant("invalid", "First Name and Sur Name must be entered to continue."));
            }

            var signatureValidation = ValidateSignatureImagePayload(model.SignatureImageString);
            if (!string.IsNullOrWhiteSpace(signatureValidation))
            {
                return Ok(FailAdminAccountant("signature", signatureValidation));
            }

            var check = await _registrationService.CheckRenewalOrRegistrationStatus(schoolId, ct);
            if (check == null)
            {
                return Ok(FailAdminAccountant(
                    "term_not_ready",
                    "Sorry! Other registrations need to be done first till when the current 'Term' is fully set; or renew this application before creating Accountant.",
                    "/supper-admin"));
            }

            var schoolSno = await _registrationService.GetSchoolSnoForSuperAdminOfficer(schoolId, ct);
            var userName = "SuperAccountant" + schoolSno;
            // Avoid Identity unique-email collisions with the shared legacy placeholder.
            var email = string.IsNullOrWhiteSpace(model.Email)
                ? $"{userName.ToLowerInvariant()}@naits.local"
                : model.Email.Trim();
            var signatureFileName = await NormalizeSignatureImageAsync(model.SignatureImageString, ct, "signatures");

            var existingUser = await _userManager.FindByNameAsync(userName);
            if (existingUser != null)
            {
                return Ok(FailAdminAccountant(
                    "identity_failed",
                    $"Username '{userName}' already exists. Use View Admin Accountant, or delete the existing accountant first."));
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                EmailConfirmed = true,
                FirstName = model.FirstName.Trim(),
                MiddleName = model.MiddleName?.Trim() ?? string.Empty,
                SurName = model.SurName.Trim(),
                Gender = model.Gender,
                UserName = userName,
                CreatedDate = DateTime.UtcNow,
                AccessStatus = AccessStatus.Active,
                UserStatus = UserStatus.Others
            };

            var createResult = await _userManager.CreateAsync(user, DefaultAdminAccountantPassword);
            if (!createResult.Succeeded)
            {
                return Ok(FailAdminAccountant(
                    "identity_failed",
                    string.Join(" ", createResult.Errors.Select(e => e.Description))));
            }

            if (!await _roleManager.RoleExistsAsync("Accounting"))
            {
                var roleCreate = await _roleManager.CreateAsync(new IdentityRole("Accounting"));
                if (!roleCreate.Succeeded)
                {
                    await _userManager.DeleteAsync(user);
                    return Ok(FailAdminAccountant(
                        "role_failed",
                        "Failed! Unable to complete the process. You need to first create the appropriate R."));
                }
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Accounting");
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return Ok(FailAdminAccountant(
                    "role_failed",
                    "Failed! Unable to complete the process. You need to first create the appropriate R. "
                    + string.Join(" ", roleResult.Errors.Select(e => e.Description))));
            }

            var employeeOk = await _registrationService.CreateSuperAccountantEmployeeRecord(
                new CreateAdminAccountantEmployeeDto
                {
                    SchoolId = schoolId,
                    UserId = user.Id,
                    UserName = userName,
                    DateOfBirth = model.DateOfBirth,
                    SignatureImageString = signatureFileName,
                },
                ct);

            if (!employeeOk)
            {
                await _userManager.RemoveFromRoleAsync(user, "Accounting");
                await _userManager.DeleteAsync(user);
                return Ok(FailAdminAccountant(
                    "employee_failed",
                    "Welcome. Other registrations need to be done first till when the current 'Term' is fully set; before you can now create 'Admin Accountant'",
                    "/supper-admin"));
            }

            return Ok(new CreateAdminAccountantResultDto
            {
                Success = true,
                Code = "ok",
                Message = "Admin Accountant Registration Succeeded.",
                RedirectPath = "/supper-admin"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin accountant for school {SchoolId}", schoolId);
            return Ok(FailAdminAccountant(
                "error",
                "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."));
        }
    }

    /// <summary>
    /// Legacy RegistrationController.CreateAdminAccountant (Accounting/SuperAccountant branch):
    /// a SuperAccountant creates a Subordinate Accountant using a caller-supplied username.
    /// </summary>
    [HttpPost("create-subordinate-accountant-by-schoolId/{schoolId}")]
    [Authorize(Roles = "SAOfficer, Accounting")]
    public async Task<IActionResult> CreateSubordinateAccountant(
        string schoolId,
        [FromBody] CreateSubordinateAccountantRequestDto model,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return Ok(FailAdminAccountant("invalid", "School id is required."));
            }

            if (model == null
                || string.IsNullOrWhiteSpace(model.FirstName)
                || string.IsNullOrWhiteSpace(model.SurName))
            {
                return Ok(FailAdminAccountant("invalid", "First Name and Sur Name must be entered to continue."));
            }

            if (string.IsNullOrWhiteSpace(model.UserName))
            {
                return Ok(FailAdminAccountant("invalid", "User Name must be entered to continue."));
            }

            var signatureValidation = ValidateSignatureImagePayload(model.SignatureImageString);
            if (!string.IsNullOrWhiteSpace(signatureValidation))
            {
                return Ok(FailAdminAccountant("signature", signatureValidation));
            }

            var userName = model.UserName.Trim();
            var email = string.IsNullOrWhiteSpace(model.Email)
                ? $"{userName.ToLowerInvariant()}@naits.local"
                : model.Email.Trim();
            var signatureFileName = await NormalizeSignatureImageAsync(model.SignatureImageString, ct, "signatures");

            var existingUser = await _userManager.FindByNameAsync(userName);
            if (existingUser != null)
            {
                return Ok(FailAdminAccountant(
                    "identity_failed",
                    $"Username '{userName}' already exists. Choose a different user name."));
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                EmailConfirmed = true,
                FirstName = model.FirstName.Trim(),
                MiddleName = model.MiddleName?.Trim() ?? string.Empty,
                SurName = model.SurName.Trim(),
                Gender = model.Gender,
                UserName = userName,
                CreatedDate = DateTime.UtcNow,
                AccessStatus = AccessStatus.Active,
                UserStatus = UserStatus.Others
            };

            var createResult = await _userManager.CreateAsync(user, DefaultAdminAccountantPassword);
            if (!createResult.Succeeded)
            {
                return Ok(FailAdminAccountant(
                    "identity_failed",
                    string.Join(" ", createResult.Errors.Select(e => e.Description))));
            }

            if (!await _roleManager.RoleExistsAsync("Accounting"))
            {
                var roleCreate = await _roleManager.CreateAsync(new IdentityRole("Accounting"));
                if (!roleCreate.Succeeded)
                {
                    await _userManager.DeleteAsync(user);
                    return Ok(FailAdminAccountant(
                        "role_failed",
                        "Failed! Unable to complete the process. You need to first create the appropriate R."));
                }
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Accounting");
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return Ok(FailAdminAccountant(
                    "role_failed",
                    "Failed! Unable to complete the process. You need to first create the appropriate R. "
                    + string.Join(" ", roleResult.Errors.Select(e => e.Description))));
            }

            var employeeOk = await _registrationService.CreateSubordinateAccountantEmployeeRecord(
                new CreateAdminAccountantEmployeeDto
                {
                    SchoolId = schoolId,
                    UserId = user.Id,
                    UserName = userName,
                    DateOfBirth = model.DateOfBirth,
                    SignatureImageString = signatureFileName,
                },
                ct);

            if (!employeeOk)
            {
                await _userManager.RemoveFromRoleAsync(user, "Accounting");
                await _userManager.DeleteAsync(user);
                return Ok(FailAdminAccountant(
                    "employee_failed",
                    "Failed! We were unable to create the subordinate accountant employee record."));
            }

            return Ok(new CreateAdminAccountantResultDto
            {
                Success = true,
                Code = "ok",
                Message = "Subordinate Accountant Registration Succeeded.",
                RedirectPath = User.IsInRole("Accounting") ? "/accounting" : "/supper-admin"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subordinate accountant for school {SchoolId}", schoolId);
            return Ok(FailAdminAccountant(
                "error",
                "Failed! We were unable to complete the process. Contact the system Aministrator for solutions."));
        }
    }

    /// <summary>Legacy StudentFeesController.SubordinateAccountantStatusInfo</summary>
    [HttpGet("subordinate-accountant-status")]
    [Authorize(Roles = "Accounting, SAOfficer, Registry")]
    public async Task<IActionResult> GetSubordinateAccountantStatus(CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var status = await _registrationService.GetSchoolAccountantStatus(userId, ct);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading subordinate accountant status");
            return StatusCode(500, new { message = "Failed! We were unable to complete the process." });
        }
    }

    /// <summary>Legacy StudentFeesController.SubordinateAccountantDetails</summary>
    [HttpGet("subordinate-accountant-details/{schoolId}")]
    [Authorize(Roles = "Accounting, SAOfficer")]
    public async Task<IActionResult> GetSubordinateAccountantDetails(string schoolId, CancellationToken ct)
    {
        try
        {
            var details = await _registrationService.GetSubordinateAccountantDetails(schoolId, ct);
            if (details == null)
            {
                return Ok(null);
            }

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading subordinate accountant details for school {SchoolId}", schoolId);
            return StatusCode(500, new { message = "Failed! We were unable to complete the process." });
        }
    }

    /// <summary>Legacy StudentFeesController.EditAccountant (SuperAccountant branch)</summary>
    [HttpPost("update-subordinate-accountant-by-schoolId/{schoolId}")]
    [Authorize(Roles = "Accounting")]
    public async Task<IActionResult> UpdateSubordinateAccountant(
        string schoolId,
        [FromBody] UpdateAdminAccountantDto model,
        CancellationToken ct)
    {
        try
        {
            model.SchoolRegId = schoolId;
            if (!string.IsNullOrWhiteSpace(model.Password)
                && !string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
            {
                return Ok(false);
            }

            var status = await _registrationService.UpdateSubordinateAccountantInfo(model, ct);
            if (!status)
            {
                return Ok(false);
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var details = await _registrationService.GetSubordinateAccountantDetails(schoolId, ct);
                if (details?.UserId != null)
                {
                    var user = await _userManager.FindByIdAsync(details.UserId);
                    if (user != null)
                    {
                        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                        await _userManager.ResetPasswordAsync(user, token, model.Password);
                    }
                }
            }

            return Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subordinate accountant for school {SchoolId}", schoolId);
            return Ok(false);
        }
    }

    /// <summary>Legacy StudentFees EnableAccountantAccount (Accounting context)</summary>
    [HttpGet("enable-subordinate-accountant/{userId}")]
    [Authorize(Roles = "Accounting")]
    public async Task<IActionResult> EnableSubordinateAccountant(string userId, CancellationToken ct)
    {
        var status = await _registrationService.EnableAccountantUserById(userId, ct);
        return Ok(status);
    }

    /// <summary>Legacy StudentFees DisableAccountantAccount (Accounting context)</summary>
    [HttpGet("disable-subordinate-accountant/{userId}")]
    [Authorize(Roles = "Accounting")]
    public async Task<IActionResult> DisableSubordinateAccountant(string userId, CancellationToken ct)
    {
        var status = await _registrationService.DisableAccountantUserById(userId, ct);
        return Ok(status);
    }

    /// <summary>Legacy RegistrationController.DeleteAnAccountants</summary>
    [HttpPost("delete-subordinate-accountant/{employeeId}")]
    [Authorize(Roles = "Accounting")]
    public async Task<IActionResult> DeleteSubordinateAccountant(string employeeId, CancellationToken ct)
    {
        try
        {
            var deletedUserId = await _registrationService.DeleteSubordinateAccountant(employeeId, ct);
            return Ok(!string.IsNullOrWhiteSpace(deletedUserId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subordinate accountant {EmployeeId}", employeeId);
            return Ok(false);
        }
    }

    /// <summary>Legacy SadminController.EditAccountant (moved from SuperAdminController).</summary>
    [HttpPost("update-admin-accountant-by-schoolId/{schoolId}")]
    [Authorize(Roles = "SAOfficer")]
    public async Task<IActionResult> EditAccountant(
        string schoolId,
        [FromBody] UpdateAdminAccountantDto model,
        CancellationToken ct)
    {
        try
        {
            model.SchoolRegId = schoolId;
            if (!string.IsNullOrWhiteSpace(model.Password)
                && !string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
            {
                return Ok(false);
            }

            var status = await _registrationService.UpdateAccountantInfo(model, ct);
            if (!status)
            {
                return Ok(false);
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var details = await _registrationService.GetAdminAccountantDetails(schoolId, ct);
                if (details?.UserId != null)
                {
                    var user = await _userManager.FindByIdAsync(details.UserId);
                    if (user != null)
                    {
                        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                        await _userManager.ResetPasswordAsync(user, token, model.Password);
                    }
                }
            }

            return Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing admin accountant for school {SchoolId}", schoolId);
            return Ok(false);
        }
    }

    private static CreateAdminAccountantResultDto FailAdminAccountant(string code, string message, string? redirectPath = null) =>
        new()
        {
            Success = false,
            Code = code,
            Message = message,
            RedirectPath = redirectPath
        };

    private static string? ValidateSignature(IFormFile signature)
    {
        var fileSizeKb = signature.Length / 1024;
        if (fileSizeKb > 13)
            return "Your signature size is too big. The maximum size is 10kb";

        if (!AllowedContentTypes.Contains(signature.ContentType))
            return "Only 'JPEG' and 'png' image are allowed.";

        return null;
    }

    private string? ValidateSignatureImagePayload(string? signatureImageString)
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

        if (!AllowedContentTypes.Contains(contentType))
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

    private async Task<string> NormalizeSignatureImageAsync(string? signatureImageString, CancellationToken ct, string? folderName = null)
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

            var filePath = Path.Combine(folder, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, bytes, ct);
            return fileName;
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid base64 signature image payload received.");
            return string.Empty;
        }
    }

    private void DeleteNormalizedSignatureIfExists(string? fileName, string? folderName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var fullPath = Path.Combine(webRoot, "Signatures", fileName);
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }

    private string? GetCurrentUserId()
    {
        return User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.PrimarySid)?.Value
            ?? User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value
            ?? User.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
    }

    private static bool TryParseEnumValue<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse(value?.Trim(), true, out parsed) && Enum.IsDefined(typeof(TEnum), parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }

    private TeacherRegistrationRequest MapTeacherRegistrationRequest(TxEmployee teacher)
    {
        var user = teacher.User;

        return new TeacherRegistrationRequest
        {
            EmployeeIdNo = teacher.EmployeeIdNo,
            FirstName = user?.FirstName ?? string.Empty,
            MiddleName = user?.MiddleName ?? string.Empty,
            SurName = user?.SurName ?? string.Empty,
            UserName = user?.UserName ?? string.Empty,
            Email = user?.Email ?? string.Empty,
            PhoneNumber = user?.PhoneNumber ?? string.Empty,
            DateOfBirth = teacher.DateOfBirth,
            Gender = user?.Gender.ToString() ?? string.Empty,
            EmployeeStatus = teacher.EmployeeStatus.ToString(),
            SignatureImageString = teacher.SignatureImageString,
        };
    }

    private async Task<IdentityResult> SyncTeacherRoleAsync(ApplicationUser user, EmployeeStatus employeeStatus)
    {
        if (employeeStatus == EmployeeStatus.Teacher)
        {
            if (!await _roleManager.RoleExistsAsync("Teacher"))
            {
                var roleCreateResult = await _roleManager.CreateAsync(new IdentityRole("Teacher"));
                if (!roleCreateResult.Succeeded)
                {
                    return roleCreateResult;
                }
            }

            if (!await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                return await _userManager.AddToRoleAsync(user, "Teacher");
            }

            return IdentityResult.Success;
        }

        if (await _userManager.IsInRoleAsync(user, "Teacher"))
        {
            return await _userManager.RemoveFromRoleAsync(user, "Teacher");
        }

        return IdentityResult.Success;
    }

    private static string AggregateIdentityErrors(IdentityResult result)
    {
        return string.Join(" ", result.Errors.Select(x => x.Description));
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

    private StudentDetailsDto BuildStudentDetailsDto(
        StudentRegistrationRequest model,
        string userId,
        string passportFileName,
        string termId)
    {
        return new StudentDetailsDto
        {
            SNo = model.SNo,
            IdNo = NormalizeLegacyText(model.IdNo),
            FirstName = NormalizeLegacyText(model.FirstName),
            MiddleName = NormalizeLegacyText(model.MiddleName),
            SurName = NormalizeLegacyText(model.SurName),
            DateOfBirth = EnsureUtcDateTime(model.DateOfBirth),
            PlaceOfBirth = model.PlaceOfBirth?.Trim() ?? string.Empty,
            Religion = model.Religion,
            Gender = model.Gender,
            BloodGroup = model.BloodGroup?.Trim() ?? string.Empty,
            DateOfAdmsn = EnsureUtcDateTime(model.DateOfAdmsn),
            TransferedFromWhere = model.TransferedFromWhere?.Trim() ?? string.Empty,
            StartClass = model.ClassLabel,
            CurrentClass = model.CurrentClass,
            Passport = passportFileName,
            ClassArmId = !string.IsNullOrWhiteSpace(model.ClassArmId) ? model.ClassArmId.Trim() : model.ArmId.Trim(),
            GuardiansName = model.GuardiansName?.Trim() ?? string.Empty,
            Relationship = model.Relationship?.Trim() ?? string.Empty,
            GuardianAddress = model.GuardianAddress?.Trim() ?? string.Empty,
            Occupation = model.Occupation?.Trim() ?? string.Empty,
            MobileNumber = model.MobileNumber?.Trim() ?? string.Empty,
            HomeNumber = model.HomeNumber?.Trim() ?? string.Empty,
            EmailAddress = model.EmailAddress?.Trim() ?? string.Empty,
            MailingAddress = string.Empty,
            StudentMobileNo = model.StudentMobileNo?.Trim() ?? string.Empty,
            Country = model.Country?.Trim() ?? string.Empty,
            UserId = userId,
            ClassCategory = model.ClassCategory,
            Status = model.Status != StudentStatus.Previous ? model.Status : StudentStatus.Returning,
            StudentInitialRegStatus = model.Status == StudentStatus.Fresher
                ? StudentInitialRegistrationStatus.Fresher
                : StudentInitialRegistrationStatus.Returning,
            AdmisnTermId = termId,
        };
    }

    private static string NormalizeLegacyText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("\\", string.Empty).Trim();
    }

    private static DateTime EnsureUtcDateTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private async Task<string> GenerateAvailableStudentUserNameAsync(string requestedUserName, CancellationToken ct)
    {
        var candidate = requestedUserName;
        if (!await _userRegistrationService.DoesThisUserNameExist(candidate, ct))
        {
            return candidate;
        }

        var counter = 0;
        do
        {
            counter += 1;
            candidate = $"{requestedUserName}e{counter}";
        }
        while (await _userRegistrationService.DoesThisUserNameExist(candidate, ct));

        return candidate;
    }

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

        var fileSizeKb = passport.Length / 1024d;
        if (fileSizeKb > 14)
        {
            return "Your passport size is too big. The maximum size is 14kb";
        }

        var contentType = (passport.ContentType ?? string.Empty).Trim();
        var fileName = passport.FileName ?? string.Empty;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var looksLikeAllowedImage =
            AllowedContentTypes.Contains(contentType) ||
            string.Equals(contentType, "image/jpg", StringComparison.OrdinalIgnoreCase) ||
            extension is ".jpg" or ".jpeg" or ".png" ||
            (string.IsNullOrWhiteSpace(contentType) && string.IsNullOrWhiteSpace(fileName));

        if (!looksLikeAllowedImage)
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

    private async Task<string> SaveSignatureAsync(IFormFile signature, CancellationToken ct)
    {
        var extension = Path.GetExtension(signature.FileName);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
        var folder = Path.Combine(webRoot, "Signatures");

        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, fileName);
        await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await signature.CopyToAsync(stream, ct);

        return fileName;
    }

    private void DeleteSignatureIfExists(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
        var fullPath = Path.Combine(webRoot, "Signatures", fileName);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    private static object GetNextAction(SchoolRegistrarRequest model, byte creatorStatus, string registrationOfficerId)
    {
        return creatorStatus switch
        {
            1 => new
            {
                Controller = "Sadmin",
                Area = "SupperAdmin",
                Action = "SchoolDefaultRegistration",
                RouteValues = new
                {
                    schoolName = model.SchoolName,
                    registrationOfficerId,
                    token = model.SmsApiToken,
                    key = model.SmsApiKey
                }
            },
            2 => new
            {
                Controller = "Ragent",
                Area = "RemoteAgent",
                Action = "SchoolDefaultRegistration",
                RouteValues = new
                {
                    schoolName = model.SchoolName,
                    registrationOfficerId,
                    token = model.SmsApiToken,
                    key = model.SmsApiKey
                }
            },
            3 => new
            {
                Controller = "Lagent",
                Area = "LocalAgent",
                Action = "SchoolDefaultRegistration",
                RouteValues = new
                {
                    schoolName = model.SchoolName,
                    registrationOfficerId,
                    token = model.SmsApiToken,
                    key = model.SmsApiKey
                }
            },
            _ => new
            {
                Controller = "Registry",
                Area = "Registry",
                Action = "SchoolDefaultRegistration",
                RouteValues = new
                {
                    schoolName = model.SchoolName,
                    token = model.SmsApiToken,
                    key = model.SmsApiKey
                }
            }
        };
    }

    private sealed class RegistrationRejectedException : Exception
    {
        public RegistrationRejectedException(string message) : base(message)
        {
        }
    }
}