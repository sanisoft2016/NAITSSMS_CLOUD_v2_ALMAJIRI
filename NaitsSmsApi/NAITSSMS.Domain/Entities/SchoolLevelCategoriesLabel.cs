using System.ComponentModel.DataAnnotations;


namespace NAITSSMS.Domain.Entities
{
    public class SchoolLevelCategoriesLabel
    {
        [StringLength(30)]
        public string GeneralSujectLabel { get; set; } = string.Empty;
        [StringLength(30)]
        public string ReligionSubjectLabel { get; set; } = string.Empty;

        [StringLength(30)]
        public string PlayClassLabel { get; set; } = string.Empty;

        [StringLength(30)]
        public string PreNurseryLabel { get; set; } = string.Empty;
        [StringLength(30)]
        public string NurseryLabel { get; set; } = string.Empty;
        [StringLength(30)]
        public string PrimaryLabel { get; set; } = string.Empty;
        [StringLength(30)]
        public string JssLabel { get; set; } = string.Empty;
        [StringLength(30)]
        public string SsLabel { get; set; } = string.Empty;
        [StringLength(30)]
        public string CustomLevelLabel { get; set; } = string.Empty;
        [StringLength(40)]
        [Key]//One to One Relationship
        public string SchoolInfoId { get; set; } = string.Empty;
        public SchoolTyInfo? SchoolInfo { get; set; }
    }
}
