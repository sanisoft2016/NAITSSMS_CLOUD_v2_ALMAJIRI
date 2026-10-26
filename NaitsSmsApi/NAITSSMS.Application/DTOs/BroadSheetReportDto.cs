using System.Collections.Generic;

namespace NAITSSMS.Application.DTOs
{
    public class BroadSheetReportDto
    {
        public string Message { get; set; } = string.Empty;
        public BroadSheetSchoolInfoDto? SchoolInfo { get; set; }
        public string SessionLabel { get; set; } = string.Empty;
        public string TermLabel { get; set; } = string.Empty;
        public string ClassLabel { get; set; } = string.Empty;
        public IList<BroadSheetArmSectionDto> Arms { get; set; } = new List<BroadSheetArmSectionDto>();
    }

    public class BroadSheetSchoolInfoDto
    {
        public string SchoolName { get; set; } = string.Empty;
        public string SchoolAddress { get; set; } = string.Empty;
        public string Motto { get; set; } = string.Empty;
        public string SchoolLogoString { get; set; } = string.Empty;
    }

    public class BroadSheetArmSectionDto
    {
        public string ArmLabel { get; set; } = string.Empty;
        public IList<string> Subjects { get; set; } = new List<string>();
        public int StudentCount { get; set; }
        public double ClassHighestAverage { get; set; }
        public double ClassLowestAverage { get; set; }
        public double SubjectAverageScore { get; set; }
        public IList<BroadSheetStudentRowDto> Students { get; set; } = new List<BroadSheetStudentRowDto>();
    }

    public class BroadSheetStudentRowDto
    {
        public int SerialNumber { get; set; }
        public string AdmissionNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public IList<BroadSheetSubjectScoreDto> Scores { get; set; } = new List<BroadSheetSubjectScoreDto>();
        public int SubjectCount { get; set; }
        public double Total { get; set; }
        public double Average { get; set; }
        public string Position { get; set; } = string.Empty;
    }

    public class BroadSheetSubjectScoreDto
    {
        public string DisplayValue { get; set; } = string.Empty;
        public bool IsFail { get; set; }
    }
}
