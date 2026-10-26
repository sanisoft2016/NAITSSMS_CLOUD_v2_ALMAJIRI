using System;
using System.ComponentModel.DataAnnotations;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    /// <summary>
    /// Marks a school as a learning centre in a Commission programme and records where it is.
    /// A school with no row here is an ordinary school and never appears in Commission views.
    /// Kept beside <see cref="SchoolTyInfo"/> rather than in it, so live school tables are untouched.
    /// </summary>
    public class ProgrammeCentreProfile
    {
        /// <summary>Same value as <see cref="SchoolTyInfo.SchoolRegistrationOfficerId"/>.</summary>
        [Key, MaxLength(40)]
        public required string SchoolInfoId { get; set; }

        [StringLength(60)]
        public string CentreType { get; set; } = string.Empty;

        public byte StateId { get; set; }

        public short? LocalGovernmentId { get; set; }

        [StringLength(80)]
        public string Community { get; set; } = string.Empty;

        /// <summary>The Malam or proprietor responsible for the centre.</summary>
        [StringLength(80)]
        public string ProprietorName { get; set; } = string.Empty;

        public short? YearEstablished { get; set; }

        public DateTime OnboardedAtUtc { get; set; }
    }

    /// <summary>
    /// Programme-specific facts about a learner that the general student record has no place for.
    /// </summary>
    public class ProgrammeLearnerProfile
    {
        /// <summary>Same value as <see cref="TxStudentDetail.UserId"/>.</summary>
        [Key, MaxLength(40)]
        public required string StudentId { get; set; }

        [MaxLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;

        public LearnerProgrammeCategory Category { get; set; }

        public byte? StateOfOriginId { get; set; }

        public short? LocalGovernmentOfOriginId { get; set; }

        /// <summary>The Tsangaya (Qur'anic school) the learner belongs to, where applicable.</summary>
        [StringLength(80)]
        public string Tsangaya { get; set; } = string.Empty;

        /// <summary>Juz (of 30) the learner has memorised, where tracked.</summary>
        public byte? QuranJuzMemorised { get; set; }

        [StringLength(80)]
        public string PriorSchooling { get; set; } = string.Empty;

        public LearnerTransitionStatus TransitionStatus { get; set; }

        public DateTime? TransitionDate { get; set; }

        [StringLength(120)]
        public string TransitionDestination { get; set; } = string.Empty;
    }

    /// <summary>
    /// A centre's own wording for a shared affective/psychomotor item. Items stay global, so live
    /// schools keep their report cards; programme centres can print items that fit their learners.
    /// </summary>
    public class CentreAffectiveItemLabel
    {
        [MaxLength(40)]
        public required string SchoolInfoId { get; set; }

        public byte ItemId { get; set; }

        [StringLength(25)]
        public string Label { get; set; } = string.Empty;
    }
}
