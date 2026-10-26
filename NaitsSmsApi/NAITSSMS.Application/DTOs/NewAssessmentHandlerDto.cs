using NAITSSMS.Domain.Enum;
using System.Collections.Generic;

namespace NAITSSMS.Application.DTOs
{
    public class NewAssessmentProcessRequestDto
    {
        public NewAssessmentCollectionMode CollectionType { get; set; }
        public CurrentClass? ClassName { get; set; }
        public ArmName? ArmName { get; set; }
        public string AssessmentCategory { get; set; } = "0";
        public string ActionType { get; set; } = "0";
        public byte? PsychoAffectDefaultValue { get; set; }
    }

    public enum NewAssessmentCollectionMode
    {
        WholeSchool = 0,
        PerClass = 1,
        PerArm = 2,
    }

    /// <summary>
    /// Legacy RegistryController.NewAssessmentHandler ViewBag payload for Session/Term/Class/Arm.
    /// </summary>
    public class NewAssessmentHandlerBootstrapDto
    {
        public IList<SelectListItem> Session { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> Term { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> Classes { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> ArmName { get; set; } = new List<SelectListItem>();
    }
}
