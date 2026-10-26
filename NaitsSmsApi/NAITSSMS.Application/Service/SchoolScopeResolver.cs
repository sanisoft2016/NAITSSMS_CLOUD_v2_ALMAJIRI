using Microsoft.Extensions.Caching.Memory;
using NAITSSMS.Application.IService;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class SchoolScopeResolver : ISchoolScopeResolver
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        private readonly IUnitOfWork _uow;
        private readonly IMemoryCache _cache;

        public SchoolScopeResolver(IUnitOfWork uow, IMemoryCache cache)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<string> ResolveSchoolIdAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("Unable to resolve the current user for school scope.");
            }

            var cacheKey = $"school-scope:{userId.Trim()}";
            if (_cache.TryGetValue(cacheKey, out string? cachedSchoolId) && !string.IsNullOrWhiteSpace(cachedSchoolId))
            {
                return cachedSchoolId;
            }

            var schoolId = await RegistrySchoolLookup.ResolveSchoolRegistrationOfficerIdAsync(_uow, userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                throw new InvalidOperationException("School registration profile was not found for the current user.");
            }

            _cache.Set(cacheKey, schoolId.Trim(), CacheDuration);
            return schoolId.Trim();
        }
    }
}
