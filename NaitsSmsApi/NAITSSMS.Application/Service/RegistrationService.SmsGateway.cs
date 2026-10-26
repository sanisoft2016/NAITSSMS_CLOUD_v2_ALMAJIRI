using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;

namespace NAITSSMS.Application.Service
{
    /// <summary>
    /// Legacy HomeController SMS / subscription gateway algorithms
    /// (SmsBalance, UpdateSmsBalance, UpdateSmsBalanceExpress, ChangeCredentials,
    /// VerifySubscription, SubscriptionCategoryStatus, NewSystemMigration, SubscriptionRewal).
    /// </summary>
    public partial class RegistrationService
    {
        /// <summary>Legacy RegistrationService.DecodeCredentials — FreePlugIn.Decrypt(credentialEncoded, true).</summary>
        public string DecodeCredentials(string credentialEncoded)
        {
            return FreePlugIn.Decrypt(credentialEncoded, true);
        }

        /// <summary>Legacy RegistrationService.EncodeCredentials — FreePlugIn.Encrypt(credential, true).</summary>
        public string EncodeCredentials(string credential)
        {
            return FreePlugIn.Encrypt(credential, true);
        }

        /// <summary>Legacy GetSmsBalance(token, key).</summary>
        public async Task<string> GetSmsBalanceAsync(string token, string key, CancellationToken ct = default)
        {
            // Legacy: Find SmsPaymentBalance where SchoolSmsInfo.SmsApiToken/Key match.
            // Resolve school first (indexed columns), then balance by PK — avoids Include+nav filter hangs.
            
            var token1 = FreePlugIn.Decrypt(token, true);
            var key1 = FreePlugIn.Decrypt(key, true);



            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(
                v => v.SmsApiToken == token && v.SmsApiKey == key,
                ct);

            if (school == null)
            {
                return "000";
            }

            var smsPaymentBalance = await _uow.Repository<SmsPaymentBalance>()
                .FindAsync(school.SchoolRegistrationOfficerId, ct);

            if (smsPaymentBalance != null)
            {
                return smsPaymentBalance.Amount.ToString(CultureInfo.InvariantCulture) + ":" +
                       smsPaymentBalance.PricePerSms.ToString(CultureInfo.InvariantCulture);
            }

            return "0000"; // Credentials valid, but no SMS units have been purchased yet

        }

        /// <summary>Legacy UpdateSmsBalance(encodedParameter).</summary>
        public async Task<string> UpdateSmsBalanceAsync(string encodedParameter, CancellationToken ct = default)
        {
            var decodeParameters = FreePlugIn.Decrypt(encodedParameter, true);
            var parts = decodeParameters.Split(':');
            if (parts.Length < 5)
            {
                return "000";
            }

            var currentToken = parts[0];
            var currentKey = parts[1];

            var smsPaymentBalanceRepo = _uow.Repository<SmsPaymentBalance>();
            var schoolSmsInfoObject = await smsPaymentBalanceRepo.FindAsync(
                v => v.SchoolSmsInfo!.SmsApiToken.Trim() == currentToken.Trim()
                     && v.SchoolSmsInfo.SmsApiKey.Trim() == currentKey.Trim(),
                ct,
                v => v.SchoolSmsInfo!);

            if (schoolSmsInfoObject == null)
            {
                return "000";
            }

            var initialBalance = schoolSmsInfoObject.Amount;
            schoolSmsInfoObject.Amount = float.Parse(parts[2], CultureInfo.InvariantCulture);

            var smsUsedModel = new SmsUsed
            {
                Id = Guid.NewGuid().ToString(),
                SchoolSmsId = schoolSmsInfoObject.SchoolSmsId,
                NoOfRecipient = short.Parse(parts[3], CultureInfo.InvariantCulture),
                PricePerSms = schoolSmsInfoObject.PricePerSms,
                NoOfPages = byte.Parse(parts[4], CultureInfo.InvariantCulture),
                UsedDate = DateTime.Now,
                BalB4SendindSms = initialBalance,
            };

            _uow.Repository<SmsUsed>().Add(smsUsedModel);
            smsPaymentBalanceRepo.Update(schoolSmsInfoObject);
            await _uow.SaveChangesAsync(ct);
            return "true";
        }

