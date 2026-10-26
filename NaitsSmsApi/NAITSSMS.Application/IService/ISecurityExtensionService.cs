using NAITSSMS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface ISecurityExtensionService
    {
        Task<string> GetAndEncodeCredentials(string userId, CancellationToken ct = default);
        Task<AccessVerifyReturnValues?> B(string userId, CancellationToken ct = default);
        Task<string> GetAndEncodeCredentialsAndExpiryDate(string userId, CancellationToken ct = default);
        Task<SchoolInfoDto> GetSystemInfoForSms(string userId, CancellationToken ct = default);

    }
}
