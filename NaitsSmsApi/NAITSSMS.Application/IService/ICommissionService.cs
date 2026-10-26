using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAITSSMS.Application.DTOs;

namespace NAITSSMS.Application.IService
{
    /// <summary>
    /// Commission oversight across programme learning centres only. Every method is scoped to
    /// centres with a programme profile, so other schools on the platform are never visible.
    /// </summary>
    public interface ICommissionService
    {
        Task<CommissionCentresDirectoryDto> GetCentresDirectoryAsync(CancellationToken ct = default);

        Task<CommissionOversightDto> GetOversightAsync(DateTime? from, DateTime? to, CancellationToken ct = default);

        Task<CommissionOutcomesDto> GetOutcomesAsync(string? session, byte? term, CancellationToken ct = default);

        /// <summary>Null when the centre is not a programme centre.</summary>
        Task<CommissionCentreDetailDto?> GetCentreDetailAsync(
            string centreId, DateTime? from, DateTime? to, CancellationToken ct = default);

        Task<IReadOnlyList<CommissionLearnerSearchRowDto>> SearchLearnersAsync(string query, CancellationToken ct = default);

        /// <summary>Learners who have left the programme, most recent first; <paramref name="outcome"/> is formal, vocational or withdrawn.</summary>
        Task<IReadOnlyList<CommissionLeaverDto>> GetLeaversAsync(string? outcome, CancellationToken ct = default);

        /// <summary>Null when the learner does not belong to a programme centre.</summary>
        Task<CommissionLearnerRecordDto?> GetLearnerRecordAsync(string studentId, CancellationToken ct = default);
    }
}
