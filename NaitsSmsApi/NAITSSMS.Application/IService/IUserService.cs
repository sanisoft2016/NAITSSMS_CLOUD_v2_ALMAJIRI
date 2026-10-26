using CustRewardMgtSys.Application.DTOs;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Infrastructure.Repo;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace CustRewardMgtSys.Application.IService
{
    public interface IUserService
    {
        Task<object> RegisterUser(ApplicationUserDto userObj);
        Task<object> Login(LoginDto model);
        Task<ResponseDto> CreateUserAsync(ApplicationUser user, string password);
        Task<List<ApplicationUserDto>> GetAllAdminUsers();
        Task<bool> DeleteUserById(string userId);
        Task<object> ChangePassword(ChangePasswordDto userObj, string userId);
        string GetUserNameByUserId(string userId);
        Task<ReturnCreateSchoolRegistrarDto> CreateSchoolRegistrar(UserDto2nd model, CancellationToken ct = default);
        Task<object> GetCurrentUser(string userId);
        Task<object> CompleteTwoFactorLogin(TwoFactorLoginDto model);
        Task<object> GetTwoFactorStatus(string userId);
        Task<object> BeginTwoFactorSetup(string userId);
        Task<object> EnableTwoFactor(string userId, TwoFactorCodeDto model);
        Task<object> DisableTwoFactor(string userId, TwoFactorDisableDto model);
    }
}
