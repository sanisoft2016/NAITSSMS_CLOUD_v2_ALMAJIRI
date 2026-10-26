using CustRewardMgtSys.Application.DTOs;
using CustRewardMgtSys.Application.IService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NAITSSMS.Application.Configuration;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Infrastructure.Unitofwork;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace CustRewardMgtSys.Application.Service
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;
        private readonly ILoginThrottleService _loginThrottle;
        private readonly ITwoFactorLoginTicketService _twoFactorTickets;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private static readonly string[] AdminTwoFactorRoles =
        [
            "SAOfficer",
            "Registry",
            "HeadAdmin",
            "Admin",
            "Accounting",
            "Accountant"
        ];

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger<UserService> logger,
            IHttpContextAccessor httpContextAccessor,
            ILoginThrottleService loginThrottle,
            ITwoFactorLoginTicketService twoFactorTickets)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _loginThrottle = loginThrottle;
            _twoFactorTickets = twoFactorTickets;
        }

        private static string AggregateErrors(IEnumerable<IdentityError> errors) =>
            string.Join("; ", errors.Select(e => e.Description));

        public async Task<List<ApplicationUserDto>> GetAllAdminUsers()
        {
            _logger.LogInformation("Fetching all admin users");
            var users = await _userManager.Users.ToListAsync();
            _logger.LogInformation("Fetched {Count} users", users.Count);

            return users.Select(x => new ApplicationUserDto
            {
                Email = x.Email,
                FirstName = x.FirstName,
                SurName = x.SurName,
                Gender = x.Gender,
                Id = x.Id,
                PhoneNumber = x.PhoneNumber,
                UserName = x.UserName
            }).ToList();
        }

        public async Task<ResponseDto> CreateUserAsync(ApplicationUser userObj, string password)
        {
            _logger.LogInformation("Creating user {UserName}", userObj.UserName);

            try
            {
                userObj.NormalizedUserName = userObj.UserName?.ToUpperInvariant();
                userObj.NormalizedEmail = userObj.Email?.ToUpperInvariant();

                var create = await _userManager.CreateAsync(userObj, password);
                if (!create.Succeeded)
                {
                    var errs = AggregateErrors(create.Errors);
                    _logger.LogWarning("Failed to create user {UserName}: {Errors}", userObj.UserName, errs);
                    return new ResponseDto { Status = "Failure", Data = errs };
                }

                const string roleName = "PaintBuyer";
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var roleCreate = await _roleManager.CreateAsync(new IdentityRole(roleName));
                    if (!roleCreate.Succeeded)
                    {
                        var errs = AggregateErrors(roleCreate.Errors);
                        _logger.LogWarning("Role creation failed for {Role}: {Errors}", roleName, errs);
                        return new ResponseDto { Status = "Failure", Data = errs };
                    }
                }

                var addRole = await _userManager.AddToRoleAsync(userObj, roleName);
                if (!addRole.Succeeded)
                {
                    var errs = AggregateErrors(addRole.Errors);
                    _logger.LogWarning("Failed to assign role {Role} to {UserName}: {Errors}", roleName, userObj.UserName, errs);
                    return new ResponseDto { Status = "Failure", Data = errs };
                }

                _logger.LogInformation("User {UserName} created and assigned role {Role}", userObj.UserName, roleName);
                return new ResponseDto { Status = "Success", Data = "User created successfully." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating user {UserName}", userObj.UserName);
                return new ResponseDto { Status = "Failure", Data = "Unexpected error occurred while creating user." };
            }
        }

        public async Task<object> RegisterUser(ApplicationUserDto dto)
        {
            _logger.LogInformation("Register user request for {UserName}", dto.UserName);

            try
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    var existing = await _userManager.FindByNameAsync(dto.UserName);
                    if (existing != null)
                    {
                        _logger.LogWarning("User {UserName} already exists", dto.UserName);
                        return new ResponseDto { Status = "Failure", Data = "User already exists!" };
                    }

                    var user = new ApplicationUser
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserName = dto.UserName,
                        NormalizedUserName = dto.UserName?.ToUpperInvariant(),
                        Email = dto.Email,
                        NormalizedEmail = dto.Email?.ToUpperInvariant(),
                        FirstName = dto.FirstName,
                        SurName = dto.SurName,
                        PhoneNumber = dto.PhoneNumber,
                        Gender = dto.Gender,
                        SecurityStamp = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow
                    };

                    var create = await _userManager.CreateAsync(user, dto.PasswordHash);
                    if (!create.Succeeded)
                    {
                        var errs = AggregateErrors(create.Errors);
                        _logger.LogWarning("Failed to register user {UserName}: {Errors}", dto.UserName, errs);
                        return new ResponseDto { Status = "Failure", Data = errs };
                    }

                    const string roleName = "PaintBuyer";
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        var rc = await _roleManager.CreateAsync(new IdentityRole(roleName));
                        if (!rc.Succeeded)
                        {
                            var errs = AggregateErrors(rc.Errors);
                            _logger.LogWarning("Role creation failed: {Errors}", errs);
                            return new ResponseDto { Status = "Failure", Data = errs };
                        }
                    }

                    var addRole = await _userManager.AddToRoleAsync(user, roleName);
                    if (!addRole.Succeeded)
                    {
                        var errs = AggregateErrors(addRole.Errors);
                        _logger.LogWarning("Failed to assign role {Role} to {UserName}: {Errors}", roleName, dto.UserName, errs);
                        return new ResponseDto { Status = "Failure", Data = errs };
                    }

                    _logger.LogInformation("User {UserName} registered successfully", dto.UserName);
                    return new ResponseDto { Status = "Success", Data = "The User has successfully been created!" };
                }

                var existingUser = await _userManager.FindByNameAsync(dto.UserName);
                if (existingUser == null)
                {
                    _logger.LogWarning("User {UserName} not found for update", dto.UserName);
                    return new ResponseDto { Status = "Failure", Data = "User not found!" };
                }

                existingUser.Email = dto.Email;
                existingUser.NormalizedEmail = dto.Email?.ToUpperInvariant();
                existingUser.PhoneNumber = dto.PhoneNumber;
                existingUser.UserName = dto.UserName;
                existingUser.NormalizedUserName = dto.UserName?.ToUpperInvariant();
                existingUser.EmailConfirmed = true;
                existingUser.SurName = dto.SurName;
                existingUser.FirstName = dto.FirstName;
                existingUser.Gender = dto.Gender;

                var update = await _userManager.UpdateAsync(existingUser);
                if (!update.Succeeded)
                {
                    var errs = AggregateErrors(update.Errors);
                    _logger.LogWarning("Failed to update user {UserName}: {Errors}", dto.UserName, errs);
                    return new ResponseDto { Status = "Failure", Data = errs };
                }

                _logger.LogInformation("User {UserName} updated successfully", dto.UserName);
                return new ResponseDto { Status = "Success", Data = "User has successfully been updated!" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error registering/updating user {UserName}", dto.UserName);
                return new ResponseDto { Status = "Failure", Data = "Unexpected error occurred while processing the request." };
            }
        }

        public async Task<object> Login(LoginDto model)
        {
            _logger.LogInformation("Login attempt for {UserName}", model.UserName);

            try
            {
                var userName = (model.UserName ?? string.Empty).Trim();
                var clientIp = ResolveClientIp();
                var ipKey = $"ip:{clientIp}";
                var userKey = $"user:{userName}";

                if (_loginThrottle.IsLockedOut(ipKey, out var ipRemaining))
                {
                    return LoginFailure(
                        FormatLockoutMessage("Too many failed login attempts from this network address", ipRemaining));
                }

                if (!string.IsNullOrWhiteSpace(userName) &&
                    _loginThrottle.IsLockedOut(userKey, out var userRemaining))
                {
                    return LoginFailure(
                        FormatLockoutMessage("Too many failed login attempts for this username", userRemaining));
                }

                var user = string.IsNullOrWhiteSpace(userName)
                    ? null
                    : await _userManager.FindByNameAsync(userName);

                if (user != null)
                {
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        var remaining = user.LockoutEnd.HasValue
                            ? user.LockoutEnd.Value - DateTimeOffset.UtcNow
                            : (TimeSpan?)null;
                        return LoginFailure(
                            FormatLockoutMessage("This account is temporarily locked due to failed login attempts", remaining));
                    }
                }

                var passwordValid = user != null && await _userManager.CheckPasswordAsync(user, model.Password);
                if (!passwordValid)
                {
                    _logger.LogWarning("Login failed for {UserName} from {ClientIp}", userName, clientIp);

                    _loginThrottle.RecordFailure(ipKey, out var ipLockRemaining);
                    if (!string.IsNullOrWhiteSpace(userName))
                    {
                        _loginThrottle.RecordFailure(userKey, out var userLockRemaining);
                        if (userLockRemaining.HasValue)
                        {
                            return LoginFailure(
                                FormatLockoutMessage("Too many failed login attempts for this username", userLockRemaining));
                        }
                    }

                    if (user != null)
                    {
                        if (!user.LockoutEnabled)
                        {
                            await _userManager.SetLockoutEnabledAsync(user, true);
                        }

                        await _userManager.AccessFailedAsync(user);
                        if (await _userManager.IsLockedOutAsync(user))
                        {
                            var remaining = user.LockoutEnd.HasValue
                                ? user.LockoutEnd.Value - DateTimeOffset.UtcNow
                                : (TimeSpan?)null;
                            return LoginFailure(
                                FormatLockoutMessage("This account is temporarily locked due to failed login attempts", remaining));
                        }
                    }

                    if (ipLockRemaining.HasValue)
                    {
                        return LoginFailure(
                            FormatLockoutMessage("Too many failed login attempts from this network address", ipLockRemaining));
                    }

                    return LoginFailure("Wrong username or password.");
                }

                await _userManager.ResetAccessFailedCountAsync(user!);
                if (user!.LockoutEnd != null)
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                }

                _loginThrottle.Reset(ipKey);
                _loginThrottle.Reset(userKey);

                var roles = await _userManager.GetRolesAsync(user);
                if (user.TwoFactorEnabled && IsAdminTwoFactorRole(roles))
                {
                    var twoFactorToken = _twoFactorTickets.CreateTicket(user.Id);
                    return new
                    {
                        Status = "RequiresTwoFactor",
                        Data = new
                        {
                            twoFactorToken,
                            message = "Enter the 6-digit code from your authenticator app."
                        }
                    };
                }

                return await IssueSuccessfulLoginAsync(user, roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for {UserName}", model.UserName);
                return new { Status = "Failure", Data = "Unexpected error occurred during login." };
            }
        }

        public async Task<object> CompleteTwoFactorLogin(TwoFactorLoginDto model)
        {
            try
            {
                if (!_twoFactorTickets.TryGetTicket(model.TwoFactorToken, out var userId))
                {
                    return LoginFailure("Your verification session expired. Please sign in again.");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.TwoFactorEnabled)
                {
                    _twoFactorTickets.ConsumeTicket(model.TwoFactorToken);
                    return LoginFailure("Two-factor authentication is not available for this account.");
                }

                var roles = await _userManager.GetRolesAsync(user);
                if (!IsAdminTwoFactorRole(roles))
                {
                    _twoFactorTickets.ConsumeTicket(model.TwoFactorToken);
                    return LoginFailure("Two-factor authentication is not required for this account.");
                }

                var code = NormalizeOtpCode(model.Code);
                var valid = await _userManager.VerifyTwoFactorTokenAsync(
                    user,
                    _userManager.Options.Tokens.AuthenticatorTokenProvider,
                    code);

                if (!valid)
                {
                    _logger.LogWarning("Invalid 2FA code for user {UserId}", userId);
                    return LoginFailure("Invalid authenticator code.");
                }

                _twoFactorTickets.ConsumeTicket(model.TwoFactorToken);
                return await IssueSuccessfulLoginAsync(user, roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during 2FA login completion");
                return LoginFailure("Unexpected error occurred during two-factor verification.");
            }
        }

        public async Task<object> GetTwoFactorStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new { Status = "Failed", Data = "User not found." };
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = IsAdminTwoFactorRole(roles);
            return new
            {
                Status = "Success",
                Data = new
                {
                    enabled = user.TwoFactorEnabled,
                    canManage,
                    isOptional = true,
                    roles
                }
            };
        }

        public async Task<object> BeginTwoFactorSetup(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new { Status = "Failed", Data = "User not found." };
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!IsAdminTwoFactorRole(roles))
            {
                return new { Status = "Failed", Data = "Two-factor authentication is only available for admin roles." };
            }

            await _userManager.ResetAuthenticatorKeyAsync(user);
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(key))
            {
                return new { Status = "Failed", Data = "Unable to generate authenticator key." };
            }

            var email = user.Email ?? user.UserName ?? user.Id;
            var issuer = _configuration["TwoFactor:Issuer"] ?? "NAITS-ASMS";
            var authenticatorUri =
                $"otpauth://totp/{UrlEncoder.Default.Encode(issuer)}:{UrlEncoder.Default.Encode(email)}?secret={key}&issuer={UrlEncoder.Default.Encode(issuer)}&digits=6";

            return new
            {
                Status = "Success",
                Data = new
                {
                    sharedKey = FormatAuthenticatorKey(key),
                    authenticatorUri,
                    enabled = user.TwoFactorEnabled,
                    message = "Scan the QR code or enter the key in your authenticator app, then confirm with a 6-digit code."
                }
            };
        }

        public async Task<object> EnableTwoFactor(string userId, TwoFactorCodeDto model)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new { Status = "Failed", Data = "User not found." };
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!IsAdminTwoFactorRole(roles))
            {
                return new { Status = "Failed", Data = "Two-factor authentication is only available for admin roles." };
            }

            var code = NormalizeOtpCode(model.Code);
            var valid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                code);

            if (!valid)
            {
                return new { Status = "Failed", Data = "Invalid authenticator code. Setup was not enabled." };
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            return new
            {
                Status = "Success",
                Data = new
                {
                    enabled = true,
                    message = "Two-factor authentication is now enabled for your admin account."
                }
            };
        }

        public async Task<object> DisableTwoFactor(string userId, TwoFactorDisableDto model)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new { Status = "Failed", Data = "User not found." };
            }

            if (!user.TwoFactorEnabled)
            {
                return new { Status = "Success", Data = new { enabled = false, message = "Two-factor authentication is already off." } };
            }

            var code = NormalizeOtpCode(model.Code);
            var valid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                code);

            if (!valid)
            {
                return new { Status = "Failed", Data = "Invalid authenticator code. Two-factor authentication was not disabled." };
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);

            return new
            {
                Status = "Success",
                Data = new
                {
                    enabled = false,
                    message = "Two-factor authentication has been disabled."
                }
            };
        }

        private async Task<object> IssueSuccessfulLoginAsync(ApplicationUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.PrimarySid, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            };
            foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

            int tokenValidity = int.Parse(_configuration["JWT:TokenValidityInMinutes"]!);
            var validTo = DateTime.UtcNow.AddMinutes(tokenValidity);
            var token = GetToken(claims, tokenValidity, validTo);

            var returnToken = new TokenDto
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = tokenValidity,
                Exp = new DateTimeOffset(validTo).ToUnixTimeSeconds()
            };

            var data = new
            {
                token = returnToken,
                role = roles.ToList()
            };

            return new { Status = "Success", Data = data };
        }

        private static bool IsAdminTwoFactorRole(IEnumerable<string> roles)
        {
            return roles.Any(role =>
                AdminTwoFactorRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        private static string NormalizeOtpCode(string? code)
        {
            return (code ?? string.Empty).Replace(" ", string.Empty).Trim();
        }

        private static string FormatAuthenticatorKey(string unformattedKey)
        {
            var result = new StringBuilder();
            var currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }

            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        private string ResolveClientIp()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return "unknown";
            }

            var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first;
                }
            }

            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static object LoginFailure(string message)
        {
            return new
            {
                Status = "Failure",
                Data = new
                {
                    token = "",
                    role = "",
                    message
                }
            };
        }

        private static string FormatLockoutMessage(string prefix, TimeSpan? remaining)
        {
            if (!remaining.HasValue || remaining.Value <= TimeSpan.Zero)
            {
                return $"{prefix}. Please try again later.";
            }

            var minutes = Math.Max(1, (int)Math.Ceiling(remaining.Value.TotalMinutes));
            return $"{prefix}. Try again in about {minutes} minute(s).";
        }

        public async Task<bool> DeleteUserById(string userId)
        {
            _logger.LogInformation("Deleting user {UserId}", userId);

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Delete failed: user {UserId} not found", userId);
                    return false;
                }

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Count > 0)
                {
                    var remove = await _userManager.RemoveFromRolesAsync(user, roles);
                    if (!remove.Succeeded)
                    {
                        _logger.LogWarning("Failed removing roles for {UserId}: {Errors}", userId, AggregateErrors(remove.Errors));
                        return false;
                    }
                }

                var delete = await _userManager.DeleteAsync(user);
                _logger.LogInformation("User {UserId} delete result: {Result}", userId, delete.Succeeded);
                return delete.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting user {UserId}", userId);
                return false;
            }
        }

        public async Task<object> ChangePassword(ChangePasswordDto dto, string userId)
        {
            _logger.LogInformation("Change password request for {UserId}", userId);

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Change password failed: user {UserId} not found", userId);
                    return new { Status = "Failed", Data = "User not found." };
                }

                var hasher = new PasswordHasher<ApplicationUser>();
                var verify = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.currentPassword);
                if (verify != PasswordVerificationResult.Success)
                {
                    _logger.LogWarning("Change password failed: wrong old password for {UserId}", userId);
                    return new { Status = "Failed", Data = "Wrong Old Password!" };
                }

                user.PasswordHash = hasher.HashPassword(user, dto.newPassword);
                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded)
                {
                    var errs = AggregateErrors(update.Errors);
                    _logger.LogWarning("Change password failed for {UserId}: {Errors}", userId, errs);
                    return new { Status = "Failed", Data = errs };
                }

                _logger.LogInformation("Password changed successfully for {UserId}", userId);
                return new { Status = "Success", Data = "Password changed successfully." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error changing password for {UserId}", userId);
                return new { Status = "Failed", Data = "Unexpected error occurred while changing password." };
            }
        }

        public string GetUserNameByUserId(string userId)
        {
            _logger.LogInformation("Get username by id {UserId}", userId);
            try
            {
                return _userManager.FindByIdAsync(userId).Result?.UserName ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting username for {UserId}", userId);
                return string.Empty;
            }
        }

        private string GetToken(List<Claim> authClaims, int tokenValidity, DateTime validTo)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtConfiguration.ResolveSecret(_configuration)));

            var token = new JwtSecurityToken(
                issuer: JwtConfiguration.ResolveIssuer(_configuration),
                audience: JwtConfiguration.ResolveAudience(_configuration),
                expires: validTo,
                claims: authClaims,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        private static byte ResolveCreatorStatus(ClaimsPrincipal principal, string agentUserId)
        {
            if (string.IsNullOrWhiteSpace(agentUserId))
                return 4;

            if (principal.IsInRole("SAOfficer"))
                return 1;

            if (principal.IsInRole("RemoteAgents"))
                return 2;

            return 3;
        }

        public Task<ReturnCreateSchoolRegistrarDto> CreateSchoolRegistrar(UserDto2nd model, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public async Task<object> GetCurrentUser(string userId)
        {
            _logger.LogInformation("Fetching current user profile for {UserId}", userId);
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return new { Status = "Failed", Data = (object)null };

                var roles = await _userManager.GetRolesAsync(user);

                return new
                {
                    status = "Success",
                    data = new
                    {
                        id = user.Id,
                        name = $"{user.FirstName} {user.SurName}".Trim(),
                        firstName = user.FirstName,
                        middleName = user.MiddleName,
                        surName = user.SurName,
                        userName = user.UserName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        roles = roles,
                        avatar = ResolveProfileAvatarUrl(user.Id),
                        twoFactorEnabled = user.TwoFactorEnabled,
                        canManageTwoFactor = IsAdminTwoFactorRole(roles)
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current user profile for {UserId}", userId);
                return new { Status = "Failed", Data = (object)null };
            }
        }

        private static string ResolveProfileAvatarUrl(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return string.Empty;
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "profile-avatars");
            if (!Directory.Exists(uploadDir))
            {
                return string.Empty;
            }

            foreach (var extension in new[] { ".jpg", ".jpeg", ".png" })
            {
                var fileName = $"{userId}{extension}";
                if (File.Exists(Path.Combine(uploadDir, fileName)))
                {
                    return $"/profile-avatars/{fileName}";
                }
            }

            return string.Empty;
        }
    }
}