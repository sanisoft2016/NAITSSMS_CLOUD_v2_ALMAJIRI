using NAITSSMS.Application.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface IRegistryOtherMethodService
    {
        Task<AccessVerifyReturnValues?> B(CancellationToken ct = default);
        Task<string> GetAndEncodeCredentials(CancellationToken ct = default);
        Task<string> GetAndEncodeCredentialsAndExpiryDate(CancellationToken ct = default);
        Task<SchoolInfoDto> GetSystemInfoForSms(CancellationToken ct = default);
    }
}
