using AutoMapper;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<TxAcademicSession, AcademicSessionDto>().ReverseMap();

            CreateMap<ApplicationUser, UserDto>()
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ForMember(dest => dest.LastName, opt => opt.Ignore());

            CreateMap<TxEmployee, EmployeeDto>()
                .ForMember(dest => dest.EmployeeStatus, opt => opt.MapFrom(src => src.EmployeeStatus.ToString()))
                .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User))
                .ForMember(dest => dest.SchoolInfo, opt => opt.Ignore())
                .ForMember(dest => dest.EmployeeIncomeTyStructure, opt => opt.Ignore());

            CreateMap<StudentDetailsDto, TxStudentDetail>()
                .ForMember(dest => dest.ClassArm, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore())
                .ForMember(dest => dest.SchoolInfo, opt => opt.Ignore())
                .ForMember(dest => dest.YearlyAndTermlyStatus, opt => opt.Ignore());

            CreateMap<TxStudentDetail, StudentDetailsDto>()
                .ForMember(dest => dest.ClassArm, opt => opt.MapFrom(src => src.ClassArm))
                .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User))
                .ForMember(dest => dest.SchoolInfo, opt => opt.Ignore())
                .ForMember(dest => dest.YearlyAndTermlyStatus, opt => opt.Ignore())
                .ForMember(dest => dest.StudentMobileNo, opt => opt.MapFrom(src => src.User != null ? src.User.PhoneNumber : null))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.User != null ? src.User.FirstName : string.Empty))
                .ForMember(dest => dest.MiddleName, opt => opt.MapFrom(src => src.User != null ? src.User.MiddleName : string.Empty))
                .ForMember(dest => dest.SurName, opt => opt.MapFrom(src => src.User != null ? src.User.SurName : string.Empty))
                .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.User != null ? src.User.Gender : default(Gender)));

            CreateMap<ApplicationUser, ApplicationUserDto>();
            CreateMap<AxClassArm, ClassArmDto>()
                .ForMember(dest => dest.Classs, opt => opt.MapFrom(src => src.Classs));
            CreateMap<RxClasss, ClassDto>();

            CreateMap<SubjectRxCategories, SubjectCategoriesDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => ParseSubjectCategoryId(src.Id)))
                .ForMember(dest => dest.SubjectStatusLabel, opt => opt.MapFrom(src => src.SubjectStatusLabel ?? string.Empty))
                .ForMember(dest => dest.SchoolId, opt => opt.MapFrom(src => src.SchoolId ?? string.Empty))
                .ForMember(dest => dest.SchoolInfo, opt => opt.Ignore());

            CreateMap<RxSubject, SubjectDto>()
                .ForMember(dest => dest.SubjectCategory, opt => opt.MapFrom(src => src.SubjectCategory));

            CreateMap<SchoolTyInfo, SchoolInfoDto>()
                .ForMember(dest => dest.AccountingStartTermId, opt => opt.MapFrom(src => ParseAccountingStartTermId(src.AccountingStartTermId)))
                .ForMember(dest => dest.SchoolRegistrationOfficer, opt => opt.Ignore())
                .ForMember(dest => dest.Teacher, opt => opt.Ignore())
                .ForMember(dest => dest.Subject, opt => opt.Ignore());
        }

        private static short ParseSubjectCategoryId(string? value)
        {
            return short.TryParse(value, out var categoryId) ? categoryId : (short)0;
        }

        private static int? ParseAccountingStartTermId(string? value)
        {
            return int.TryParse(value, out var termId) ? termId : null;
        }
    }
}
