using AutoMapper;
using Microsoft.AspNetCore.Http;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class SecurityExtensionService:ISecurityExtensionService
    {

        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public SecurityExtensionService(
            IUnitOfWork uow,
            IMapper mapper)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }


        public async Task<AccessVerifyReturnValues?> B(string userId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ApplicationUser, string>();
            var user = await repo.FindAsync(u => u.Id == userId, ct);
            return user?.AccessStatus == AccessStatus.Active
                ? null
                : new AccessVerifyReturnValues
                {
                    Value1 = "Sorry! Your Access has been denied.",
                    Value2 = "BashLogOff",
                    Value3 = "Account",
                    Value4 = ""
                };
        }
        public async Task<string> GetAndEncodeCredentialsAndExpiryDate(string userId, CancellationToken ct = default)
        {
            var programRepo = _uow.Repository<ProgramYInfo, byte>();
            var strDateExpiry = Convert.ToString((await programRepo.FindAsync(f => f.Id == 10, ct))?.Machines);
            var decrypted = FreePlugIn.Decrypt(strDateExpiry ?? string.Empty, true);

            var credential = (await GetSmsTokenAndKey(userId, ct)).Replace(':', '~') + '~' + decrypted;
            return FreePlugIn.Encrypt(credential, true);
        }

        public async Task<string> GetAndEncodeCredentials(string userId, CancellationToken ct = default)
        {
            var credential = await GetSmsTokenAndKey(userId, ct);
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                throw new InvalidOperationException("School registration profile was not found for the current user.");
            }

            var studentRepo = _uow.Repository<TxStudentDetail>();
            var noOfStudents = await studentRepo.CountAsync(s =>
                (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning) &&
                s.SchoolInfoId == schoolId, ct);

            var schoolInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var schoolInfo = await schoolInfoRepo.FindAsync(
                c => c.SchoolRegistrationOfficerId == schoolId,
                ct,
                s => s.SchoolRegistrationOfficer.User);
            if (schoolInfo == null)
            {
                throw new InvalidOperationException("School registration profile was not found for the current user.");
            }

            var payload = $"{credential}:{schoolInfo.SchoolAdminAccountantStatus}:{noOfStudents}";
            return FreePlugIn.Encrypt(payload, true);
        }


        public async Task<string> GetSmsTokenAndKey(string userId, CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(userId, ct);
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var school = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == schoolId, ct, v => v.SchoolRegistrationOfficer.User);

            if (school == null) return string.Empty;

            var smsApiToken = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
            var smsApiKey = FreePlugIn.GetUniqueSystemId();
            var smsApiKeyEncode = FreePlugIn.Encrypt(smsApiKey, true);

            if (string.IsNullOrEmpty(school.SmsApiToken))
            {
                school.SmsApiToken = smsApiToken;
                school.SmsApiKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
                repo.Update(school);
                await _uow.SaveChangesAsync(ct);
                return $"{smsApiToken}:{smsApiKeyEncode}";
            }

            // The online platform matches the key already stored for this school.
            // Encrypt(this PC's hardware id) only matches the VPS that registered the school.
            var registeredKey = string.IsNullOrWhiteSpace(school.SmsApiKey)
                ? smsApiKeyEncode
                : school.SmsApiKey;
            return $"{school.SmsApiToken}:{registeredKey}";
        }

        private async Task<string> GetSchoolIdForSchoolRegistrationOfficer(string userId, CancellationToken ct = default)
        {
           return await RegistrySchoolLookup.ResolveSchoolRegistrationOfficerIdAsync(_uow, userId, ct)
                ?? string.Empty;
        }
        public async Task<SchoolInfoDto> GetSystemInfoForSms(string userId, CancellationToken ct = default)
        {
            var schoolInfo = await FindRegistrySchoolForSmsAsync(userId, ct);
            if (schoolInfo == null)
            {
                throw new InvalidOperationException("School registration profile was not found for the current user.");
            }

            var model = _mapper.Map<SchoolInfoDto>(schoolInfo);
            if (model == null)
            {
                throw new InvalidOperationException("Unable to map school registration profile for SMS.");
            }

            var token = model.SmsApiToken;
            try
            {
                if (!string.IsNullOrWhiteSpace(model.SmsApiToken))
                {
                    token = FreePlugIn.Decrypt(model.SmsApiToken, true);
                }
            }
            catch
            {
                // leave token as is
            }

            model.SmsApiToken = token;
            model.SmsApiKey = FreePlugIn.ResolveDisplayedSmsApiKey(model.SmsApiKey);

            if (schoolInfo.SchoolRegistrationOfficer?.User != null)
            {
                model.SchoolRegistrationOfficer ??= new SchoolRegistrationOfficerDto();
                model.SchoolRegistrationOfficer.User = new UserDto
                {
                    Id = schoolInfo.SchoolRegistrationOfficer.User.Id,
                };
            }

            return model;
        }
        private async Task<SchoolTyInfo?> FindRegistrySchoolForSmsAsync(string userId, CancellationToken ct = default)
        {
            return await RegistrySchoolLookup.FindSchoolForRegistryUserAsync(
                _uow,
                userId,
                ct,
                s => s.SchoolRegistrationOfficer!.User!);
        }
    }
}
