using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Enum
{
   public enum QuestionProcessingActionType: byte
    {
        GetAlReadyGeneratedQuestions,
        GetAlReadyGeneratedQuestionsAndBalanceTheRequest,
        GenerateNewQuestions
    }
    public enum AssessmentType : byte
    {
        FirstCA,
        SecondCA,
        ThirdCA
    }
    public enum Role : byte
    {
        CbtTeacher = 0,
        Teacher = 1,
        Both = 2
    }
    public enum AnswerOption : byte
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3,
    }
    public enum ActionType : byte
    {
        Create = 0,
        Delete = 1,
    }
    public enum CurrentTermStatusAndId : byte
    {
        CurrentYearlyAndTermlyAssessmentStatusId = 0,
        TermStatus = 1,
    }
    public enum RenewalConsumptionStatus : byte
    {
        IsYetToBeConsummed = 0,
        HasAreadyBeenConsummed = 1,
    }
    public enum LicenseCategories : byte
    {
        FourMonths,
        OneYear,
        ThreeYears,
        FiveYears,
        Infinity//3,000 years
    }
    public enum UserStatus : byte
    {
        SystemAdmin,
        Agent,
        SchoolAdmin,
        Teacher,
        CbtTeacher,
        Student,
        Accountant,
        Others = 15,
    }
    public enum WeekOfMonth : byte
    {
        First,
        Second,
        Third,
        Fourth,
        Fift,
        Sixth
    }
    public enum NetworkType : byte
    {
        InvalidNo,
        DndLine,
        Mtn,
        Glo,
        Airtel,
        Etisalat,
        Others
    }
    public enum TelNoStatus : byte
    {
        OrdinaryNo,
        DndLine,
        InvalidNo
    }
    public enum SubscriptionStatus : byte
    {
        HasSubscribedButYetToConfirm = 0,
        HasConfirmed = 1,
    }
    public enum SmsImplementationStatus : byte
    {
        HasNotImplementated = 0,
        HasImplementated = 1,
    }
    public enum SystemImplementationType : byte
    {
        PerTerm = 0,
        PerStudentPerTerm = 1,
    }
    public enum PaymentType : byte
    {
        Cash = 0,
        Teller = 1,
        Transfer = 2
    }

    public enum FeatureImplementationType : byte
    {
        ResultProcessingAlone = 0,
        AccountingManagementAlone = 1,
        BothResultProcessingAndAccountingManagement = 2
    }
    public enum SchoolAdminAccountantStatus : byte
    {
        None = 0,
        Exist = 1
    }
    public enum StudentInitialRegistrationStatus : byte
    {
        Fresher,
        Returning
    }
    public enum CardStatus : byte
    {
        First = 0,
        Second = 1,
        Third = 2,
        Forth = 3
    }
    public enum HeadStatus : byte
    {
        HeadTeacher = 0,
        Principal = 1
    }
    public enum AccessStatus : byte
    {
        Active = 0,
        Disable = 1
    }
    public enum TermlyAccessmentStatus : byte
    {
        Current =0,
        ResultProccessing=1,
        Previous =2       
    }
    public enum CasCategory : byte
    {
        OneCA,
        TwoCAs,
        ThreeCAs
    }
    public enum TermStatus : byte
    {
        First,
        Second,
        Third
    }
    public enum TimeStatus : byte
    {
        Current,
        Previous
    }

    public enum EmployeeIncomeStructureStatus : byte
    {
        HasNotSet,
        HasSet
    }

    public enum EmployeeMonthlyPaymentLevel : byte
    {
        HasIssuedPaymentSlip,
        HasConfirmedPayment
    }

    public enum EmployeeQualificationStatus : byte
    {
        
        DegreeInEducation,
        DegreeInSingleHonour,
        PgdEducation,
        Nce,
        Diploma,
        MasterInEducation,
        MasterInSingleHonour,
        Others
    }
    public enum EmployeeMonthlyPaymentStatus : byte
    {
        HasBeenPaid,
        YetToBePaid,
        ShouldNotBePaid
    }

    public enum EmployeeStatus : byte
    {
        Teacher,
        Accountant,
        Security,
        Principal,
        Headmaster,
        Others
    }
    public enum StudentStatus : byte
    {
        Fresher,
        Returning,
        Previous
    }
    public enum ElectiveStatusCode : byte
    {
        OptionalToAllStudents,
        CompulsaryToSomeStudents
    }

    public enum CreatorStatus : byte
    {
        SupperAdmin = 1,
        RemoteAgent,
        LocalAgent,
        SchoolAdmin
    }



    public enum ArmName : byte
    {
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I,
        J,
        K,
        L,
        N,
        O,
        NoArm
    }

    public enum ProcessingLevel : byte
    {
        NurseryClasses,
        PreNursery,
        Nursery1,
        Nursery2,
        PrimaryClasses,
        Primary1,
        Primary2,
        Primary3,
        Primary4,
        Primary5,
        Primary6,
        JuniorClass,
        Jss1,
        Jss2,
        Jss3,
        SeniorClasses,
        Ss1,
        Ss2,
        Ss3
    }
    public enum SubjectStatus : byte
    {
        PlayClass,
        PreNursery,
        General,
        Nursery,
        Primary,
        Jss,
        AllSeniors,
        Science,
        Art,
        Commercial,
        Elective
    }

    public enum StudentLevelCategory : byte
    {
        IsBasic,
        IsSenior
    }

    public enum ClassCategoryStatus : byte
    {
        PlayClass,
        PreNursery,
        Nursery,
        Primary,
        Jss,
        Science,
        Art,
        Commercial,
        None
    }
    public enum CurrentClass : byte
    {
        PlayClass,
        PreNursery,
        Nursery1,
        Nursery2,
        Primary1,
        Primary2,
        Primary3,
        Primary4,
        Primary5,
        Primary6,
        Jss1,
        Jss2,
        Jss3,
        Ss1,
        Ss2,
        Ss3,
    }

    public enum ResponceMessageStatus : byte
    {
        New = 0,
        Replied = 1
    }
    public enum ResponceDirectionStatus : byte
    {
        IsMerchant = 0,
        IsCustomerService = 1
    }
    public enum AddressType : byte
    {
        Residential = 0,
        Shipping = 1,
        Payment = 2,
        Business = 3
    }
    public enum ArmTermlyAssessmentStatus : byte
    {
        Uncompleted = 0,
        Completed = 1
    }
    public enum Religion : byte
    {
        Islam = 0,
        Christianity = 1
    }
    public enum Gender : byte
    {
        Male = 0,
        Female = 1
    }
    public enum FeesPaymentStatus : byte
    {
        None,
        PartPayment,
        Completed
    }

    public enum PrivateMessageStatus : byte
    {
        New = 0,
        Read = 1,
        Replied = 2,
        All = 3
    }

    public enum SmsStatus
    {
        Pending = 0,
        Sent = 1,
        Failed = 2,
        Delivered = 3
    }

    public enum DailyAttendanceStatus : byte
    {
        Present = 0,
        Absent = 1,
        Late = 2,
        Excused = 3
    }

    /// <summary>How a learner came into an Almajiri or out-of-school programme centre.</summary>
    public enum LearnerProgrammeCategory : byte
    {
        Almajiri = 0,
        OutOfSchoolNeverEnrolled = 1,
        OutOfSchoolDroppedOut = 2,
        DisplacedChild = 3
    }

    /// <summary>Where a learner is now, relative to the programme.</summary>
    public enum LearnerTransitionStatus : byte
    {
        Active = 0,
        TransitionedToFormalSchool = 1,
        TransitionedToVocational = 2,
        Withdrawn = 3
    }
}