        /// <summary>Legacy UpdateSmsBalanceExpress(parameterEncoded) — including BalB4SendindSms after debit.</summary>
        public async Task<string> UpdateSmsBalanceExpressAsync(string parameterEncoded, CancellationToken ct = default)
        {
            var decodeParameters = FreePlugIn.Decrypt(parameterEncoded, true);
            var parts = decodeParameters.Split(':');
            if (parts.Length < 4)
            {
                return "000";
            }

            var currentToken = parts[0];
            var currentKey = parts[1];
            var noOfRecipient = float.Parse(parts[2], CultureInfo.InvariantCulture);
            var noOfPages = float.Parse(parts[3], CultureInfo.InvariantCulture);

            var smsPaymentBalanceRepo = _uow.Repository<SmsPaymentBalance>();
            var schoolSmsInfoObject = await smsPaymentBalanceRepo.FindAsync(
                v => v.SchoolSmsInfo!.SmsApiToken.Trim() == currentToken.Trim()
                     && v.SchoolSmsInfo.SmsApiKey.Trim() == currentKey.Trim(),
                ct,
                v => v.SchoolSmsInfo!);

            if (schoolSmsInfoObject == null)
            {
                return "000";
            }

            var pricePerSms = schoolSmsInfoObject.PricePerSms;
            var totalSmsCharge = noOfRecipient * noOfPages * pricePerSms;
            if (totalSmsCharge > schoolSmsInfoObject.Amount)
            {
                return "00"; // Insufficient balance
            }

            var smsBalance = schoolSmsInfoObject.Amount - totalSmsCharge;
            schoolSmsInfoObject.Amount = smsBalance;

            // Legacy sets BalB4SendindSms AFTER debit (Amount already reduced).
            var smsUsedModel = new SmsUsed
            {
                Id = Guid.NewGuid().ToString(),
                SchoolSmsId = schoolSmsInfoObject.SchoolSmsId,
                NoOfRecipient = (short)noOfRecipient,
                PricePerSms = schoolSmsInfoObject.PricePerSms,
                NoOfPages = (byte)noOfPages,
                UsedDate = DateTime.Now,
                BalB4SendindSms = schoolSmsInfoObject.Amount,
            };

            _uow.Repository<SmsUsed>().Add(smsUsedModel);
            smsPaymentBalanceRepo.Update(schoolSmsInfoObject);
            await _uow.SaveChangesAsync(ct);
            return "true";
        }

        /// <summary>Legacy UpdateSmsCredential(smsCredential) — no decrypt; token:key:newToken.</summary>
        public async Task<string> UpdateSmsCredentialAsync(string smsCredential, CancellationToken ct = default)
        {
            var parts = smsCredential.Split(':');
            if (parts.Length < 3)
            {
                return "000";
            }

            var currentToken = parts[0];
            var currentKey = parts[1];

            var schoolTyInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var schoolSmsInfoObject = await schoolTyInfoRepo.FindAsync(
                v => v.SmsApiToken.Trim() == currentToken.Trim()
                     && v.SmsApiKey.Trim() == currentKey.Trim(),
                ct);

            if (schoolSmsInfoObject == null)
            {
                return "000";
            }

            schoolSmsInfoObject.SmsApiToken = parts[2];
            schoolTyInfoRepo.Update(schoolSmsInfoObject);
            await _uow.SaveChangesAsync(ct);
            return "true";
        }

        /// <summary>Legacy VerifyCredentials(credentialEncoded) — token~key~jsonExpiryDate.</summary>
        public async Task<bool> VerifyCredentialsAsync(string credentialEncoded, CancellationToken ct = default)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var parts = credentialString.Split('~');
            if (parts.Length < 3)
            {
                return false;
            }

            var token = parts[0];
            var key = parts[1];
            var expiryDate = LegacyJsonDate.FromJson(parts[2]);

            var licenseRepo = _uow.Repository<LicensTying>();
            var todayDate = DateTime.Now;
            var schoolSmsInfoObject = await licenseRepo.FindAsync(
                v => v.SchoolSmsInfo!.SmsApiToken.Trim() == token.Trim()
                     && v.SchoolSmsInfo.SmsApiKey.Trim() == key.Trim()
                     && v.TimeStatus == TimeStatus.Current,
                ct,
                v => v.SchoolSmsInfo!);

            if (schoolSmsInfoObject != null && schoolSmsInfoObject.ExpiryDate >= todayDate)
            {
                schoolSmsInfoObject.ExpiryDate = expiryDate;
                licenseRepo.Update(schoolSmsInfoObject);
                await _uow.SaveChangesAsync(ct);
                return true;
            }

            return false;
        }

        /// <summary>Legacy GetSubscriptionCategoryStatus(credentialEncoded).</summary>
        public async Task<byte> GetSubscriptionCategoryStatusAsync(string credentialEncoded, CancellationToken ct = default)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var parts = credentialString.Split(':');
            if (parts.Length < 2)
            {
                return 2;
            }

            var token = parts[0];
            var key = parts[1];

            var schoolSmsInfoObject = await _uow.Repository<SchoolTyInfo, string>().FindAsync(
                v => v.SmsApiToken.Trim() == token.Trim() && v.SmsApiKey.Trim() == key.Trim(),
                ct);

            if (schoolSmsInfoObject == null)
            {
                return 2; // Unable to verify credentials
            }

            var decrypted = FreePlugIn.Decrypt(schoolSmsInfoObject.SystemImplementationType, true);
            if (decrypted.Contains(":0", StringComparison.Ordinal))
            {
                return 0;
            }

            if (decrypted.Contains(":1", StringComparison.Ordinal))
            {
                return 1;
            }

