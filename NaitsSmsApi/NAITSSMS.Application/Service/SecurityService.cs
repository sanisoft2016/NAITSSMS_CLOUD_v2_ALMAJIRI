using Microsoft.AspNetCore.Http;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    /// <summary>
    /// Strict port of legacy Naits.Services.Implementation.SecurityService (B / Authentication).
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private const string HttpContextItemKeyCurrentUserId = "__SecurityServiceCurrentUserId";

        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private string _uaStatus = string.Empty;
        private DateTime _currentDate = DateTime.Today;
        private DateTime _currentDateOnly = DateTime.Today;
        private DateTime _lastRegAttempt;
        private DateTime _expiryDate;
        private int _daysRemaining;
        private short _noOfDays;

        public SecurityService(IUnitOfWork uow, IHttpContextAccessor httpContextAccessor)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Legacy SecurityService.B() — VerifyAccess.
        /// Machine-bound installs use ProgramYInfo Authentication only.
        /// Cloud: ProgramYInfo row keyed by SchoolTyInfo.SmsApiKey is treated as that binding
        /// (pod fingerprints change). Unbound installs use LicensTying Current, with ProgramYInfo
        /// Authentication as fallback when LicensTying is empty (desktop-imported DBs).
        /// </summary>
        public async Task<AccessVerifyReturnValuesAndMoreDto> B(CancellationToken ct = default)
        {
            try
            {
                _currentDate = DateTime.Now;
                var programInfoRepository = _uow.Repository<ProgramYInfo, byte>();

                var startTimeRow = await programInfoRepository.FindAsync(s => s.Machines == "Start_Time", ct);
                var expiryRow = await programInfoRepository.FindAsync(s => s.Id == 10, ct);
                var statusRow = await programInfoRepository.FindAsync(s => s.Machines == "Status", ct);

                var strDateStart = FreePlugIn.Decrypt(startTimeRow?.Matches ?? string.Empty, true);
                _uaStatus = statusRow?.Matches ?? string.Empty;
                if (_uaStatus.Trim().Length > 3)
                {
                    _uaStatus = FreePlugIn.Decrypt(_uaStatus, true);
                }

                var strDateExpiry = FreePlugIn.Decrypt(expiryRow?.Machines ?? string.Empty, true);
                var dtUaRegDate = DateTimeFromJson(strDateStart).AddDays(-1);
                _expiryDate = DateTimeFromJson(strDateExpiry);

                if (_currentDate < dtUaRegDate)
                {
                    return CreateLoginStatus(
                        "Failed! Your current date has to first set properly",
                        "Failure");
                }

                // Resolve host fingerprint. Never emit the legacy Windows "NIC" error on cloud hosts.
                string strNic;
                try
                {
                    strNic = FreePlugIn.GetUniqueSystemId() ?? string.Empty;
                }
                catch
                {
                    strNic = string.Empty;
                }

                if (string.IsNullOrWhiteSpace(strNic))
                {
                    strNic = (Environment.GetEnvironmentVariable("NAITS_UNIQUE_SYSTEM_ID")
                        ?? Environment.MachineName
                        ?? string.Empty).Trim();
                }

                if (string.IsNullOrWhiteSpace(strNic))
                {
                    // Last resort — cloud/container installs must not hard-fail like desktop WMI.
                    strNic = "ourschoolonline-vps-01";
                }

                var compCodeArrayPublicValue = FreePlugIn.Encrypt(strNic, true);

                // Desktop installs: ProgramYInfo row for Encrypt(NIC) → Authentication() only.
                // Cloud installs skip this — fingerprints change across hosts/pods.
                if (!IsCloudNativeHost())
                {
                    var machineBinding = await programInfoRepository.FindAsync(
                        s => s.Machines == compCodeArrayPublicValue,
                        ct);
                    if (machineBinding != null)
                    {
                        return await AuthenticateWithRenewedLicenseAsync(ct);
                    }
                }

                // Unbound / cloud path: school SmsApiKey + LicensTying Current row.
                var userId = GetCurrentUserId();
                SchoolTyInfo? schoolModel = null;
                var principal = CurrentPrincipal();
                if (principal?.IsInRole("Registry") == true)
                {
                    schoolModel = await _uow.Repository<SchoolTyInfo, string>().FindAsync(
                        c => c.SchoolRegistrationOfficer != null &&
                             c.SchoolRegistrationOfficer.UserId == userId,
                        ct,
                        c => c.SchoolRegistrationOfficer!);
                }
                else if (principal?.IsInRole("Teacher") == true ||
                         principal?.IsInRole("Accounting") == true ||
                         principal?.IsInRole("Accountant") == true)
                {
                    var employee = await _uow.Repository<TxEmployee>().FindAsync(
                        c => c.UserId == userId,
                        ct,
                        r => r.SchoolInfo!);
                    schoolModel = employee?.SchoolInfo;
                }

                if (schoolModel == null)
                {
                    return CreateLoginStatus(
                        "Warning! You need to register this software in order to use this service.<br />Contact Administrator on +2348055068619 or +2348035550938 for solution",
                        "Failure");
                }

                // Legacy registered installs store Encrypt(NIC) both as SchoolTyInfo.SmsApiKey and as a
                // ProgramYInfo.Machines row. Cloud pods never see the original NIC, so look up that
                // registered key directly and use Authentication() (ProgramYInfo Status/Expiry) —
                // the same path desktop used after machine binding.
                if (!string.IsNullOrWhiteSpace(schoolModel.SmsApiKey))
                {
                    var registeredKeyBinding = await programInfoRepository.FindAsync(
                        s => s.Machines == schoolModel.SmsApiKey,
                        ct);
                    if (registeredKeyBinding != null)
                    {
                        return await AuthenticateWithRenewedLicenseAsync(ct);
                    }
                }

                var systemId = compCodeArrayPublicValue;
                var smsApiKeyMatches = string.Equals(schoolModel.SmsApiKey, systemId, StringComparison.Ordinal);

                if (!smsApiKeyMatches && !IsCloudNativeHost())
                {
                    return CreateLoginStatus(
                        "Warning! You need to register this software in order to use this service.<br />Contact Administrator on +2348055068619 or +2348035550938 for solution",
                        "Failure");
                }

                var theCurrentLicence = await _uow.Repository<LicensTying>().FindAsync(
                    x =>
                        x.SchoolSmsId == schoolModel.SchoolRegistrationOfficerId &&
                        x.TimeStatus == TimeStatus.Current,
                    ct);

                if (theCurrentLicence == null)
                {
                    // Desktop-imported DBs often have an active subscription only in ProgramYInfo
                    // (Status/Expiry), with LicensTying empty. Fall back before treating as unsubscribed.
                    var programYAuth = await AuthenticationAsync(ct);
                    if (programYAuth.ReturnMessage == "1"
                        || programYAuth.StatusCode == "2"
                        || (programYAuth.ReturnMessage?.Contains("days remaining", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        return new AccessVerifyReturnValuesAndMoreDto
                        {
                            LoginReturnStatus = programYAuth,
                            AccessVerifyReturnValues = null
                        };
                    }

                    return CreateLoginStatus(
                        "Sorry! You have not made any subscription yet. Please Contact Administrator <br />on +2348035550938 or +2348055068619 for solution",
                        "1");
                }

                if (_currentDate > theCurrentLicence.ExpiryDate)
                {
                    return CreateLoginStatus(
                        "Your Software has expired Contact Administrator <br />on +2348035550938 or +2348055068619 for solution",
                        "1");
                }

                if (!smsApiKeyMatches && IsCloudNativeHost())
                {
                    schoolModel.SmsApiKey = systemId;
                    _uow.Repository<SchoolTyInfo, string>().Update(schoolModel);
                    await _uow.SaveChangesAsync(ct);
                }

                if (_currentDate.AddDays(30) > theCurrentLicence.ExpiryDate)
                {
                    var remainingDays = (int)Math.Round((theCurrentLicence.ExpiryDate - DateTime.Now).TotalDays);
                    return CreateLoginStatus(
                        "Warning: You have only " + remainingDays +
                        " days remaining for your validity to expire. <br /> To continue with this application, contact Administration on +2348055068619 or +2348035550938.",
                        "2");
                }

                return CreateLoginStatus("1", "2");
            }
            catch
            {
                return new AccessVerifyReturnValuesAndMoreDto
                {
                    AccessVerifyReturnValues = new AccessVerifyReturnValues
                    {
                        Value1 = "Failed!!! We have noticed some illegal attempt and we decided to log you out.",
                        Value2 = "BashLogOff",
                        Value3 = "Account",
                        Value4 = string.Empty
                    },
                    LoginReturnStatus = new LoginReturnStatus
                    {
                        ReturnMessage = string.Empty,
                        StatusCode = "Failure"
                    }
                };
            }
        }

        /// <summary>
        /// ProgramYInfo Status/Expiry is what legacy Authentication() trusts.
        /// A renewal that only updated LicensTying leaves Status as Encrypt("2"), which always
        /// returns the hard "SUBSCRIPTION has expired" message. Re-open ProgramYInfo from the
        /// current licence when that licence is still in date.
        /// </summary>
        private async Task<AccessVerifyReturnValuesAndMoreDto> AuthenticateWithRenewedLicenseAsync(CancellationToken ct)
        {
            var auth = await AuthenticationAsync(ct);
            if (!IsHardExpired(auth))
            {
                return new AccessVerifyReturnValuesAndMoreDto
                {
                    LoginReturnStatus = auth,
                    AccessVerifyReturnValues = null
                };
            }

            var restored = await SyncProgramExpiryFromCurrentLicenseAsync(ct);
            if (!restored)
            {
                return new AccessVerifyReturnValuesAndMoreDto
                {
                    LoginReturnStatus = auth,
                    AccessVerifyReturnValues = null
                };
            }

            var refreshed = await AuthenticationAsync(ct);
            return new AccessVerifyReturnValuesAndMoreDto
            {
                LoginReturnStatus = refreshed,
                AccessVerifyReturnValues = null
            };
        }

        public async Task ApplyRenewedProgramExpiryAsync(DateTime expiryDate, CancellationToken ct = default)
        {
            var programInfoRepository = _uow.Repository<ProgramYInfo, byte>();
            var encryptedExpiry = FreePlugIn.Encrypt(DateTimeToJson(expiryDate), true);

            var expiryRow = await programInfoRepository.FindAsync(row => row.Id == 10, ct);
            if (expiryRow != null)
            {
                expiryRow.Machines = encryptedExpiry;
                programInfoRepository.Update(expiryRow);
            }

            // Active subscription plaintext is "123" (seed Status 85pkGugg8Jw=).
            // Encrypt("1") is the 30-day warning; Encrypt("2") is the hard-expired flag.
            var statusRow = await programInfoRepository.FindAsync(row => row.Machines == "Status", ct);
            if (statusRow != null)
            {
                statusRow.Matches = FreePlugIn.Encrypt("123", true);
                programInfoRepository.Update(statusRow);
            }

            var daysRemaining = (ToLocalDate(expiryDate) - DateTime.Today).Days;
            if (daysRemaining < 0)
            {
                daysRemaining = 0;
            }

            var daysRow = await programInfoRepository.FindAsync(row => row.Machines == "Days_Remaining", ct);
            if (daysRow != null)
            {
                daysRow.Matches = daysRemaining.ToString();
                programInfoRepository.Update(daysRow);
            }
        }

        public async Task<bool> SyncProgramExpiryFromCurrentLicenseAsync(CancellationToken ct = default)
        {
            var school = await ResolveCurrentSchoolAsync(ct);
            if (school == null || string.IsNullOrWhiteSpace(school.SchoolRegistrationOfficerId))
            {
                return false;
            }

            var schoolId = school.SchoolRegistrationOfficerId;
            var licenses = await _uow.Repository<LicensTying>().WhereAsync(
                license => license.SchoolSmsId == schoolId && license.TimeStatus == TimeStatus.Current,
                ct);
            var currentLicense = licenses
                .Where(license => IsLicenceStillValid(license.ExpiryDate))
                .OrderByDescending(license => license.ExpiryDate)
                .FirstOrDefault();
            if (currentLicense == null)
            {
                return false;
            }

            await ApplyRenewedProgramExpiryAsync(currentLicense.ExpiryDate, ct);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        private async Task<SchoolTyInfo?> ResolveCurrentSchoolAsync(CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var byOfficer = await _uow.Repository<SchoolTyInfo, string>().FindAsync(
                c => c.SchoolRegistrationOfficer != null &&
                     c.SchoolRegistrationOfficer.UserId == userId,
                ct,
                c => c.SchoolRegistrationOfficer!);
            if (byOfficer != null)
            {
                return byOfficer;
            }

            var employee = await _uow.Repository<TxEmployee>().FindAsync(
                c => c.UserId == userId,
                ct,
                r => r.SchoolInfo!);
            return employee?.SchoolInfo;
        }

        private static bool IsHardExpired(LoginReturnStatus status)
        {
            if (!string.Equals(status.StatusCode, "1", StringComparison.Ordinal))
            {
                return false;
            }

            var message = status.ReturnMessage ?? string.Empty;
            return message.Contains("SUBSCRIPTION has expired", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Software has expired", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLicenceStillValid(DateTime expiryDate) =>
            ToLocalDate(expiryDate) >= DateTime.Today;

        private static DateTime ToLocalDate(DateTime value)
        {
            var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
            return local.Date;
        }

        /// <summary>
        /// Legacy SecurityService.Authentication() — ProgramYInfo Status / Expiry / Days_Remaining.
        /// Used only when the machine NIC is already bound in ProgramYInfo.
        /// </summary>
        private async Task<LoginReturnStatus> AuthenticationAsync(CancellationToken ct)
        {
            var programInfoRepository = _uow.Repository<ProgramYInfo, byte>();
            var expiryRow = await programInfoRepository.FindAsync(f => f.Id == 10, ct);
            var statusRow = await programInfoRepository.FindAsync(f => f.Machines == "Status", ct);
            var daysRemainingRow = await programInfoRepository.FindAsync(f => f.Machines == "Days_Remaining", ct);

            var strDateExpiry = FreePlugIn.Decrypt(expiryRow?.Machines ?? string.Empty, true);
            _uaStatus = statusRow?.Matches ?? string.Empty;
            if (_uaStatus.Trim().Length > 3)
            {
                _uaStatus = FreePlugIn.Decrypt(_uaStatus, true);
            }

            _currentDate = DateTime.Today;
            _currentDateOnly = DateTime.Today;
            _expiryDate = DateTimeFromJson(strDateExpiry);
            _ = short.TryParse(daysRemainingRow?.Matches ?? "0", out var parsedDays);
            _daysRemaining = parsedDays;

            // Status still encrypted (length == 3) means active subscription encoding path.
            if (_uaStatus.Trim().Length == 3)
            {
                if (_currentDate <= _expiryDate)
                {
                    var span = _expiryDate.Subtract(_currentDate);
                    _noOfDays = Convert.ToInt16(span.TotalDays);
                    if (_noOfDays > 0 && _noOfDays <= 30)
                    {
                        _uaStatus = FreePlugIn.Encrypt("1", true);
                        if (statusRow != null)
                        {
                            statusRow.Matches = _uaStatus;
                            programInfoRepository.Update(statusRow);
                        }

                        _daysRemaining = _noOfDays + 1;
                        return await AlmostExpiredAsync(ct);
                    }

                    return new LoginReturnStatus
                    {
                        ReturnMessage = "1",
                        StatusCode = "2"
                    };
                }

                await EncryptExpiredStatusAsync(statusRow, ct);
                return new LoginReturnStatus
                {
                    ReturnMessage =
                        "Your Software has expired Contact Administrator <br />on +2348035550938 or +2348055068619 for solution",
                    StatusCode = "1"
                };
            }

            if (_uaStatus == "1")
            {
                if (_daysRemaining <= 0 || _expiryDate < _currentDateOnly)
                {
                    await EncryptExpiredStatusAsync(statusRow, ct);
                }

                return await AlmostExpiredAsync(ct);
            }

            return new LoginReturnStatus
            {
                ReturnMessage =
                    "Your SUBSCRIPTION has expired and you can no longer use to this service.<br />  Contact Expert on +2348035550938 or +2348055068619 for re-subscription.",
                StatusCode = "1"
            };
        }

        private async Task EncryptExpiredStatusAsync(ProgramYInfo? statusRow, CancellationToken ct)
        {
            _uaStatus = FreePlugIn.Encrypt("2", true);
            if (statusRow == null)
            {
                return;
            }

            statusRow.Matches = _uaStatus;
            _uow.Repository<ProgramYInfo, byte>().Update(statusRow);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Legacy SecurityService.fnAlmost_Expired().
        /// </summary>
        private async Task<LoginReturnStatus> AlmostExpiredAsync(CancellationToken ct)
        {
            await AttemptDecryptionAsync(ct);

            var programInfoRepository = _uow.Repository<ProgramYInfo, byte>();
            if (_currentDateOnly != _lastRegAttempt)
            {
                _daysRemaining -= 1;
                var span = _expiryDate.Subtract(_currentDateOnly);
                _noOfDays = Convert.ToInt16(span.TotalDays);
                if (_daysRemaining < 0 || _noOfDays < _daysRemaining)
                {
                    _daysRemaining = _noOfDays;
                }

                var daysModel = await programInfoRepository.FindAsync(r => r.Machines == "Days_Remaining", ct);
                if (daysModel != null)
                {
                    daysModel.Matches = Convert.ToString(_daysRemaining);
                    programInfoRepository.Update(daysModel);
                }

                var lastDateAccessModel = await programInfoRepository.FindAsync(d => d.Machines == "Attempt", ct);
                if (lastDateAccessModel != null)
                {
                    lastDateAccessModel.Matches = FreePlugIn.Encrypt(DateTimeToJson(_currentDateOnly), true);
                    programInfoRepository.Update(lastDateAccessModel);
                }
            }

            return new LoginReturnStatus
            {
                ReturnMessage =
                    "Warning: You have only " + _daysRemaining +
                    " days remaining for your validity to expire. <br /> To continue with this application, contact Administration on +2348055068619 or +2348035550938.",
                StatusCode = "2"
            };
        }

        private async Task AttemptDecryptionAsync(CancellationToken ct)
        {
            var attemptRow = await _uow.Repository<ProgramYInfo, byte>().FindAsync(
                d => d.Machines == "Attempt",
                ct);
            var strAttempt = FreePlugIn.Decrypt(attemptRow?.Matches ?? string.Empty, true);
            _lastRegAttempt = DateTimeFromJson(strAttempt);
        }

        public string ReturnMessage1()
        {
            return "Failed!This may likely to be Internet Connection issues, please try again later.";
        }

        public async Task<bool> GetSubscriptionConfirmationStatusForRegistrar(CancellationToken ct = default)
        {
            var userId = GetCurrentUserId();
            var school = await ResolveRegistrarSchoolAsync(userId, ct);
            return school?.MyStatus == SubscriptionStatus.HasConfirmed;
        }

        public async Task ConfirmSubscription(CancellationToken ct = default)
        {
            var userId = GetCurrentUserId();
            var school = await ResolveRegistrarSchoolAsync(userId, ct);
            if (school == null)
            {
                return;
            }

            school.MyStatus = SubscriptionStatus.HasConfirmed;
            _uow.Repository<SchoolTyInfo, string>().Update(school);
            await _uow.SaveChangesAsync(ct);
        }

        private async Task<SchoolTyInfo?> ResolveRegistrarSchoolAsync(string userId, CancellationToken ct)
        {
            var schoolRepo = _uow.Repository<SchoolTyInfo, string>();
            return await schoolRepo.FindAsync(
                c => c.SchoolRegistrationOfficer != null && c.SchoolRegistrationOfficer.UserId == userId,
                ct,
                c => c.SchoolRegistrationOfficer!);
        }

        private string GetCurrentUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Items[HttpContextItemKeyCurrentUserId] is string cachedUserId)
            {
                return cachedUserId;
            }

            var principal = CurrentPrincipal();
            var userId = principal?.FindFirstValue(ClaimTypes.PrimarySid)
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal?.FindFirstValue("sub")
                ?? principal?.FindFirstValue("uid")
                ?? principal?.FindFirstValue("user_id");

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated");
            }

            if (httpContext != null)
            {
                httpContext.Items[HttpContextItemKeyCurrentUserId] = userId;
            }

            return userId;
        }

        private ClaimsPrincipal? CurrentPrincipal()
        {
            return _httpContextAccessor.HttpContext?.User ?? Thread.CurrentPrincipal as ClaimsPrincipal;
        }

        private static AccessVerifyReturnValuesAndMoreDto CreateLoginStatus(string returnMessage, string statusCode)
        {
            return new AccessVerifyReturnValuesAndMoreDto
            {
                AccessVerifyReturnValues = null,
                LoginReturnStatus = new LoginReturnStatus
                {
                    ReturnMessage = returnMessage,
                    StatusCode = statusCode
                }
            };
        }

        /// <summary>
        /// Cloud/container hosts cannot use Windows WMI fingerprints; treat them as cloud-native.
        /// </summary>
        private static bool IsCloudNativeHost()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NAITS_UNIQUE_SYSTEM_ID")))
            {
                return true;
            }

            var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            return string.Equals(inContainer, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime DateTimeFromJson(string dateString) =>
            LegacyJsonDate.FromJson(dateString);

        private static string DateTimeToJson(DateTime value) =>
            LegacyJsonDate.ToJson(value);

        public string GetPv()
        {
            return "gsmM!120An";
        }
    }
}
