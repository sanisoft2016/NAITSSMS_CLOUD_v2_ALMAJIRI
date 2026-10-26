using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CustRewardMgtSys.Infrastructure.Data
{
    public class AppDbContext: IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){ }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            modelBuilder.Entity<ApplicationUser>()
               .Property(u => u.UserName)
               .HasMaxLength(50);
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.NormalizedUserName)
                .HasMaxLength(50);

            modelBuilder.Entity<ApplicationUser>()
               .Property(u => u.Email)
               .HasMaxLength(50);

            modelBuilder.Entity<ApplicationUser>()
               .Property(u => u.NormalizedEmail)
               .HasMaxLength(50);

            modelBuilder.Entity<IdentityRole>()
                .Property(r => r.Name)
                .HasMaxLength(20);

            modelBuilder.Entity<IdentityRole>()
               .Property(r => r.NormalizedName)
               .HasMaxLength(20);

            modelBuilder.Entity<IdentityRole>()
               .Property(r => r.Id)
               .HasMaxLength(40);

            modelBuilder.Entity<IdentityUserRole<string>>()
              .Property(r => r.UserId)
              .HasMaxLength(40);
            modelBuilder.Entity<IdentityUserRole<string>>()
             .Property(r => r.RoleId)
             .HasMaxLength(40);


            modelBuilder.Entity<IdentityUserRole<string>>()
               .HasIndex(p => p.UserId);

            modelBuilder.Entity<IdentityUserRole<string>>()
               .HasIndex(p => p.RoleId);

            modelBuilder.Entity<IdentityRole>()
                .HasIndex(p => p.Name)
               .IsUnique();

            modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.NormalizedUserName)
            .HasDatabaseName("UserNameIndex")
            .IsUnique()
            .HasFilter("\"NormalizedUserName\" IS NOT NULL");

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.NormalizedEmail)
                .HasDatabaseName("EmailIndex");

            //modelBuilder.Entity<ApplicationUser>()
            //  .HasIndex(p => p.UserName)
            //  .IsUnique();

            //modelBuilder.Entity<ApplicationUser>()
            //.HasIndex(p => p.PhoneNumber)
            //.IsUnique();
            // Identity customization
            modelBuilder.Entity<ApplicationUser>()
              .Property(u => u.PasswordHash)
              .HasMaxLength(150);


            modelBuilder.Entity<SchoolLevelCategoriesLabel>()
                .HasKey(l => l.SchoolInfoId);

            modelBuilder.Entity<SchoolTyInfo>()
                .HasOne(s => s.SchoolLevelCategoriesLabel)
                .WithOne(l => l.SchoolInfo)
                .HasForeignKey<SchoolLevelCategoriesLabel>(l => l.SchoolInfoId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-one: TxEmployee (principal) <-> TxEmployeeIncomeStructure (dependent, shared PK)
            // TxEmployeeIncomeStructure.Id is both PK and FK to TxEmployee.Id
            modelBuilder.Entity<TxEmployeeIncomeStructure>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<TxEmployee>()
                .HasOne(e => e.EmployeeIncomeStructure)
                .WithOne(i => i.Employee)
                .HasForeignKey<TxEmployeeIncomeStructure>(i => i.Id)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-one: SchoolTyInfo (principal) <-> SchoolRegistrationOfficer (dependent, shared PK)
            // SchoolRegistrationOfficer.Id is both PK and FK to SchoolTyInfo.SchoolRegistrationOfficerId
            modelBuilder.Entity<SchoolTyInfo>()
              .HasKey(s => s.SchoolRegistrationOfficerId);

            modelBuilder.Entity<SchoolRegistrationOfficer>()
            .HasOne(o => o.SchoolInfo)
            .WithOne(s => s.SchoolRegistrationOfficer)
            .HasForeignKey<SchoolTyInfo>(s => s.SchoolRegistrationOfficerId)
            .OnDelete(DeleteBehavior.Cascade);

            // One-to-one: ApplicationUser (principal) <-> SchoolRegistrationOfficer (dependent)
            // SchoolRegistrationOfficer.UserId is both PK and FK to ApplicationUser.Id
            modelBuilder.Entity<SchoolRegistrationOfficer>()
                .HasOne(o => o.User)
                .WithOne()
                .HasForeignKey<SchoolRegistrationOfficer>(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-one: SchoolTyInfo (principal) <-> SmsPaymentBalance (dependent)
            // SmsPaymentBalance.SchoolSmsId is both PK and FK to SchoolTyInfo (principal key used by default)
            modelBuilder.Entity<SmsPaymentBalance>()
               .HasKey(b => b.SchoolSmsId);

            modelBuilder.Entity<SchoolTyInfo>()
               .HasOne(s => s.SmsBalance)
               .WithOne(b => b.SchoolSmsInfo)
               .HasForeignKey<SmsPaymentBalance>(b => b.SchoolSmsId)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudenXtAccessment>(eb =>
            {
                // Helpful composite indexes based on common filters/joins
                eb.HasIndex(e => new { e.StudentId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("IX_Assess_Student_Term");

                eb.HasIndex(e => new { e.ClassArmId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("IX_Assess_ClassArm_Term");

                eb.HasIndex(e => new { e.SubjectId, e.ClassArmId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("IX_Assess_Subject_ClassArm_Term");

                eb.HasIndex(e => new { e.EmployeeId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("IX_Assess_Teacher_Term");
            });

            // Attendance/comments – one row per student per term + fast class term
            modelBuilder.Entity<StudentsAtteXzndanceAndCommentsReport>(eb =>
            {
                eb.HasIndex(e => new { e.StudentId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("UQ_Attendance_Student_Term")
                  .IsUnique();

                eb.HasIndex(e => new { e.ClassArmId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("IX_Attendance_ClassArm_Term");
            });

            // Daily register – one row per learner per day; drives the term totals above
            modelBuilder.Entity<LearnerDailyAttendance>(eb =>
            {
                eb.ToTable("LearnerDailyAttendances");

                // 'date' (not timestamptz) keeps the unique-per-day index exact and side-steps
                // Npgsql's UTC-kind requirement for timestamp with time zone.
                eb.Property(e => e.AttendanceDate).HasColumnType("date");

                eb.HasIndex(e => new { e.StudentId, e.AttendanceDate })
                  .HasDatabaseName("UQ_LearnerDaily_Student_Date")
                  .IsUnique();

                eb.HasIndex(e => new { e.SchoolInfoId, e.AttendanceDate })
                  .HasDatabaseName("IX_LearnerDaily_School_Date");

                eb.HasIndex(e => new { e.ClassArmId, e.AttendanceDate })
                  .HasDatabaseName("IX_LearnerDaily_ClassArm_Date");

                eb.HasIndex(e => new { e.YearlyAndTermlyStatusId, e.StudentId })
                  .HasDatabaseName("IX_LearnerDaily_Term_Student");
            });

            // Daily register – one row per facilitator per day
            modelBuilder.Entity<FacilitatorDailyAttendance>(eb =>
            {
                eb.ToTable("FacilitatorDailyAttendances");

                eb.Property(e => e.AttendanceDate).HasColumnType("date");

                eb.HasIndex(e => new { e.EmployeeId, e.AttendanceDate })
                  .HasDatabaseName("UQ_FacilitatorDaily_Employee_Date")
                  .IsUnique();

                eb.HasIndex(e => new { e.SchoolInfoId, e.AttendanceDate })
                  .HasDatabaseName("IX_FacilitatorDaily_School_Date");
            });

            // Commission programme side tables – created by SchemaRepair, so no foreign keys
            modelBuilder.Entity<ProgrammeCentreProfile>(eb =>
            {
                eb.ToTable("ProgrammeCentreProfiles");
                eb.HasIndex(e => e.StateId).HasDatabaseName("IX_ProgrammeCentre_State");
            });

            modelBuilder.Entity<ProgrammeLearnerProfile>(eb =>
            {
                eb.ToTable("ProgrammeLearnerProfiles");
                eb.HasIndex(e => e.SchoolInfoId).HasDatabaseName("IX_ProgrammeLearner_School");
            });

            modelBuilder.Entity<CentreAffectiveItemLabel>(eb =>
            {
                eb.ToTable("CentreAffectiveItemLabels");
                eb.HasKey(e => new { e.SchoolInfoId, e.ItemId });
            });

            // Affective & Psychomotor – one row per student-item per term + fast class/teacher term
            modelBuilder.Entity<AffectiveAndPsychomoTxtorAssessment>(eb =>
            {
                // Enforce one score per item for a student in a term
                eb.HasIndex(e => new { e.StudentId, e.YearlyAndTermlyStatusId, e.ItemId })
                  .HasDatabaseName("UQ_Affective_Student_Term_Item")
                  .IsUnique();

                eb.HasIndex(e => new { e.ClassArmId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("IX_Affective_ClassArm_Term");

                eb.HasIndex(e => new { e.FormMasterId, e.YearlyAndTermlyStatusId })
                  .HasDatabaseName("IX_Affective_FormMaster_Term");
            });
            modelBuilder.Entity<SubjectRxCategories>()
                .HasIndex(sc => new { sc.SchoolId, sc.SubjectStatus })
                .HasDatabaseName("IX_SubjectCategories_SchoolId_Status");
            modelBuilder.Entity<SubjectRxCategories>()
                .Property(sc => sc.SubjectStatusLabel)
                .IsRequired(false);

            modelBuilder.Entity<RxSubject>()
                .HasIndex(s => s.SubjectCategoryId)
                .HasDatabaseName("IX_Subject_CategoryId");
        }
        //public DbSet<PaintCategory> PaintCategories { get; set; }
        #region entities

        public DbSet<Term> Terms { get; set; }
         public DbSet<SmsLog> SmsLogs { get; set; }
        public DbSet<ContinuousAssessmentType> ContinuousAssessmentTypes { get; set; }
        public DbSet<CbtSetQuestion> CbtSetQuestions { get; set; }
        public DbSet<CbtQuestionAndAnswer> CbtQuestionsAndAnswers { get; set; }

        public DbSet<LicensTying> Licensings { get; set; }
        public DbSet<SmsPaymentBalance> SmsPaymentBalances { get; set; }
        public DbSet<SmsPurchase> SmsPurchases { get; set; }
        public DbSet<SmsUsed> SmsUseds { get; set; }


        public DbSet<TeacherClassArmTxAssignment> TeacherClassArmAssignments { get; set; }
        public DbSet<SchoolHeadTx> PrincipalDetails { get; set; }
        public DbSet<SchoolTyInfo> SchoolInfos { get; set; }

        public DbSet<TeachTxerSubjectArmAssignment> TeacherSubjectArmAssignments { get; set; }
        public DbSet<TxEmployee> ClassTeachers { get; set; }
        public DbSet<RxClasss> Classes { get; set; }
        public DbSet<AxClassArm> ClassArms { get; set; }
        public DbSet<RxSubject> Subjects { get; set; }//
        public DbSet<TxStudentDetail> StudentsDetails { get; set; }
        public DbSet<StudentsAtteXzndanceAndCommentsReport> StudentsAttendanceReports { get; set; }
        public DbSet<LearnerDailyAttendance> LearnerDailyAttendances { get; set; }
        public DbSet<FacilitatorDailyAttendance> FacilitatorDailyAttendances { get; set; }
        public DbSet<ProgrammeCentreProfile> ProgrammeCentreProfiles { get; set; }
        public DbSet<ProgrammeLearnerProfile> ProgrammeLearnerProfiles { get; set; }
        public DbSet<CentreAffectiveItemLabel> CentreAffectiveItemLabels { get; set; }
        public DbSet<AffectiveXzAndPsychomotor> AffectiveAndPsychomotors { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<State> States { get; set; }
        public DbSet<LocalGovernment> LocalGovernments { get; set; }
        public DbSet<StudenXtAccessment> StudentAccessments { get; set; }
        public DbSet<AffectiveAndPsychomoTxtorAssessment> AffectiveAndPsychomotorAssessments { get; set; }
        public DbSet<TxYearlyAndTermlyStatus> YearlyAndTermlyAssessmentStatus { get; set; }
        public DbSet<TxAcademicSession> AcademicSessions { get; set; }
        public DbSet<SchoolLevelCategoriesLabel> SchoolLevelCategoriesLabels { get; set; }
        public DbSet<SubjectStudentTxAssignment> SubjectStudentAssignments { get; set; }
        public DbSet<SubjectRxCategories> SubjectCategories { get; set; }
        //public DbSet<ProprieTytor> Proprietor { get; set; }
        public DbSet<SchoolHeadTxCategory> SchoolHeadCategories { get; set; }
        public DbSet<ProgramYInfo> ProgramInfos { get; set; }
        public DbSet<StudentTyTermlyFeePayment> StudentTermlyFeePayments { get; set; }
        public DbSet<SetEmployeeSalary> SetEmployeeSalaries { get; set; }
        public DbSet<SetSchoolFeesTr> SetFreshierRegistrationFees { get; set; }
        public DbSet<StudentAcademicTxProgressHistory> StudentAcademicProgressHistories { get; set; }
        public DbSet<StudentTyTermlyFixedPaymentDiscount> FeePaymentDiscounts { get; set; }

        public DbSet<TxEmployeeIncomeStructure> EmployeeIncomeStructures { get; set; }
        public DbSet<TxEmployeesMonthlyPayment> EmployeesMonthlyPayments { get; set; }

        public DbSet<TxYear> Years { get; set; }
        public DbSet<TxMonth> Months { get; set; }

        public DbSet<XyIncome> Incomes { get; set; }
        public DbSet<XyExpenses> Expenses { get; set; }
        public DbSet<XyExpenditureWeek> ExpenditureWeeks { get; set; }
        public DbSet<XyIncomeWeek> IncomeWeeks { get; set; }
        public DbSet<XyExpensesCategory> ExpensesCategories { get; set; }
        public DbSet<XyIncomeCategory> IncomeCategorys { get; set; }
        public DbSet<Agent> Agents { get; set; }


        public DbSet<StudentOPtionalChargesAssignment> StudentOPtionalFeesAssignments { get; set; }
        public DbSet<OptionalXyChargeItems> OptionalFeesItems { get; set; }
        public DbSet<XyOPtionalChargeCategory> OPtionalFeesCategories { get; set; }

        public DbSet<StudentTermlyOptionalPayment> StudentOptionalFeePayments { get; set; }
        public DbSet<StudentTermlyOptionalCharges> StudentTermlyOptionalCharges { get; set; }
        public DbSet<StudentTermlyOptionalPaymentDiscount> StudentTermlyOptionalPaymentDiscounts { get; set; }
        #endregion


        #region save
        public override int SaveChanges()
        {
            var modifiedEntries = ChangeTracker.Entries()
                .Where(x => x.Entity is IAuditableEntity
                    && (x.State == EntityState.Added || x.State == EntityState.Modified));

            foreach (var entry in modifiedEntries)
            {
                IAuditableEntity entity = entry.Entity as IAuditableEntity;
                if (entity != null)
                {
                    string identityName = Thread.CurrentPrincipal.Identity.Name;
                    DateTime now = DateTime.Now;

                    if (entry.State == EntityState.Added)
                    {
                        entity.CreatedBy = identityName;
                        entity.CreatedDate = now;
                    }
                    else
                    {
                        base.Entry(entity).Property(x => x.CreatedBy).IsModified = false;
                        base.Entry(entity).Property(x => x.CreatedDate).IsModified = false;
                    }

                    entity.UpdatedBy = identityName;
                    entity.UpdatedDate = now;
                }
            }

            return base.SaveChanges();
        }
        #endregion
    }

}
