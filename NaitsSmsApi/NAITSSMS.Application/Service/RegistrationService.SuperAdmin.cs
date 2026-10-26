using Microsoft.AspNetCore.Identity;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public partial class RegistrationService
    {
        public async Task<SuperAdminDashboardDto> GetSuperAdminDashboard(CancellationToken ct = default)
        {
            var schools = await _uow.Repository<SchoolTyInfo, string>().GetAllAsync(ct);
            await HydrateSchoolOfficerGraphAsync(schools, ct);

            var currentLicenses = (await _uow.Repository<LicensTying>().WhereAsync(
                    license => license.TimeStatus == TimeStatus.Current,
                    ct))
                .GroupBy(license => license.SchoolSmsId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(license => license.ExpiryDate).First(),
                    StringComparer.OrdinalIgnoreCase);

            var today = DateTime.UtcNow.Date;
            var thirtyDaysFromNow = today.AddDays(30);
            var overviewRows = schools.Select(school =>
            {
                currentLicenses.TryGetValue(school.SchoolRegistrationOfficerId, out var license);
                var expiry = license?.ExpiryDate;
                var user = school.SchoolRegistrationOfficer?.User;
                var isDisabled = user?.AccessStatus == AccessStatus.Disable;
                var hasAccountant = school.SchoolAdminAccountantStatus == SchoolAdminAccountantStatus.Exist;

                var health = expiry switch
                {
                    null => "No subscription",
                    _ when expiry.Value.Date < today => "Expired",
                    _ when expiry.Value.Date <= thirtyDaysFromNow => "Expiring soon",
                    _ => "Active"
                };

                var priority = isDisabled
                    ? 4
                    : health is "Expired" or "No subscription"
                        ? 3
                        : health == "Expiring soon"
                            ? 2
                            : !hasAccountant ? 1 : 0;

                var registrarName = string.Join(
                    " ",
                    new[] { user?.FirstName, user?.MiddleName, user?.SurName }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));

                return new SuperAdminSchoolOverviewDto
                {
                    SchoolId = school.SchoolRegistrationOfficerId,
                    RegistrationNumber = school.SsNo,
                    SchoolName = school.SchoolName,
                    RegistrarUserId = user?.Id ?? school.SchoolRegistrationOfficerId,
                    RegistrarName = registrarName,
                    RegistrarUserName = user?.UserName ?? string.Empty,
                    AccessStatus = user?.AccessStatus ?? AccessStatus.Disable,
                    AccountantStatus = school.SchoolAdminAccountantStatus,
                    SubscriptionExpiryDate = expiry,
                    CreatedDate = user?.CreatedDate,
                    SubscriptionHealth = health,
                    AttentionPriority = priority
                };
            }).ToList();

            return new SuperAdminDashboardDto
            {
                TotalSchools = overviewRows.Count,
                ActiveSchools = overviewRows.Count(row => row.AccessStatus == AccessStatus.Active),
                DisabledSchools = overviewRows.Count(row => row.AccessStatus == AccessStatus.Disable),
                SchoolsWithAccountant = overviewRows.Count(
                    row => row.AccountantStatus == SchoolAdminAccountantStatus.Exist),
                SchoolsWithoutAccountant = overviewRows.Count(
                    row => row.AccountantStatus == SchoolAdminAccountantStatus.None),
                ActiveSubscriptions = overviewRows.Count(row =>
                    row.SubscriptionExpiryDate.HasValue
                    && row.SubscriptionExpiryDate.Value.Date >= today),
                ExpiringWithin30Days = overviewRows.Count(row =>
                    row.SubscriptionExpiryDate.HasValue
                    && row.SubscriptionExpiryDate.Value.Date >= today
                    && row.SubscriptionExpiryDate.Value.Date <= thirtyDaysFromNow),
                ExpiredSubscriptions = overviewRows.Count(row =>
                    row.SubscriptionExpiryDate.HasValue
                    && row.SubscriptionExpiryDate.Value.Date < today),
                NoSubscription = overviewRows.Count(row => row.SubscriptionExpiryDate == null),
                TotalAgents = await _uow.Repository<Agent>().CountAsync(cancellationToken: ct),
                SchoolsNeedingAttention = overviewRows
                    .Where(row => row.AttentionPriority > 0)
                    .OrderByDescending(row => row.AttentionPriority)
                    .ThenBy(row => row.SubscriptionExpiryDate ?? DateTime.MinValue)
                    .Take(10)
                    .ToList(),
                RecentSchools = overviewRows
                    .OrderByDescending(row => row.CreatedDate ?? DateTime.MinValue)
                    .Take(6)
                    .ToList()
            };
        }

        /// <summary>
        /// Legacy RegistrationService.GetSchoolListForSuperAdmin — every school, with officer/user/agent.
        /// The old list stopped at 7; the page now shows the full count.
        /// </summary>
        /// <remarks>
        /// Do not Include required navigations (Officer/User/Agent) on the base query.
        /// EF uses INNER JOINs for those required FKs, so a school whose
        /// SchoolRegistrationOfficerId / AgentUserId is orphaned is counted but excluded from the list.
        /// Load SchoolInfos first, then hydrate related rows separately (LEFT JOIN style).
        /// </remarks>
        public async Task<SchoolListForSystemAdminDto> GetSchoolListForSuperAdmin(CancellationToken ct = default)
        {
            var schoolRegistrationRepository = _uow.Repository<SchoolTyInfo, string>();
            var noOfSchools = await schoolRegistrationRepository.CountAsync(cancellationToken: ct);
            var schools = (await schoolRegistrationRepository.GetAllAsync(ct)).ToList();

            await HydrateSchoolOfficerGraphAsync(schools, ct);

            return new SchoolListForSystemAdminDto
            {
                SchoolInfo = schools.Select(MapSchoolInfoForSuperAdmin).ToList(),
                NoOfSchools = noOfSchools
            };
        }

        /// <summary>
        /// Legacy RegistrationService.GetAllRegistrarForSuperAdmin — full list after enable/disable refresh.
        /// </summary>
        public async Task<IEnumerable<SchoolInfoDto>> GetAllRegistrarForSuperAdmin(CancellationToken ct = default)
        {
            var schoolRegistrationOfficerRepository = _uow.Repository<SchoolTyInfo, string>();
            var schools = await schoolRegistrationOfficerRepository.GetAllAsync(ct);
            await HydrateSchoolOfficerGraphAsync(schools, ct);

            return schools.Select(MapSchoolInfoForSuperAdmin).ToList();
        }

        /// <summary>
        /// Attaches Officer / User / Agent without filtering out orphan SchoolInfos rows.
        /// </summary>
        private async Task HydrateSchoolOfficerGraphAsync(List<SchoolTyInfo> schools, CancellationToken ct)
        {
            if (schools == null || schools.Count == 0)
            {
                return;
            }

            var scopeIds = schools
                .Select(s => s.SchoolRegistrationOfficerId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (scopeIds.Count == 0)
            {
                return;
            }

            var officerRepo = _uow.Repository<SchoolRegistrationOfficer>();
            // Load officers + users only (Agent is required in the model; including it can drop officers
            // whose AgentUserId points at a missing Agents row).
            var officers = await officerRepo.WhereAsync(
                o => scopeIds.Contains(o.UserId),
                ct,
                o => o.User);

            var agentIds = officers
                .Select(o => o.AgentUserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var agents = agentIds.Count == 0
                ? new List<Agent>()
                : await _uow.Repository<Agent>().WhereAsync(a => agentIds.Contains(a.UserId), ct);

            var officersByUserId = officers.ToDictionary(o => o.UserId, StringComparer.OrdinalIgnoreCase);
            var agentsByUserId = agents.ToDictionary(a => a.UserId, StringComparer.OrdinalIgnoreCase);

            var missingUserIds = scopeIds
                .Where(id => !officersByUserId.ContainsKey(id))
                .ToList();
            var fallbackUsers = missingUserIds.Count == 0
                ? new List<ApplicationUser>()
                : await _uow.Repository<ApplicationUser, string>()
                    .WhereAsync(u => missingUserIds.Contains(u.Id), ct);
            var usersById = fallbackUsers.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var school in schools)
            {
                if (officersByUserId.TryGetValue(school.SchoolRegistrationOfficerId, out var officer))
                {
                    if (!string.IsNullOrWhiteSpace(officer.AgentUserId)
                        && agentsByUserId.TryGetValue(officer.AgentUserId, out var agent))
                    {
                        officer.AgentUser = agent;
                    }

                    school.SchoolRegistrationOfficer = officer;
                    continue;
                }

                // Orphan school scope: SchoolInfos row exists but no matching SchoolRegistrationOfficer.UserId.
                // Still surface AspNetUsers when SchoolRegistrationOfficerId is a login id.
                if (usersById.TryGetValue(school.SchoolRegistrationOfficerId, out var user))
                {
                    school.SchoolRegistrationOfficer = new SchoolRegistrationOfficer
                    {
                        UserId = user.Id,
                        User = user
                    };
                }
            }
        }

        /// <summary>
        /// Legacy RegistrationService.SearchSchoolByCertainParameter (SearchComplex).
        /// searchStatus: 0 UserName, 1 FirstName, 2 SurName, 3 Phone, 4 SchoolName, 5 SmsApiToken, 6 SmsApiKey.
        /// </summary>
        public async Task<IEnumerable<SchoolInfoDto>> SearchSchoolByCertainParameter(
            string searchParameter,
            int searchStatus,
            CancellationToken ct = default)
        {
            var schoolRegistrationRepository = _uow.Repository<SchoolTyInfo, string>();
            searchParameter = (searchParameter ?? string.Empty).Trim();

            // Token / Key: strip whitespace so fingerprints like "^^" match after transport mangling.
            if (searchStatus is 5 or 6)
            {
                searchParameter = new string(searchParameter.Where(static c => !char.IsWhiteSpace(c)).ToArray());
            }

            // Base64 cipher is mixed-case; Postgres LIKE/Contains is case-sensitive, so both sides must be lowered
            // (legacy SQL Server CI collation hid this when only the column was ToLower()'d).
            var encryptSearchParameter = FreePlugIn.Encrypt(searchParameter, true).ToLower();
            var normalized = searchParameter.ToLowerInvariant();

            List<SchoolTyInfo> searchResult;
            if (searchStatus == 0)
            {
                searchResult = await schoolRegistrationRepository.WhereAsync(
                    x => x.SchoolRegistrationOfficer.User.UserName.Trim().ToLower().Contains(normalized),
                    ct,
                    z => z.SchoolRegistrationOfficer.User);
            }
            else if (searchStatus == 1)
            {
                searchResult = await schoolRegistrationRepository.WhereAsync(
                    x => x.SchoolRegistrationOfficer.User.FirstName.Trim().ToLower().Contains(normalized),
                    ct,
                    z => z.SchoolRegistrationOfficer.User);
            }
            else if (searchStatus == 2)
            {
                searchResult = await schoolRegistrationRepository.WhereAsync(
                    x => x.SchoolRegistrationOfficer.User.SurName.Trim().ToLower().Contains(normalized),
                    ct,
                    z => z.SchoolRegistrationOfficer.User);
            }
            else if (searchStatus == 3)
            {
                searchResult = await schoolRegistrationRepository.WhereAsync(
                    x => x.Telephone.Trim().Contains(normalized) ||
                         x.SchoolRegistrationOfficer.User.PhoneNumber.Trim().Contains(normalized),
                    ct,
                    z => z.SchoolRegistrationOfficer.User);
            }
            else if (searchStatus == 4)
            {
                // School-name search must not INNER JOIN officer/agent (orphans would vanish).
                searchResult = await schoolRegistrationRepository.WhereAsync(
                    x => x.SchoolName.Trim().ToLower().Contains(normalized),
                    ct);
            }
            else if (searchStatus == 5)
            {
                // System token (SmsApiToken): exact encrypted match, cipher paste, or plaintext paste.
                var encryptExact = FreePlugIn.Encrypt(searchParameter, true);
                searchResult = await schoolRegistrationRepository.WhereAsync(
                    x => x.SmsApiToken != null &&
                         (x.SmsApiToken == encryptExact
                          || x.SmsApiToken.ToLower() == encryptSearchParameter
                          || x.SmsApiToken.ToLower().Contains(encryptSearchParameter)
                          || x.SmsApiToken.ToLower().Contains(normalized)),
                    ct);
            }
            else if (searchStatus == 6)
            {
                // System Id (Key) (SmsApiKey): same matching strategy as token.
                var encryptExact = FreePlugIn.Encrypt(searchParameter, true);
                searchResult = await schoolRegistrationRepository.WhereAsync(
                    x => x.SmsApiKey != null &&
                         (x.SmsApiKey == encryptExact
                          || x.SmsApiKey.ToLower() == encryptSearchParameter
                          || x.SmsApiKey.ToLower().Contains(encryptSearchParameter)
                          || x.SmsApiKey.ToLower().Contains(normalized)),
                    ct);

                // Fallback: some rows store already-encrypted NIC / mixed legacy forms — decrypt & compare.
                if (searchResult.Count == 0)
                {
                    var allWithKeys = await schoolRegistrationRepository.WhereAsync(
                        x => x.SmsApiKey != null && x.SmsApiKey != string.Empty,
                        ct);
                    searchResult = allWithKeys.Where(x => SmsApiKeyMatchesSearch(x.SmsApiKey, searchParameter, encryptExact))
                        .ToList();
                }
            }
            else
            {
                searchResult = new List<SchoolTyInfo>();
            }

            await HydrateSchoolOfficerGraphAsync(searchResult, ct);
            return searchResult.Select(MapSchoolInfoForSuperAdmin).ToList();
        }

        /// <summary>
        /// Legacy RegistrationService.DisableSchoolUsersBySchoolRegistrarId — cascade disable registrar + students + teachers.
        /// </summary>
        public async Task<bool> DisableSchoolUsersBySchoolRegistrarId(string userId, CancellationToken ct = default)
        {
            var teacherRepository = _uow.Repository<TxEmployee>();
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var userModel = await userRepository.FindAsync(i => i.Id == userId, ct);
            if (userModel == null)
            {
                return false;
            }

            userModel.AccessStatus = AccessStatus.Disable;
            userRepository.Update(userModel);

            var school = await _uow.Repository<SchoolTyInfo, string>()
                .FindAsync(x => x.SchoolRegistrationOfficer.UserId == userId, ct);
            if (school == null)
            {
                return false;
            }

            var schoolId = school.SchoolRegistrationOfficerId;
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var armStudents = await studentRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId,
                ct,
                r => r.User);

            foreach (var item in armStudents)
            {
                var model = item.User;
                if (model == null) continue;
                model.AccessStatus = AccessStatus.Disable;
                userRepository.Update(model);
            }

            var teachers = await teacherRepository.WhereAsync(x => x.SchoolId == schoolId, ct, r => r.User);
            foreach (var item in teachers)
            {
                var model = item.User;
                if (model == null) continue;
                model.AccessStatus = AccessStatus.Disable;
                userRepository.Update(model);
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Legacy RegistrationService.EnableSchoolUsersBySchoolRegistrarId — cascade enable registrar + students + teachers.
        /// </summary>
        public async Task<bool> EnableSchoolUsersBySchoolRegistrarId(string userId, CancellationToken ct = default)
        {
            var teacherRepository = _uow.Repository<TxEmployee>();
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var userModel = await userRepository.FindAsync(i => i.Id == userId, ct);
            if (userModel == null)
            {
                return false;
            }

            userModel.AccessStatus = AccessStatus.Active;
            userRepository.Update(userModel);

            var school = await _uow.Repository<SchoolTyInfo, string>()
                .FindAsync(x => x.SchoolRegistrationOfficer.UserId == userId, ct);
            if (school == null)
            {
                return false;
            }

            var schoolId = school.SchoolRegistrationOfficerId;
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var armStudents = await studentRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId,
                ct,
                r => r.User);

            foreach (var item in armStudents)
            {
                var model = item.User;
                if (model == null) continue;
                model.AccessStatus = AccessStatus.Active;
                userRepository.Update(model);
            }

            var teachers = await teacherRepository.WhereAsync(x => x.SchoolId == schoolId, ct, r => r.User);
            foreach (var item in teachers)
            {
                var model = item.User;
                if (model == null) continue;
                model.AccessStatus = AccessStatus.Active;
                userRepository.Update(model);
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Legacy RegistrationService.GetSchoolRegistrarDetailsById.
        /// </summary>
        public async Task<SchoolInfoDto?> GetSchoolRegistrarDetailsById(string userId, CancellationToken ct = default)
        {
            var schoolRegistrationOfficerRepository = _uow.Repository<SchoolRegistrationOfficer>();
            var officer = await schoolRegistrationOfficerRepository.FindAsync(
                i => i.UserId == userId,
                ct,
                u => u.SchoolInfo);
            var schoolInfo = officer?.SchoolInfo;
            if (schoolInfo == null)
            {
                return null;
            }

            return MapSchoolInfoForSuperAdmin(schoolInfo);
        }

        /// <summary>
        /// Legacy RegistrationService.GetAgentInfoById.
        /// </summary>
        public async Task<AgentInfoDto> GetAgentInfoById(string id, CancellationToken ct = default)
        {
            var agentRepo = _uow.Repository<ApplicationUser, string>();
            var model = await agentRepo.FindAsync(id, ct);
            if (model == null)
            {
                return new AgentInfoDto();
            }

            var agentEntity = await _uow.Repository<Agent>().FindAsync(a => a.UserId == id, ct);

            return new AgentInfoDto
            {
                UserName = model.UserName ?? string.Empty,
                Email = model.Email ?? string.Empty,
                UserId = model.Id,
                PhoneNumber = model.PhoneNumber ?? string.Empty,
                FirstName = model.FirstName ?? string.Empty,
                SurName = model.SurName ?? string.Empty,
                MiddleName = model.MiddleName ?? string.Empty,
                Gender = model.Gender,
                AccessStatus = model.AccessStatus,
                FullAddress = agentEntity?.FullAddress ?? string.Empty,
                Password = "******",
                ConfirmPassword = "******"
            };
        }

        /// <summary>
        /// Legacy RegistrationService.DeleteSchoolRegistrar — return codes 0..4.
        /// Also removes Registry role before deleting the user (legacy controller RemoveFromRole).
        /// </summary>
        public async Task<byte> DeleteSchoolRegistrar(string userId, CancellationToken ct = default)
        {
            var studentDetailsRepository = _uow.Repository<TxStudentDetail>();
            var hasHeRegisteredStudent = await studentDetailsRepository.FindAsync(
                a => a.SchoolInfo.SchoolRegistrationOfficer.UserId == userId,
                ct);
            if (hasHeRegisteredStudent != null)
            {
                return 1;
            }

            var classsRepository = _uow.Repository<RxClasss>();
            var hasHeclasses = await classsRepository.FindAsync(
                a => a.SchoolInfo.SchoolRegistrationOfficer.UserId == userId,
                ct);
            if (hasHeclasses != null)
            {
                return 2;
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            var hasHeSubject = await subjectRepository.FindAsync(
                a => a.SubjectCategory.SchoolInfo.SchoolRegistrationOfficer.UserId == userId,
                ct);
            if (hasHeSubject != null)
            {
                return 3;
            }

            var smsPurchaseRepository = _uow.Repository<SmsPurchase>();
            var purchaseList = await smsPurchaseRepository.WhereAsync(
                d => d.SchoolSmsInfo.SchoolRegistrationOfficer.UserId == userId,
                ct);

            var usedSms = await _uow.Repository<SmsUsed>().FindAsync(
                d => d.SchoolSmsInfo.SchoolRegistrationOfficer.UserId == userId,
                ct);
            if (usedSms != null)
            {
                return 4;
            }

            var smsPaymentBalanceRepository = _uow.Repository<SmsPaymentBalance>();
            foreach (var each in purchaseList)
            {
                smsPurchaseRepository.Delete(each);
            }

            var smsBal = await smsPaymentBalanceRepository.FindAsync(
                c => c.SchoolSmsInfo.SchoolRegistrationOfficer.UserId == userId,
                ct);
            if (smsBal != null)
            {
                smsPaymentBalanceRepository.Delete(smsBal);
            }

            var userRepository = _uow.Repository<ApplicationUser, string>();
            var schoolRegistrationOfficerRepository = _uow.Repository<SchoolRegistrationOfficer>();
            var subjectCategoriesRepository = _uow.Repository<SubjectRxCategories>();
            var subjectCategories = await subjectCategoriesRepository.WhereAsync(
                k => k.SchoolInfo.SchoolRegistrationOfficer.UserId == userId,
                ct,
                c => c.SchoolInfo);
            foreach (var each in subjectCategories)
            {
                subjectCategoriesRepository.Delete(each);
            }

            var schoolInfoRepository = _uow.Repository<SchoolTyInfo, string>();
            var schoolModel = await schoolInfoRepository.FindAsync(
                x => x.SchoolRegistrationOfficer.UserId == userId,
                ct);
            if (schoolModel != null)
            {
                schoolInfoRepository.Delete(schoolModel);
            }

            var registrationOfficerModel = await schoolRegistrationOfficerRepository.FindAsync(
                s => s.UserId == userId,
                ct,
                o => o.User);
            if (registrationOfficerModel == null)
            {
                return 4;
            }

            var userModel = registrationOfficerModel.User;
            schoolRegistrationOfficerRepository.Delete(registrationOfficerModel);

            // Legacy RegistrationController: RemoveFromRole(userId, "Registry") before commit.
            await RemoveUserFromRegistryRoleAsync(userId, ct);

            if (userModel != null)
            {
                userRepository.Delete(userModel);
            }

            await _uow.SaveChangesAsync(ct);
            return 0;
        }

        private async Task RemoveUserFromRegistryRoleAsync(string userId, CancellationToken ct)
        {
            var roleRepository = _uow.Repository<IdentityRole>();
            var role = await roleRepository.FindAsync(v => v.Name == "Registry", ct);
            if (role == null || string.IsNullOrWhiteSpace(role.Id))
            {
                return;
            }

            var userRoleRepository = _uow.Repository<IdentityUserRole<string>>();
            var existingRoles = await userRoleRepository.WhereAsync(
                r => r.UserId == userId && r.RoleId == role.Id,
                ct);
            foreach (var eachRole in existingRoles)
            {
                userRoleRepository.Delete(eachRole);
            }
        }

        private SchoolInfoDto MapSchoolInfoForSuperAdmin(SchoolTyInfo school)
        {
            var dto = _mapper.Map<SchoolInfoDto>(school) ?? new SchoolInfoDto();
            dto.SchoolRegistrationOfficerId = school.SchoolRegistrationOfficerId;
            dto.SystemImplementationTypeCode = DecodeSystemImplementationTypeCode(school.SystemImplementationType);

            var officer = school.SchoolRegistrationOfficer;
            if (officer != null)
            {
                dto.SchoolRegistrationOfficer ??= new SchoolRegistrationOfficerDto();
                // Legacy SchoolRegistrationOfficer.Id == SchoolRegistrationOfficerId (school PK).
                dto.SchoolRegistrationOfficer.Id = school.SchoolRegistrationOfficerId;
                dto.SchoolRegistrationOfficer.UserId = officer.UserId;
                dto.SchoolRegistrationOfficer.AgentUserId = 0;

                if (officer.User != null)
                {
                    dto.SchoolRegistrationOfficer.User = new UserDto
                    {
                        Id = officer.User.Id,
                        UserName = officer.User.UserName ?? string.Empty,
                        Email = officer.User.Email ?? string.Empty,
                        PhoneNumber = officer.User.PhoneNumber ?? string.Empty,
                        FirstName = officer.User.FirstName ?? string.Empty,
                        MiddleName = officer.User.MiddleName ?? string.Empty,
                        SurName = officer.User.SurName ?? string.Empty,
                        AccessStatus = officer.User.AccessStatus,
                        Gender = officer.User.Gender
                    };
                }

                if (officer.AgentUser != null)
                {
                    dto.SchoolRegistrationOfficer.AgentUser = new AgentDto
                    {
                        Id = officer.AgentUser.UserId,
                        UserId = officer.AgentUser.UserId,
                        TimeStatus = officer.AgentUser.TimeStatus
                    };
                }
            }

            return dto;
        }

        private static byte? DecodeSystemImplementationTypeCode(string? encrypted)
        {
            if (string.IsNullOrWhiteSpace(encrypted))
            {
                return null;
            }

            try
            {
                var decrypted = FreePlugIn.Decrypt(encrypted, true);
                if (decrypted.Contains(":1", StringComparison.Ordinal))
                {
                    return 1;
                }

                if (decrypted.Contains(":0", StringComparison.Ordinal))
                {
                    return 0;
                }
            }
            catch
            {
                // leave null
            }

            return null;
        }

        /// <summary>
        /// Legacy RegistrationService.CheckRenewalOrRegistrationStatus(schoolId) —
        /// returns current term Id when the school has a Current term; otherwise null.
        /// </summary>
        public async Task<string?> CheckRenewalOrRegistrationStatus(string schoolId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var current = await yearlyAndTermlyStatusRepository.FindAsync(
                x => x.Session.SchoolInfoId == schoolId
                     && x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.Session!);
            return string.IsNullOrWhiteSpace(current?.Id) ? null : current.Id;
        }

        /// <summary>Legacy RegistrationService.GetSchoolSnoForSuperAdminOfficer</summary>
        public async Task<short> GetSchoolSnoForSuperAdminOfficer(string schoolId, CancellationToken ct = default)
        {
            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(schoolId, ct);
            if (school == null)
            {
                throw new InvalidOperationException("School was not found.");
            }

            return school.SsNo;
        }

        /// <summary>
        /// Legacy CreateClassTeacher SuperAccountant branch — creates TxEmployee and marks school accountant status Exist.
        /// </summary>
        public async Task<bool> CreateSuperAccountantEmployeeRecord(
            CreateAdminAccountantEmployeeDto model,
            CancellationToken ct = default)
        {
            if (model == null
                || string.IsNullOrWhiteSpace(model.SchoolId)
                || string.IsNullOrWhiteSpace(model.UserId)
                || string.IsNullOrWhiteSpace(model.UserName)
                || !model.UserName.StartsWith("SuperAccountant", StringComparison.Ordinal))
            {
                return false;
            }

            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var currentTerm = await yearlyAndTermlyStatusRepository.FindAsync(
                x => x.Session.SchoolInfoId == model.SchoolId
                     && x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.Session!);
            if (currentTerm == null || string.IsNullOrWhiteSpace(currentTerm.Id))
            {
                return false;
            }

            var schoolInfoRepository = _uow.Repository<SchoolTyInfo, string>();
            var schoolModel = await schoolInfoRepository.FindAsync(
                x => x.SchoolRegistrationOfficerId == model.SchoolId,
                ct);
            if (schoolModel == null)
            {
                return false;
            }

            schoolModel.SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.Exist;
            schoolModel.AccountingStartTermId = currentTerm.Id;
            schoolInfoRepository.Update(schoolModel);

            var teacherRepository = _uow.Repository<TxEmployee>();
            teacherRepository.Add(new TxEmployee
            {
                Id = Guid.NewGuid().ToString(),
                DateOfBirth = model.DateOfBirth,
                UserId = model.UserId.Trim(),
                TimeStatus = TimeStatus.Current,
                SignatureImageString = model.SignatureImageString?.Trim() ?? string.Empty,
                EmployeeStatus = EmployeeStatus.Accountant,
                SchoolId = model.SchoolId.Trim(),
                EmployeeIdNo = model.EmployeeIdNo?.Trim() ?? string.Empty,
                FullAddress = model.FullAddress?.Trim() ?? string.Empty,
                EmployeeIncomeStructureStatus = EmployeeIncomeStructureStatus.HasNotSet,
                EmployeeQualificationStatus = EmployeeQualificationStatus.Others
            });

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Creates the employee record for a Subordinate Accountant (legacy CreateAdminAccountant,
        /// Accounting/SuperAccountant branch → CreateClassTeacher with EmployeeStatus.Accountant).
        /// Unlike the Admin Accountant, the username is caller-supplied and the school's
        /// admin-accountant status is not toggled.
        /// </summary>
        public async Task<bool> CreateSubordinateAccountantEmployeeRecord(
            CreateAdminAccountantEmployeeDto model,
            CancellationToken ct = default)
        {
            if (model == null
                || string.IsNullOrWhiteSpace(model.SchoolId)
                || string.IsNullOrWhiteSpace(model.UserId)
                || string.IsNullOrWhiteSpace(model.UserName))
            {
                return false;
            }

            var schoolInfoRepository = _uow.Repository<SchoolTyInfo, string>();
            var schoolModel = await schoolInfoRepository.FindAsync(
                x => x.SchoolRegistrationOfficerId == model.SchoolId,
                ct);
            if (schoolModel == null)
            {
                return false;
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            teacherRepository.Add(new TxEmployee
            {
                Id = Guid.NewGuid().ToString(),
                DateOfBirth = model.DateOfBirth,
                UserId = model.UserId.Trim(),
                TimeStatus = TimeStatus.Current,
                SignatureImageString = model.SignatureImageString?.Trim() ?? string.Empty,
                EmployeeStatus = EmployeeStatus.Accountant,
                SchoolId = model.SchoolId.Trim(),
                EmployeeIdNo = model.EmployeeIdNo?.Trim() ?? string.Empty,
                FullAddress = model.FullAddress?.Trim() ?? string.Empty,
                EmployeeIncomeStructureStatus = EmployeeIncomeStructureStatus.HasNotSet,
                EmployeeQualificationStatus = EmployeeQualificationStatus.Others
            });

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Legacy RegistrationService.GetSchoolAccountantStatus — used by Accounting side nav
        /// (Create vs View Subordinate Accountant link for SuperAccountant users).
        /// </summary>
        public async Task<SchoolSubordinateAccountantStatusDto> GetSchoolAccountantStatus(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new SchoolSubordinateAccountantStatusDto
                {
                    SchoolId = string.Empty,
                    SubordinateAccountantStatus = false
                };
            }

            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(schoolId, ct);
            if (school == null)
            {
                return new SchoolSubordinateAccountantStatusDto
                {
                    SchoolId = schoolId,
                    SubordinateAccountantStatus = false
                };
            }

            var superUserName = "SuperAccountant" + school.SsNo;
            var employeeRepository = _uow.Repository<TxEmployee>();
            var subordinate = await employeeRepository.FindAsync(
                x =>
                    x.SchoolId == schoolId &&
                    x.EmployeeStatus == EmployeeStatus.Accountant &&
                    x.User != null &&
                    x.User.UserName != superUserName,
                ct,
                x => x.User!);

            return new SchoolSubordinateAccountantStatusDto
            {
                SchoolId = schoolId,
                SubordinateAccountantStatus = subordinate != null
            };
        }

        /// <summary>Legacy RegistrationService.GetSubordinateAccountantDetails</summary>
        public async Task<EmployeeDto?> GetSubordinateAccountantDetails(
            string schoolId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(schoolId, ct);
            if (school == null)
            {
                return null;
            }

            var superUserName = "SuperAccountant" + school.SsNo;
            var employeeRepository = _uow.Repository<TxEmployee>();
            var model = await employeeRepository.FindAsync(
                s =>
                    s.SchoolId == schoolId &&
                    s.EmployeeStatus == EmployeeStatus.Accountant &&
                    s.User != null &&
                    s.User.UserName != superUserName,
                ct,
                s => s.User!,
                x => x.SchoolInfo!);

            if (model == null)
            {
                return null;
            }

            return new EmployeeDto
            {
                Id = model.Id,
                FullAddress = model.FullAddress,
                DateOfBirth = model.DateOfBirth,
                UserId = model.UserId,
                SignatureImageString = model.SignatureImageString,
                TimeStatus = model.TimeStatus,
                EmployeeStatus = model.EmployeeStatus.ToString(),
                SchoolId = model.SchoolId,
                SchoolInfo = model.SchoolInfo == null
                    ? null
                    : new SchoolInfoDto
                    {
                        SchoolName = model.SchoolInfo.SchoolName,
                        SchoolRegistrationOfficerId = model.SchoolInfo.SchoolRegistrationOfficerId
                    },
                User = model.User == null
                    ? null
                    : new UserDto
                    {
                        Id = model.User.Id,
                        UserName = model.User.UserName ?? string.Empty,
                        Email = model.User.Email ?? string.Empty,
                        PhoneNumber = model.User.PhoneNumber ?? string.Empty,
                        FirstName = model.User.FirstName ?? string.Empty,
                        MiddleName = model.User.MiddleName ?? string.Empty,
                        SurName = model.User.SurName ?? string.Empty,
                        AccessStatus = model.User.AccessStatus,
                        Gender = model.User.Gender
                    }
            };
        }

        /// <summary>
        /// Legacy UpdateAccountantInfo Accounting/SuperAccountant branch — updates the subordinate
        /// accountant (username != SuperAccountant{sno}) for the given school.
        /// </summary>
        public async Task<bool> UpdateSubordinateAccountantInfo(
            UpdateAdminAccountantDto model,
            CancellationToken ct = default)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.SchoolRegId))
            {
                return false;
            }

            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(model.SchoolRegId, ct);
            if (school == null)
            {
                return false;
            }

            var superUserName = "SuperAccountant" + school.SsNo;
            var employeeRepository = _uow.Repository<TxEmployee>();
            var accountantModel = await employeeRepository.FindAsync(
                x =>
                    x.SchoolId == model.SchoolRegId &&
                    x.EmployeeStatus == EmployeeStatus.Accountant &&
                    x.User != null &&
                    x.User.UserName != superUserName,
                ct,
                u => u.User!);
            if (accountantModel?.User == null)
            {
                return false;
            }

            accountantModel.EmployeeStatus = EmployeeStatus.Accountant;
            employeeRepository.Update(accountantModel);

            var userModel = accountantModel.User;
            userModel.FirstName = model.FirstName?.Trim() ?? string.Empty;
            userModel.SurName = model.SurName?.Trim() ?? string.Empty;
            userModel.MiddleName = model.MiddleName?.Trim() ?? string.Empty;
            _uow.Repository<ApplicationUser, string>().Update(userModel);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>Legacy RegistrationService.DeleteAnAccountants — deletes one subordinate accountant.</summary>
        public async Task<string?> DeleteSubordinateAccountant(string employeeId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return null;
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var employeesModel = await teacherRepository.FindAsync(
                t => t.Id == employeeId,
                ct,
                x => x.User!);
            if (employeesModel?.User == null)
            {
                return null;
            }

            var returnString = employeesModel.User.Id;
            await RemoveUserFromRoleNameAsync(employeesModel.UserId, "Accounting", ct);
            userRepository.Delete(employeesModel.User);
            teacherRepository.Delete(employeesModel);
            await _uow.SaveChangesAsync(ct);
            return returnString;
        }

        /// <summary>Legacy RegistrationService.GetAdminAccountantDetails</summary>
        public async Task<EmployeeDto?> GetAdminAccountantDetails(string schoolId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(schoolId, ct);
            if (school == null)
            {
                return null;
            }

            var expectedUserName = "SuperAccountant" + school.SsNo;
            var employeeRepository = _uow.Repository<TxEmployee>();
            var model = await employeeRepository.FindAsync(
                s => s.SchoolId == schoolId && s.User!.UserName == expectedUserName,
                ct,
                s => s.User!,
                x => x.SchoolInfo!);

            if (model == null)
            {
                return null;
            }

            return new EmployeeDto
            {
                Id = model.Id,
                FullAddress = model.FullAddress,
                DateOfBirth = model.DateOfBirth,
                UserId = model.UserId,
                SignatureImageString = model.SignatureImageString,
                TimeStatus = model.TimeStatus,
                EmployeeStatus = model.EmployeeStatus.ToString(),
                SchoolId = model.SchoolId,
                SchoolInfo = model.SchoolInfo == null
                    ? null
                    : new SchoolInfoDto
                    {
                        SchoolName = model.SchoolInfo.SchoolName,
                        SchoolRegistrationOfficerId = model.SchoolInfo.SchoolRegistrationOfficerId
                    },
                User = model.User == null
                    ? null
                    : new UserDto
                    {
                        Id = model.User.Id,
                        UserName = model.User.UserName ?? string.Empty,
                        Email = model.User.Email ?? string.Empty,
                        PhoneNumber = model.User.PhoneNumber ?? string.Empty,
                        FirstName = model.User.FirstName ?? string.Empty,
                        MiddleName = model.User.MiddleName ?? string.Empty,
                        SurName = model.User.SurName ?? string.Empty,
                        AccessStatus = model.User.AccessStatus,
                        Gender = model.User.Gender
                    }
            };
        }

        public async Task<bool> EnableAccountantUserById(string userId, CancellationToken ct = default)
        {
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var userModel = await userRepository.FindAsync(i => i.Id == userId, ct);
            if (userModel == null) return false;
            userModel.AccessStatus = AccessStatus.Active;
            userRepository.Update(userModel);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DisableAccountantUserById(string userId, CancellationToken ct = default)
        {
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var userModel = await userRepository.FindAsync(i => i.Id == userId, ct);
            if (userModel == null) return false;
            userModel.AccessStatus = AccessStatus.Disable;
            userRepository.Update(userModel);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>Legacy RegistrationService.UpdateAccountantInfo (SAOfficer / SuperAccountant branch).</summary>
        public async Task<bool> UpdateAccountantInfo(UpdateAdminAccountantDto model, CancellationToken ct = default)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.SchoolRegId))
            {
                return false;
            }

            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(model.SchoolRegId, ct);
            if (school == null)
            {
                return false;
            }

            var schoolSnoId = school.SsNo.ToString();
            var expectedUserName = "SuperAccountant" + schoolSnoId;
            var employeeRepository = _uow.Repository<TxEmployee>();
            var accountantModel = await employeeRepository.FindAsync(
                x => x.SchoolId == model.SchoolRegId && x.User!.UserName == expectedUserName,
                ct,
                u => u.User!);
            if (accountantModel?.User == null)
            {
                return false;
            }

            accountantModel.EmployeeStatus = EmployeeStatus.Accountant;
            employeeRepository.Update(accountantModel);

            var userModel = accountantModel.User;
            userModel.FirstName = model.FirstName?.Trim() ?? string.Empty;
            userModel.SurName = model.SurName?.Trim() ?? string.Empty;
            userModel.MiddleName = model.MiddleName?.Trim() ?? string.Empty;
            _uow.Repository<ApplicationUser, string>().Update(userModel);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Legacy RegistrationService.DeleteAllAccountants — returns comma-separated userIds for role removal.
        /// </summary>
        public async Task<string?> DeleteAllAccountants(string employeeId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return null;
            }

            var teacherRepository = _uow.Repository<TxEmployee>();
            var userRepository = _uow.Repository<ApplicationUser, string>();
            var employeesModel = await teacherRepository.FindAsync(
                t => t.Id == employeeId,
                ct,
                x => x.SchoolInfo!,
                z => z.User!);
            if (employeesModel?.SchoolInfo == null)
            {
                return null;
            }

            var schoolModel = employeesModel.SchoolInfo;
            schoolModel.SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.None;
            _uow.Repository<SchoolTyInfo, string>().Update(schoolModel);

            var accountantList = await teacherRepository.WhereAsync(
                s => s.SchoolId == employeesModel.SchoolId && s.EmployeeStatus == EmployeeStatus.Accountant,
                ct,
                z => z.User!);

            var returnString = "";
            foreach (var each in accountantList)
            {
                returnString = returnString != "" ? returnString + "," + each.UserId : each.UserId;
                if (each.User != null)
                {
                    // Remove Accounting role rows before deleting the user.
                    await RemoveUserFromRoleNameAsync(each.UserId, "Accounting", ct);
                    userRepository.Delete(each.User);
                }

                teacherRepository.Delete(each);
            }

            await _uow.SaveChangesAsync(ct);
            return returnString;
        }

        /// <summary>Legacy RegistrationService.GetSchoolRegistrarById</summary>
        public async Task<EditSchoolRegistrarDto?> GetSchoolRegistrarById(string userId, CancellationToken ct = default)
        {
            var schoolRegistrationOfficerRepository = _uow.Repository<SchoolRegistrationOfficer>();
            var userModel = await schoolRegistrationOfficerRepository.FindAsync(
                i => i.UserId == userId,
                ct,
                u => u.User!,
                u => u.SchoolInfo!);

            if (userModel?.User == null || userModel.SchoolInfo == null)
            {
                return null;
            }

            string key;
            try
            {
                key = FreePlugIn.Decrypt(userModel.SchoolInfo.SmsApiKey, true);
            }
            catch
            {
                key = userModel.SchoolInfo.SmsApiKey ?? string.Empty;
            }

            string token;
            try
            {
                token = FreePlugIn.Decrypt(userModel.SchoolInfo.SmsApiToken, true);
            }
            catch
            {
                token = userModel.SchoolInfo.SmsApiToken ?? string.Empty;
            }

            var user = userModel.User;
            return new EditSchoolRegistrarDto
            {
                Id = user.Id,
                FirstName = user.FirstName ?? string.Empty,
                MiddleName = user.MiddleName ?? string.Empty,
                SurName = user.SurName ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Gender = user.Gender,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                CreatedDate = user.CreatedDate,
                AccessStatus = user.AccessStatus,
                UserStatus = user.UserStatus,
                Addresses = userModel.FullAddress ?? string.Empty,
                SchoolName = userModel.SchoolInfo.SchoolName ?? string.Empty,
                DateOfBirth = userModel.DateOfBirth,
                TimeStatus = userModel.TimeStatus,
                SignatureImageString = userModel.SignatureImageString ?? string.Empty,
                SmsApiToken = token,
                SmsApiKey = key,
            };
        }

        /// <summary>
        /// Legacy RegistrationService.UpdateSchoolRegistrarInfo — return codes:
        /// 0 success, 1 email taken, 2 username taken, 3 SMS token used on another school.
        /// </summary>
        public async Task<byte> UpdateSchoolRegistrarInfo(
            EditSchoolRegistrarDto model,
            string hostName,
            CancellationToken ct = default)
        {
            // Guard: legacy assumes non-null model/Id.
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return 2;
            }

            var userRepository = _uow.Repository<ApplicationUser, string>();
            var hasNewEmailExistAgainstAnotherUser = (await userRepository.FindAsync(s => s.Email == model.Email, ct))?.Id;
            if (hasNewEmailExistAgainstAnotherUser != null && hasNewEmailExistAgainstAnotherUser != model.Id)
            {
                return 1; // Failed! Email exist against another record.
            }

            var hasNewUsernameExistAgainstAnotherUser = (await userRepository.FindAsync(s => s.UserName == model.UserName, ct))?.Id;
            if (hasNewUsernameExistAgainstAnotherUser != null && hasNewUsernameExistAgainstAnotherUser != model.Id)
            {
                return 2; // Failed! Username exist against another user.
            }

            var schoolRegistrationOfficerRepository = _uow.Repository<SchoolRegistrationOfficer>();
            var schoolRegistrationOfficer = await schoolRegistrationOfficerRepository.FindAsync(
                i => i.UserId == model.Id,
                ct,
                u => u.SchoolInfo!);

            // Guard: legacy NREs if officer/school missing.
            if (schoolRegistrationOfficer?.SchoolInfo == null)
            {
                return 2;
            }

            var user = await userRepository.FindAsync(model.Id, ct);
            // Guard: legacy NREs if user missing.
            if (user == null)
            {
                return 2;
            }

            var securityStamp = user.SecurityStamp;

            if (string.IsNullOrEmpty(model.Password))
            {
                var passwordHash = user.PasswordHash;
                user.FirstName = model.FirstName;
                user.MiddleName = model.MiddleName;
                user.SurName = model.SurName;
                user.UserName = model.UserName;
                user.NormalizedUserName = (model.UserName ?? string.Empty).ToUpperInvariant();
                user.Email = model.Email;
                user.NormalizedEmail = (model.Email ?? string.Empty).ToUpperInvariant();
                user.PhoneNumber = model.PhoneNumber;
                user.Gender = model.Gender;
                user.AccessStatus = model.AccessStatus;
                user.UserStatus = model.UserStatus;
                user.CreatedDate = model.CreatedDate;
                user.PasswordHash = passwordHash;
                user.SecurityStamp = securityStamp;
                user.EmailConfirmed = true;
            }
            else
            {
                var hasher = new PasswordHasher<ApplicationUser>();
                user.FirstName = model.FirstName;
                user.MiddleName = model.MiddleName;
                user.SurName = model.SurName;
                user.UserName = model.UserName;
                user.NormalizedUserName = (model.UserName ?? string.Empty).ToUpperInvariant();
                user.Email = model.Email;
                user.NormalizedEmail = (model.Email ?? string.Empty).ToUpperInvariant();
                user.PhoneNumber = model.PhoneNumber;
                user.Gender = model.Gender;
                user.AccessStatus = model.AccessStatus;
                user.UserStatus = model.UserStatus;
                user.CreatedDate = model.CreatedDate;
                user.PasswordHash = hasher.HashPassword(user, model.Password);
                user.SecurityStamp = securityStamp;
                user.EmailConfirmed = true;
            }

            userRepository.Update(user);
            schoolRegistrationOfficer.TimeStatus = model.TimeStatus;
            schoolRegistrationOfficer.DateOfBirth = model.DateOfBirth;
            schoolRegistrationOfficerRepository.Update(schoolRegistrationOfficer);

            var schoolInfoRepository = _uow.Repository<SchoolTyInfo, string>();
            model.SmsApiToken = !string.IsNullOrWhiteSpace(model.SmsApiToken) ? model.SmsApiToken.Trim() : null;
            var tokenEncode = FreePlugIn.Encrypt(model.SmsApiToken ?? Guid.NewGuid().ToString(), true);
            var isApiTokenExistOnAnotherRecord = await schoolInfoRepository.FindAsync(
                x => x.SmsApiToken == tokenEncode,
                ct,
                c => c.SchoolRegistrationOfficer!);
            if (isApiTokenExistOnAnotherRecord != null)
            {
                // Guard: legacy NREs if SchoolRegistrationOfficer null.
                if (isApiTokenExistOnAnotherRecord.SchoolRegistrationOfficer != null
                    && isApiTokenExistOnAnotherRecord.SchoolRegistrationOfficer.UserId != model.Id)
                {
                    return 3;
                }
            }

            var schoolModel = schoolRegistrationOfficer.SchoolInfo;

            schoolModel.SchoolName = model.SchoolName;
            schoolModel.TotalStudents = string.IsNullOrEmpty(schoolModel.TotalStudents)
                ? "0"
                : schoolModel.TotalStudents;

            var result = 0;
            if (int.TryParse(schoolModel.TotalStudents, out result))
            {
                schoolModel.TotalStudents =
                    FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":" + schoolModel.TotalStudents, true);
            }

            schoolModel.SmsApiToken = FreePlugIn.Encrypt(model.SmsApiToken ?? Guid.NewGuid().ToString(), true);
            if (hostName == "www.ourschoolonline.ng" || hostName == "ourschoolonline.ng")
            {
                model.SmsApiKey = !string.IsNullOrWhiteSpace(model.SmsApiKey) ? model.SmsApiKey.Trim() : null;
                var arbitraryKey = Guid.NewGuid().ToString();
                schoolModel.SmsApiKey = FreePlugIn.Encrypt(model.SmsApiKey ?? arbitraryKey, true);

                schoolModel.FeatureImplementationType =
                    !string.IsNullOrWhiteSpace(schoolModel.FeatureImplementationType)
                        ? schoolModel.FeatureImplementationType
                        : null;
                var featureImplType = arbitraryKey + ":0";
                schoolModel.FeatureImplementationType = schoolModel.FeatureImplementationType ??
                                                        FreePlugIn.Encrypt(featureImplType, true);
            }
            else
            {
                schoolModel.FeatureImplementationType =
                    !string.IsNullOrWhiteSpace(schoolModel.FeatureImplementationType)
                        ? schoolModel.FeatureImplementationType
                        : null;
                var systemId = FreePlugIn.GetUniqueSystemId();
                var implementationType = systemId + ":0";
                schoolModel.FeatureImplementationType = schoolModel.FeatureImplementationType ??
                                                        FreePlugIn.Encrypt(implementationType, true);
            }

            schoolInfoRepository.Update(schoolModel);
            await _uow.SaveChangesAsync(ct);
            return 0;
        }

        /// <summary>
        /// SMS API Key fingerprints must not contain whitespace. Proxies/form decoding can
        /// turn '+' into space so values like <c>^^</c> arrive as <c>^ ^</c>.
        /// </summary>
        private static string? NormalizeSmsApiKeyFingerprint(string? smsApiKey)
        {
            if (string.IsNullOrWhiteSpace(smsApiKey))
            {
                return null;
            }

            var compact = new string(smsApiKey.Trim().Where(static c => !char.IsWhiteSpace(c)).ToArray());
            return compact.Length == 0 ? null : compact;
        }

        private static bool SmsApiKeyMatchesSearch(string? stored, string searchPlain, string searchEncrypted)
        {
            if (string.IsNullOrWhiteSpace(stored))
            {
                return false;
            }

            var storedTrim = stored.Trim();
            if (string.Equals(storedTrim, searchEncrypted, StringComparison.Ordinal)
                || storedTrim.Contains(searchEncrypted, StringComparison.OrdinalIgnoreCase)
                || storedTrim.Contains(searchPlain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                var decrypted = FreePlugIn.Decrypt(storedTrim, true);
                if (string.IsNullOrWhiteSpace(decrypted) || decrypted == "0")
                {
                    return false;
                }

                var decryptedCompact = new string(decrypted.Where(static c => !char.IsWhiteSpace(c)).ToArray());
                return string.Equals(decryptedCompact, searchPlain, StringComparison.OrdinalIgnoreCase)
                       || decryptedCompact.Contains(searchPlain, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Legacy RegistrationService.GetSchoolInfoById + LicensingIndex ViewBag assembly.</summary>
        public async Task<LicensingIndexPageDto?> GetLicensingIndexPage(string schoolId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var schoolInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var model = await schoolInfoRepo.FindAsync(
                v => v.SchoolRegistrationOfficerId == schoolId,
                ct,
                x => x.SchoolRegistrationOfficer!);

            if (model == null)
            {
                return null;
            }

            var balModel = await _uow.Repository<SmsPaymentBalance>().FindAsync(
                x => x.SchoolSmsId == model.SchoolRegistrationOfficerId,
                ct);
            var smsBal = balModel?.Amount ?? 0f;

            byte? featureType = null;
            string? featureWarning = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(model.FeatureImplementationType))
                {
                    var decrypted = FreePlugIn.Decrypt(model.FeatureImplementationType, true);
                    var parts = decrypted.Split(':');
                    if (parts.Length > 1 && byte.TryParse(parts[1], out var ft))
                    {
                        featureType = ft;
                    }
                }
            }
            catch
            {
                // ignore decrypt failures
            }

            if (featureType == null)
            {
                featureWarning =
                    "Sorry! You need to update the school profile before implementation-type-features can be shown.";
            }

            var totalStudents = "0";
            try
            {
                if (!string.IsNullOrWhiteSpace(model.TotalStudents))
                {
                    var decryptedStudents = FreePlugIn.Decrypt(model.TotalStudents, true);
                    var parts = decryptedStudents.Split(':');
                    totalStudents = parts.Length > 1 ? parts[^1] : decryptedStudents;
                    if (string.IsNullOrWhiteSpace(totalStudents))
                    {
                        totalStudents = "0";
                    }
                }
            }
            catch
            {
                if (int.TryParse(model.TotalStudents, out _))
                {
                    totalStudents = model.TotalStudents;
                }
            }

            var expiringDate = await GetExpiryDateInternal(schoolId, ct);

            return new LicensingIndexPageDto
            {
                Id = model.SchoolRegistrationOfficerId,
                SchoolName = model.SchoolName ?? string.Empty,
                SchoolAddress = model.SchoolAddress ?? string.Empty,
                SchoolTelephone = model.Telephone ?? string.Empty,
                SmsBalance = smsBal,
                ExpiringDate = expiringDate,
                FeatureImplementationType = featureType,
                FeatureImplementationTypeWarning = featureWarning,
                TotalStudents = totalStudents,
            };
        }

        /// <summary>Legacy RegistrationService.GetLastSubscriptionInfo</summary>
        public async Task<LastSubscriptionInfoDto> GetLastSubscriptionInfo(string schoolId, CancellationToken ct = default)
        {
            var lastSubs = await _uow.Repository<LicensTying>().FindAsync(
                x => x.SchoolSmsId == schoolId && x.TimeStatus == TimeStatus.Current,
                ct);

            if (lastSubs == null)
            {
                return new LastSubscriptionInfoDto { HasSubscription = false };
            }

            var subscriptionId = lastSubs.SubscriptionId.ToString();
            var encoded = FreePlugIn.InternalRegistrationKeyEncodingAndExpiryDate(subscriptionId);
            var registrationKey = string.IsNullOrWhiteSpace(encoded)
                ? string.Empty
                : encoded.Split(',')[0];

            return new LastSubscriptionInfoDto
            {
                HasSubscription = true,
                SubscriptionId = subscriptionId,
                RegistrationKey = registrationKey,
                ExpiryDate = lastSubs.ExpiryDate.ToLongDateString(),
            };
        }

        /// <summary>Legacy RegistrationService.GetSubscriptionHistory — last 7 rows.</summary>
        public async Task<IReadOnlyList<SubscriptionHistoryItemDto>> GetSubscriptionHistory(
            string schoolId,
            CancellationToken ct = default)
        {
            var rows = await _uow.Repository<LicensTying>().WhereAsync(
                s => s.SchoolSmsId == schoolId,
                ct);

            return rows
                .OrderByDescending(x => x.PaymentDate)
                .Take(7)
                .Select(x => new SubscriptionHistoryItemDto
                {
                    SubscriptionId = x.SubscriptionId,
                    PaymentDate = x.PaymentDate,
                    ExpiryDate = x.ExpiryDate,
                    Charges = x.Charges,
                    PaymentDateLong = x.PaymentDate.ToLongDateString(),
                    ExpiryDateLong = x.ExpiryDate.ToLongDateString(),
                })
                .ToList();
        }

        /// <summary>Legacy SecurityService.GetSubscriptionKey</summary>
        public async Task<string?> GetSubscriptionKey(
            CreateSubscriptionKeyRequestDto model,
            CancellationToken ct = default)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.SchoolSmsId) || model.SubscriptionId <= 0)
            {
                return null;
            }

            var subscriptionIdText = model.SubscriptionId.ToString();
            if (!subscriptionIdText.All(char.IsDigit)
                || (subscriptionIdText.Length != 7 && subscriptionIdText.Length != 9))
            {
                return null;
            }

            var encoded = FreePlugIn.InternalRegistrationKeyEncodingAndExpiryDate(subscriptionIdText);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return null;
            }

            var licenseRepo = _uow.Repository<LicensTying>();
            var existingSame = await licenseRepo.FindAsync(
                x => x.SchoolSmsId == model.SchoolSmsId
                     && x.SubscriptionId == model.SubscriptionId
                     && x.TimeStatus == TimeStatus.Current,
                ct);

            if (existingSame != null)
            {
                var regKey = encoded.Split(',')[0];
                return $"{regKey},{existingSame.ExpiryDate}";
            }

            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(
                v => v.SchoolRegistrationOfficerId == model.SchoolSmsId,
                ct,
                x => x.SchoolRegistrationOfficer!);

            if (school?.SchoolRegistrationOfficer == null)
            {
                return null;
            }

            var currentLicenses = await licenseRepo.WhereAsync(
                x => x.SchoolSmsId == model.SchoolSmsId && x.TimeStatus == TimeStatus.Current,
                ct);
            foreach (var each in currentLicenses)
            {
                each.TimeStatus = TimeStatus.Previous;
                licenseRepo.Update(each);
            }

            var agentUserId = await ResolveLicensingAgentUserIdAsync(
                model.AgentUserId,
                school.SchoolRegistrationOfficer.AgentUserId,
                ct);
            if (string.IsNullOrWhiteSpace(agentUserId))
            {
                // Persistable fallback so Create is not blocked by a missing Agent FK.
                // Prefer school officer id only when an Agent row exists for it.
                return null;
            }

            var licenseCategory = subscriptionIdText.Length == 9
                ? LicenseCategories.OneYear
                : LicenseCategories.FourMonths;

            // Npgsql requires UTC for "timestamp with time zone" columns.
            var paymentDateUtc = DateTime.UtcNow;
            var expiryDateUtc = model.ExpiryDate == default
                ? paymentDateUtc
                : (model.ExpiryDate.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(model.ExpiryDate, DateTimeKind.Utc)
                    : model.ExpiryDate.ToUniversalTime());

            // The form's date picker defaults to today; a licence that expires on creation
            // can never be renewed. Fall back to the period the subscription Id encodes.
            if (expiryDateUtc <= paymentDateUtc)
            {
                expiryDateUtc = licenseCategory == LicenseCategories.OneYear
                    ? paymentDateUtc.AddYears(1)
                    : paymentDateUtc.AddMonths(4);
            }

            licenseRepo.Add(new LicensTying
            {
                Id = Guid.NewGuid().ToString("N"),
                SchoolSmsId = model.SchoolSmsId,
                SubscriptionId = model.SubscriptionId,
                PaymentDate = paymentDateUtc,
                ExpiryDate = expiryDateUtc,
                Charges = model.Charges,
                AgentUserId = agentUserId,
                LicenseCategory = licenseCategory,
                TimeStatus = TimeStatus.Current,
                RenewalConsumptionStatus = RenewalConsumptionStatus.IsYetToBeConsummed,
            });

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.Message ?? ex.Message;
                throw new InvalidOperationException(
                    "Failed to save licensing record. " + detail,
                    ex);
            }

            return encoded;
        }

        private async Task<string?> ResolveLicensingAgentUserIdAsync(
            string? requestedAgentUserId,
            string? schoolAgentUserId,
            CancellationToken ct)
        {
            var agentRepo = _uow.Repository<Agent>();

            async Task<string?> IfExists(string? userId)
            {
                if (string.IsNullOrWhiteSpace(userId)) return null;
                var found = await agentRepo.FindAsync(a => a.UserId == userId, ct);
                return found?.UserId;
            }

            return await IfExists(requestedAgentUserId)
                   ?? await IfExists(schoolAgentUserId)
                   ?? (await agentRepo.GetAllAsync(ct)).FirstOrDefault()?.UserId;
        }

        /// <summary>Legacy SecurityService.GetNoOfStudent</summary>
        public async Task<string?> UpdateNumberOfStudentsForLicensing(
            string schoolId,
            short noOfStudents,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var schoolInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var school = await schoolInfoRepo.FindAsync(
                v => v.SchoolRegistrationOfficerId == schoolId,
                ct);
            if (school == null)
            {
                return null;
            }

            string key;
            try
            {
                key = FreePlugIn.Decrypt(school.SmsApiKey, true);
            }
            catch
            {
                key = school.SmsApiKey ?? string.Empty;
            }

            school.TotalStudents = FreePlugIn.Encrypt($"{key}:{noOfStudents}", true);
            schoolInfoRepo.Update(school);
            await _uow.SaveChangesAsync(ct);
            return "Has successfully been updated";
        }

        private async Task<string?> GetExpiryDateInternal(string id, CancellationToken ct)
        {
            var current = await _uow.Repository<LicensTying>().FindAsync(
                s => s.SchoolSmsId == id && s.TimeStatus == TimeStatus.Current,
                ct);
            return current?.ExpiryDate.ToLongDateString();
        }

        private async Task RemoveUserFromRoleNameAsync(string userId, string roleName, CancellationToken ct)
        {
            var roleRepository = _uow.Repository<IdentityRole>();
            var role = await roleRepository.FindAsync(v => v.Name == roleName, ct);
            if (role == null || string.IsNullOrWhiteSpace(role.Id))
            {
                return;
            }

            var userRoleRepository = _uow.Repository<IdentityUserRole<string>>();
            var existingRoles = await userRoleRepository.WhereAsync(
                r => r.UserId == userId && r.RoleId == role.Id,
                ct);
            foreach (var eachRole in existingRoles)
            {
                userRoleRepository.Delete(eachRole);
            }
        }
    }
}
