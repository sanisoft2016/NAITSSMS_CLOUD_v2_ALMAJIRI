using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Infrastructure.Unitofwork;

namespace NAITSSMS.Application.Service
{
    /// <summary>
    /// Swaps the shared affective/psychomotor item names for a centre's own wording, where the
    /// centre has set one. Schools without <see cref="CentreAffectiveItemLabel"/> rows are untouched.
    /// </summary>
    internal static class AffectiveItemLabels
    {
        public static async Task ApplyForSchoolAsync(
            IUnitOfWork uow,
            string? schoolId,
            IEnumerable<AffectiveAndPsychomotorDto?>? items,
            CancellationToken ct)
        {
            if (items == null || string.IsNullOrWhiteSpace(schoolId))
            {
                return;
            }

            var labels = await LoadAsync(uow, new[] { schoolId }, ct);
            if (labels.TryGetValue(schoolId, out var forSchool))
            {
                Apply(forSchool, items);
            }
        }

        /// <summary>For report rows, which can be resolved to a school through their class arm.</summary>
        public static async Task ApplyByClassArmAsync(
            IUnitOfWork uow,
            IEnumerable<(string? ClassArmId, AffectiveAndPsychomotorDto? Item)> rows,
            CancellationToken ct)
        {
            var list = rows.Where(row => row.Item != null && !string.IsNullOrWhiteSpace(row.ClassArmId)).ToList();
            if (list.Count == 0)
            {
                return;
            }

            var armIds = list.Select(row => row.ClassArmId!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var arms = await uow.Repository<AxClassArm>().WhereAsync(
                arm => armIds.Contains(arm.Id),
                ct,
                arm => arm.Classs!);
            var schoolByArm = arms
                .Where(arm => arm.Classs != null)
                .ToDictionary(arm => arm.Id, arm => arm.Classs!.SchoolInfoId, StringComparer.OrdinalIgnoreCase);

            var labels = await LoadAsync(uow, schoolByArm.Values.Distinct(StringComparer.OrdinalIgnoreCase), ct);
            if (labels.Count == 0)
            {
                return;
            }

            foreach (var (armId, item) in list)
            {
                if (schoolByArm.TryGetValue(armId!, out var schoolId)
                    && labels.TryGetValue(schoolId, out var forSchool)
                    && forSchool.TryGetValue(item!.Id, out var label))
                {
                    item.ItemsName = label;
                }
            }
        }

        private static async Task<Dictionary<string, Dictionary<byte, string>>> LoadAsync(
            IUnitOfWork uow,
            IEnumerable<string> schoolIds,
            CancellationToken ct)
        {
            var ids = schoolIds.ToList();
            var rows = await uow.Repository<CentreAffectiveItemLabel>().WhereAsync(
                row => ids.Contains(row.SchoolInfoId),
                ct);

            return rows
                .Where(row => !string.IsNullOrWhiteSpace(row.Label))
                .GroupBy(row => row.SchoolInfoId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(row => row.ItemId, row => row.Label),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static void Apply(IReadOnlyDictionary<byte, string> labels, IEnumerable<AffectiveAndPsychomotorDto?> items)
        {
            foreach (var item in items)
            {
                if (item != null && labels.TryGetValue(item.Id, out var label))
                {
                    item.ItemsName = label;
                }
            }
        }
    }
}
