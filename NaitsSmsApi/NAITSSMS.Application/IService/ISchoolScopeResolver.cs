using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface ISchoolScopeResolver
    {
        /// <summary>
        /// Resolves the school registration officer id (SchoolInfoId) for the given user.
        /// </summary>
        Task<string> ResolveSchoolIdAsync(string userId, CancellationToken ct = default);
    }
}
