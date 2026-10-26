using CustRewardMgtSys.Application.DTOs;
using CustRewardMgtSys.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.Application.DTOs;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace CustRewardMgtSys.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private const long MaxProfileImageBytes = 100 * 1024;
        private static readonly string[] AllowedProfileImageExtensions = [".jpg", ".jpeg", ".png"];

        private readonly IServiceProvider _provider;
        private readonly IWebHostEnvironment _environment;

        public UserController(IServiceProvider provider, IWebHostEnvironment environment)
        {
            _provider = provider;
            _environment = environment;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { Status = "Failed", Data = "Unable to resolve the current user." });
            }

            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            var response = await userService.GetCurrentUser(userId);
            return Ok(response);
        }

        [HttpPost("profile-image")]
        [Authorize]
        [RequestSizeLimit(MaxProfileImageBytes + 8192)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxProfileImageBytes + 8192)]
        public async Task<IActionResult> UploadProfileImage(IFormFile? profileImage, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { Status = "Failed", Data = "Unable to resolve the current user." });
            }

            if (profileImage == null || profileImage.Length == 0)
            {
                return BadRequest(new { Status = "Failed", Data = "Please choose a profile image to upload." });
            }

            if (profileImage.Length > MaxProfileImageBytes)
            {
                return BadRequest(new { Status = "Failed", Data = "Profile image must be 100kb or smaller." });
            }

            var extension = Path.GetExtension(profileImage.FileName)?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedProfileImageExtensions.Contains(extension))
            {
                return BadRequest(new { Status = "Failed", Data = "Only JPG and PNG images are allowed." });
            }

            if (!IsAllowedProfileImageContentType(profileImage.ContentType, extension))
            {
                return BadRequest(new { Status = "Failed", Data = "Invalid image content type. Use JPG or PNG." });
            }

            var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;
            var uploadDir = Path.Combine(webRoot, "profile-avatars");
            Directory.CreateDirectory(uploadDir);

            foreach (var stale in Directory.EnumerateFiles(uploadDir, $"{userId}.*"))
            {
                try { System.IO.File.Delete(stale); } catch { /* ignore */ }
            }

            var storedFileName = $"{userId}{extension}";
            var physicalPath = Path.Combine(uploadDir, storedFileName);
            await using (var stream = System.IO.File.Create(physicalPath))
            {
                await profileImage.CopyToAsync(stream, cancellationToken);
            }

            var relativeUrl = $"/profile-avatars/{storedFileName}";
            return Ok(new { Status = "Success", Data = new { avatar = relativeUrl } });
        }

        private static bool IsAllowedProfileImageContentType(string? contentType, string extension)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return extension is ".jpg" or ".jpeg" or ".png";
            }

            var normalized = contentType.Trim().ToLowerInvariant();
            return normalized is "image/jpeg" or "image/jpg" or "image/pjpeg" or "image/png";
        }

        [HttpPost]
        [Route("register-user")]
        [Authorize(Roles = "HeadAdmin,Admin")]
        public async Task<IActionResult> RegisterUser([FromBody] ApplicationUserDto userObj)
        {
            //var loggedInUserId = GetUserId();
            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            //var userName = userService.GetUserNameByUserId(loggedInUserId);

            //if (userName == "HeadAdmin")
            //{
                var response = await userService.RegisterUser(userObj);
                return Ok(response);
            //}
            //else
            //{
            //    return Ok(MainAdminUnauthorizedRequestMessageObject());
            //}
        }

        [HttpGet]
        [Route("verify-access-right")]
        [Authorize(Roles = "HeadAdmin,Admin")]
        //[AllowAnonymous]
        public IActionResult VerifyAccessRight()
        {
            //var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            //string loggedInUserId = GetUserId();
            //var userName = userService.GetUserNameByUserId(loggedInUserId);
            return Ok(new { Status = "Success", Data = true });
        }
        private string GetUserId()
        {
            return User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.PrimarySid)?.Value
                ?? User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value
                ?? User.Claims.FirstOrDefault(x => x.Type == "sub")?.Value
                ?? string.Empty;
        }
        private object MainAdminUnauthorizedRequestMessageObject()
        {
            return new { Status = "Failed", Data = "Failed! You are not authorized to access this location." };
        }
        [HttpPost]
        [Route("post-change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto userObj)
        {
            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            string userId = GetUserId();
            var response = await userService.ChangePassword(userObj, userId);
            return Ok(response);
        }

        [HttpPost, Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            object response = await userService.Login(model);
            return Ok(response);
        }

        [HttpPost("login-2fa")]
        public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginDto model)
        {
            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            var response = await userService!.CompleteTwoFactorLogin(model);
            return Ok(response);
        }

        [HttpGet("2fa/status")]
        [Authorize]
        public async Task<IActionResult> GetTwoFactorStatus()
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { Status = "Failed", Data = "Unable to resolve the current user." });
            }

            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            return Ok(await userService!.GetTwoFactorStatus(userId));
        }

        [HttpPost("2fa/setup/start")]
        [Authorize]
        public async Task<IActionResult> BeginTwoFactorSetup()
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { Status = "Failed", Data = "Unable to resolve the current user." });
            }

            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            return Ok(await userService!.BeginTwoFactorSetup(userId));
        }

        [HttpPost("2fa/setup/enable")]
        [Authorize]
        public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorCodeDto model)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { Status = "Failed", Data = "Unable to resolve the current user." });
            }

            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            return Ok(await userService!.EnableTwoFactor(userId, model));
        }

        [HttpPost("2fa/disable")]
        [Authorize]
        public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorDisableDto model)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { Status = "Failed", Data = "Unable to resolve the current user." });
            }

            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            return Ok(await userService!.DisableTwoFactor(userId, model));
        }

        [HttpGet("get-all-users")]
        public async Task<IActionResult> GetAllAdminUsers()
        {
            var userServices = _provider.GetService(typeof(IUserService)) as IUserService;
            var resultList = await userServices.GetAllAdminUsers();
            var returnObject = new { Status = "Success", Data = resultList };
            return Ok(returnObject);
        }

        [HttpGet("delete-user-by-id/{userId}")]
        [Authorize(Roles = "HeadAdmin,Admin")]
        public async Task<IActionResult> DeleteUserById(string userId)
        {
            var userService = _provider.GetService(typeof(IUserService)) as IUserService;
            var status = await userService.DeleteUserById(userId);
            var returnObject = new { Status = "Success", Data = status };
            return Ok(returnObject);
        }

        [HttpGet("menu")]
        [Authorize]
        public IActionResult GetMenu()
        {
            var menu = new object[]
            {
                new
                {
                    route = "dashboard",
                    name  = "dashboard",
                    type  = "link",
                    icon  = "dashboard"
                },
                new
                {
                    route    = "admin",
                    name     = "admin",
                    type     = "sub",
                    icon     = "settings",
                    children = new[]
                    {
                        new { route = "class-reg-index", name = "class-reg-index", type = "link" },
                        new { route = "class-reg",       name = "class-reg",       type = "link" },
                        new { route = "class-arm-index", name = "class-arm-index", type = "link" }
                    }
                },
                new
                {
                    route    = "profile",
                    name     = "profile",
                    type     = "sub",
                    icon     = "person",
                    children = new[]
                    {
                        new { route = "overview", name = "overview", type = "link" },
                        new { route = "change-profile-image", name = "change-profile-image", type = "link" }
                    }
                }
            };

            return Ok(new { menu });
        }
    }
}
