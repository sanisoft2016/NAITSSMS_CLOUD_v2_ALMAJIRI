using NAITSSMS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.IService
{
    public interface IClassAndArmService
    {
        Task<IEnumerable<ClassDto>> GetClass(string userId, CancellationToken ct = default);
        Task<bool> CreateClass(ClassFormDto model, string userId, CancellationToken ct = default);
        Task<bool> UpdateClass(ClassFormDto model, string userId, CancellationToken ct = default);
        Task<bool> DeleteClass(string classId, string userId, CancellationToken ct = default);
        Task<IEnumerable<ClassArmDto>> GetClasssArm(string classId, CancellationToken ct = default);
        Task<bool> CreatClassArm(ClassArmDto model, CancellationToken ct = default);

        /// <summary>Returns true when a class with the same ClassName already exists for the school.</summary>
        Task<bool> IsClassAlreadyExist(CurrentClass className, string schoolId, CancellationToken ct = default);

        /// <summary>Returns true when an arm with the same ArmName already exists inside the class.</summary>
        Task<bool> IsClassArmAlreadyExist(ArmName armName, string classId, CancellationToken ct = default);

        /// <summary>Deletes an arm if it contains no students. Returns false when students exist.</summary>
        Task<bool> DeleteClassArm(string armId, CancellationToken ct = default);

        /// <summary>Updates ArmName/FormMaster on an arm. Returns false when the new name already belongs to another arm in the same class.</summary>
        Task<bool> UpdateClassArm(ClassArmDto model, CancellationToken ct = default);
    }
}
