using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class RemarkDto
    {
    }

    public class AffectiveAssessment
    {
        public string Id { get; set; }
        [Required]
        public byte Attentiveness { get; set; }
        [Required]
        public byte Honesty { get; set; }
        [Required]
        public byte Neatness { get; set; }
        [Required]
        public byte Politeness { get; set; }
        [Required]
        public byte Punctuality { get; set; }
        [Display(Name = "Relationship With Others")]
        public byte RelationshipWithOthers { get; set; }

        public string StudentsAccessmentId { get; set; }
        public StudentsAccessmentDto StudentsAccessment { get; set; }
        public string StudentId { get; set; }
        public StudentDetailsDto Student { get; set; }

    }

    public class PsychomotorAssessment  
    {
        
        public string Id { get; set; }
        [Required]
        [Display(Name = "Drawing And Painting")]
        public byte DrawingAndPainting { get; set; }
        [Required]
        [Display(Name = "Hand Writing")]
        public byte HandWriting { get; set; }
        [Required]
        public byte Hobies { get; set; }
        [Required]
        [Display(Name = "Speach Fluency")]
        public byte SpeachFluency { get; set; }
        [Required]
        [Display(Name = "Club and Society")]
        public byte ClubAndSociety { get; set; }
        [Required]
        [Display(Name = "Sport and Game")]
        public byte SportAndGame { get; set; }
    }
}
