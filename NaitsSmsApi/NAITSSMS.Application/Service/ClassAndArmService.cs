using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class ClassAndArmService : IClassAndArmService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISchoolScopeResolver _schoolScopeResolver;

        public ClassAndArmService(IUnitOfWork uow, ISchoolScopeResolver schoolScopeResolver)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
        }

        // ── 1. Get all classes for a school ──────────────────────────────────
        public async Task<IEnumerable<ClassDto>> GetClass(string userId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var classRepo = _uow.Repository<RxClasss>();
            var classes   = await classRepo.WhereAsync(s => s.SchoolInfoId == schoolId, ct);
            return classes.Select(c => new ClassDto
            {
                Id           = c.Id,
                SNo          = c.SNo,
                ClassName    = c.ClassName,
                ClassLabel   = c.ClassLabel,
                SchoolInfoId = c.SchoolInfoId,
            });
        }

        // ── 2. Create a class ─────────────────────────────────────────────────
        public async Task<bool> CreateClass(ClassFormDto model, string userId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var classRepo = _uow.Repository<RxClasss>();

            var schoolCount    = await classRepo.CountAsync(c => c.SchoolInfoId == schoolId, ct);
            model.SNo          = (byte)(schoolCount + 1);
            model.Id           = Guid.NewGuid().ToString();
            model.SchoolInfoId = schoolId;

            var entity = new RxClasss
            {
                Id           = model.Id,
                SNo          = model.SNo,
                ClassName    = model.ClassName,
                ClassLabel   = model.ClassLabel,
                SchoolInfoId = model.SchoolInfoId,
            };

            classRepo.Add(entity);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        // ── 2b. Update a class ────────────────────────────────────────────────
        // Mirrors legacy RegistrationService.UpdateClass / Registry EditClass POST.
        public async Task<bool> UpdateClass(ClassFormDto model, string userId, CancellationToken ct = default)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
                return false;

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var classRepo = _uow.Repository<RxClasss>();
            var schoolClasses = await classRepo.WhereAsync(c => c.SchoolInfoId == schoolId, ct);

            if (string.IsNullOrWhiteSpace(model.ClassLabel))
                model.ClassLabel = model.ClassName.ToString();

            var classId = model.Id.Trim();
            var existingRow = schoolClasses.FirstOrDefault(c => ClassIdsEqual(c.Id, classId));
            if (existingRow == null)
                return false;

            model.Id = existingRow.Id;
            model.SchoolInfoId = schoolId;

            var nameConflict = schoolClasses.FirstOrDefault(c => c.ClassName == model.ClassName);
            if (nameConflict != null && !ClassIdsEqual(nameConflict.Id, model.Id))
                return false;

            var normalizedLabel = model.ClassLabel.Trim();
            var labelConflict = schoolClasses.FirstOrDefault(c =>
                string.Equals(c.ClassLabel?.Trim(), normalizedLabel, StringComparison.OrdinalIgnoreCase));
            if (labelConflict != null && !ClassIdsEqual(labelConflict.Id, model.Id))
                return false;

            var existing = await classRepo.FindAsync(existingRow.Id, ct);
            if (existing == null)
                return false;

            existing.ClassName = model.ClassName;
            existing.ClassLabel = normalizedLabel;
            classRepo.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        private static bool ClassIdsEqual(string? left, string? right) =>
            string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

        // ── 2c. Delete a class ────────────────────────────────────────────────
        // Mirrors legacy RegistrationService.DeleteClass / Registry DeleteClass.
        public async Task<bool> DeleteClass(string classId, string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(classId))
                return false;

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var classRepo = _uow.Repository<RxClasss>();
            var armRepo = _uow.Repository<AxClassArm>();
            var studentRepo = _uow.Repository<TxStudentDetail>();

            var schoolClasses = await classRepo.WhereAsync(c => c.SchoolInfoId == schoolId, ct);
            var classRow = schoolClasses.FirstOrDefault(c => ClassIdsEqual(c.Id, classId));
            if (classRow == null)
                return false;

            var resolvedClassId = classRow.Id;
            var arms = await armRepo.WhereAsync(a => a.ClassId == resolvedClassId, ct);
            var armIds = arms.Select(a => a.Id).ToList();

            if (armIds.Count > 0)
            {
                var hasStudents = await studentRepo.CountAsync(
                    s => s.SchoolInfoId == schoolId && armIds.Contains(s.ClassArmId),
                    ct) > 0;
                if (hasStudents)
                    return false;

                foreach (var arm in arms)
                {
                    var trackedArm = await armRepo.FindAsync(arm.Id, ct);
                    if (trackedArm != null)
                        armRepo.Delete(trackedArm);
                }
            }

            var existingClass = await classRepo.FindAsync(resolvedClassId, ct);
            if (existingClass == null)
                return false;

            classRepo.Delete(existingClass);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        // ── 3. Get all arms for a class ───────────────────────────────────────
        public async Task<IEnumerable<ClassArmDto>> GetClasssArm(string classId, CancellationToken ct = default)
        {
            var armRepo = _uow.Repository<AxClassArm>();
            var arms    = await armRepo.WhereAsync(a => a.ClassId == classId, ct, a => a.Classs);
            return arms.Select(a => new ClassArmDto
            {
                Id         = a.Id,
                SNo        = a.SNo,
                ClassId    = a.ClassId,
                ArmName    = a.ArmName,
                FormMaster = a.FormMaster,
            });
        }

        // ── 4. Create a class arm ─────────────────────────────────────────────
        public async Task<bool> CreatClassArm(ClassArmDto model, CancellationToken ct = default)
        {
            var armRepo = _uow.Repository<AxClassArm>();

            var classArmCount = await armRepo.CountAsync(a => a.ClassId == model.ClassId, ct);
            model.SNo = (byte)(classArmCount + 1);
            model.Id  = Guid.NewGuid().ToString();

            var entity = new AxClassArm
            {
                Id         = model.Id,
                SNo        = model.SNo,
                ClassId    = model.ClassId,
                ArmName    = model.ArmName,
                FormMaster = model.FormMaster ?? string.Empty,
            };

            armRepo.Add(entity);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        // ── 5. Uniqueness check: class ────────────────────────────────────────
        public async Task<bool> IsClassAlreadyExist(CurrentClass className, string schoolId, CancellationToken ct = default)
        {
            var resolvedSchoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(schoolId, ct);
            var classRepo = _uow.Repository<RxClasss>();
            var count     = await classRepo.CountAsync(
                c => c.ClassName == className && c.SchoolInfoId == resolvedSchoolId, ct);
            return count > 0;
        }

        // ── 6. Uniqueness check: class arm ────────────────────────────────────
        public async Task<bool> IsClassArmAlreadyExist(ArmName armName, string classId, CancellationToken ct = default)
        {
            var armRepo = _uow.Repository<AxClassArm>();
            var count   = await armRepo.CountAsync(
                a => a.ArmName == armName && a.ClassId == classId, ct);
            return count > 0;
        }

        // ── 7. Delete a class arm ─────────────────────────────────────────────
        // Mirrors legacy: returns false when the arm still has students.
        public async Task<bool> DeleteClassArm(string armId, CancellationToken ct = default)
        {
            var armRepo     = _uow.Repository<AxClassArm>();
            var studentRepo = _uow.Repository<TxStudentDetail>();

            var hasStudents = await studentRepo.CountAsync(s => s.ClassArmId == armId, ct) > 0;
            if (hasStudents)
                return false;

            var deleted = await armRepo.DeleteAsync(armId, ct);
            if (deleted)
                await _uow.SaveChangesAsync(ct);
            return deleted;
        }

        // ── 8. Update a class arm ─────────────────────────────────────────────
        // Returns false when the new ArmName already belongs to another arm in the same class.
        public async Task<bool> UpdateClassArm(ClassArmDto model, CancellationToken ct = default)
        {
            var armRepo = _uow.Repository<AxClassArm>();

            var duplicate = await armRepo.FindAsync(
                a => a.ArmName == model.ArmName && a.ClassId == model.ClassId && a.Id != model.Id, ct);
            if (duplicate != null)
                return false;

            var existing = await armRepo.FindAsync(model.Id, ct);
            if (existing == null)
                return false;

            existing.ArmName    = model.ArmName;
            existing.FormMaster = model.FormMaster ?? string.Empty;
            armRepo.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return true;
        }
    }
}