            return 9;
        }

        /// <summary>
        /// Legacy UpdateNewSystemInfo — updates in memory only (caller persists),
        /// except when invoked alone from NewSystemMigration which then SaveChanges.
        /// </summary>
        public async Task<string> UpdateNewSystemInfoAsync(
            string credentialEncoded,
            bool persist = false,
            CancellationToken ct = default)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var parts = credentialString.Split(':');
            // Guard: legacy indexes [0]/[1] and would throw if short.
            if (parts.Length < 2)
            {
                return "000";
            }

            var token = parts[0];
            var key = parts[1];
            var schoolSmsInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var schoolSmsInfoObject = await schoolSmsInfoRepo.FindAsync(
                v => v.SmsApiToken.Trim() == token.Trim() && v.SmsApiKey.Trim() == key.Trim(),
                ct);
            var newKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
            if (schoolSmsInfoObject != null)
            {
                schoolSmsInfoObject.SmsApiToken = newKey;

                schoolSmsInfoRepo.Update(schoolSmsInfoObject);
                var encode = EncodeCredentials(newKey + ":" + newKey);

                if (persist)
                {
                    await _uow.SaveChangesAsync(ct);
                }

                return encode;
            }

            return "000"; // Un-able to verify Credentials
        }

        /// <summary>Legacy GetSubscriptionRenewalKey(credentialEncoded).</summary>
        public async Task<string> GetSubscriptionRenewalKeyAsync(string credentialEncoded, CancellationToken ct = default)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var parts = credentialString.Split(':');
            // Guard: legacy indexes [0]/[1]/[3] and would throw if short.
            if (parts.Length < 4)
            {
                return "000";
            }

            var token = parts[0];
            var key = parts[1];
            var noOfStudentsInSchool = parts[3];

            var schoolSmsInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var schoolSmsInfoObject = await schoolSmsInfoRepo.FindAsync(
                v => v.SmsApiToken.Trim() == token.Trim() && v.SmsApiKey.Trim() == key.Trim(),
                ct);

            if (schoolSmsInfoObject != null)
            {
                var featureImplType = schoolSmsInfoObject.FeatureImplementationType;
                var noOfStudent = schoolSmsInfoObject.TotalStudents;
                var licenseRepo = _uow.Repository<LicensTying>();
                var todayDate = DateTime.Now;
                var schoolSmsInfoObjects = await licenseRepo.FindAsync(
                    v => v.SchoolSmsInfo!.SmsApiToken.Trim() == token.Trim()
                         && v.SchoolSmsInfo.SmsApiKey.Trim() == key.Trim()
                         && v.TimeStatus == TimeStatus.Current,
                    ct,
                    v => v.SchoolSmsInfo!,
                    v => v.SchoolSmsInfo!.SchoolRegistrationOfficer,
                    v => v.SchoolSmsInfo!.SchoolRegistrationOfficer.User!);

                if (schoolSmsInfoObjects != null && schoolSmsInfoObjects.ExpiryDate >= todayDate)
                {
                    // Null-conditional only: legacy NRE if User missing.
                    if (schoolSmsInfoObjects.SchoolSmsInfo?.SchoolRegistrationOfficer?.User?.AccessStatus ==
                        AccessStatus.Disable)
                    {
                        return "1";
                    }

                    if (schoolSmsInfoObjects.RenewalConsumptionStatus == RenewalConsumptionStatus.HasAreadyBeenConsummed)
                    {
                        return "2:" + featureImplType + ":" + noOfStudent;
                    }

                    if (schoolSmsInfoObjects.RenewalConsumptionStatus == RenewalConsumptionStatus.IsYetToBeConsummed)
                    {
                        var systemId = FreePlugIn.Decrypt(schoolSmsInfoObject.SmsApiKey, true) + ":" + noOfStudentsInSchool;
                        schoolSmsInfoObject.TotalStudents = FreePlugIn.Encrypt(systemId, true);
                        schoolSmsInfoRepo.Update(schoolSmsInfoObject);

                        var paymentDate = LegacyJsonDate.ToJson(schoolSmsInfoObjects.PaymentDate);
                        var expiryDate = LegacyJsonDate.ToJson(schoolSmsInfoObjects.ExpiryDate);
                        var returString = paymentDate + "~" + expiryDate + "~" + schoolSmsInfoObjects.SchoolSmsId + "~" +
                                          schoolSmsInfoObjects.SubscriptionId;

                        schoolSmsInfoObjects.RenewalConsumptionStatus = RenewalConsumptionStatus.HasAreadyBeenConsummed;
                        // Avoid EF tracking conflict with the school instance already updated above.
                        schoolSmsInfoObjects.SchoolSmsInfo = null;
                        licenseRepo.Update(schoolSmsInfoObjects);
                        var encryptDate = FreePlugIn.Encrypt(returString, true);
                        var newCredential = await UpdateNewSystemInfoAsync(credentialEncoded, persist: false, ct);
                        await _uow.SaveChangesAsync(ct);
                        return encryptDate + "|" + newCredential + "|" + featureImplType;
                    }
                }

                return ""; // Your subscription has expired
            }

            return "000"; // Un-able to verify Credentials
        }
    }
}
