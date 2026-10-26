using NAITSSMS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface ISecurityService
    {
        Task<AccessVerifyReturnValuesAndMoreDto> B(CancellationToken ct = default);
        Task<bool> GetSubscriptionConfirmationStatusForRegistrar(CancellationToken ct = default);
        Task ConfirmSubscription(CancellationToken ct = default);

        /// <summary>
        /// Legacy renewal writes the new expiry into ProgramYInfo (row 10) and resets Status to the
        /// active code Encrypt("123"). Authentication() reads those rows, not LicensTying.
        /// </summary>
        Task ApplyRenewedProgramExpiryAsync(DateTime expiryDate, CancellationToken ct = default);

        /// <summary>
        /// When a renewal was already stored on the current LicensTying row, copy that expiry into
        /// ProgramYInfo so the next admin check stops reporting a stale expired status.
        /// </summary>
        Task<bool> SyncProgramExpiryFromCurrentLicenseAsync(CancellationToken ct = default);
        string ReturnMessage1();
        string GetPv();
    }
}
