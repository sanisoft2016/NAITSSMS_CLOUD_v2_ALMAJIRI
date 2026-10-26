using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace NAITSSMS.Infrastructure.Data
{
    public static class SchemaRepair
    {
        /// <summary>
        /// Idempotent DDL applied on every start, in every environment. Production databases were
        /// restored from the legacy system so __EFMigrationsHistory cannot be trusted and
        /// Database.Migrate() is not run there — new tables must be created defensively here.
        /// </summary>
        public static async Task ApplyAsync(AppDbContext db)
        {
            await ApplyClassTeachersIdRepairAsync(db);
            await ApplyDailyAttendanceTablesAsync(db);
            await db.Database.ExecuteSqlRawAsync(ProgrammeProfileTablesSql);
        }

        /// <summary>
        /// Commission programme side tables and the absence index Commission oversight relies on.
        /// Shared with the AddProgrammeProfiles migration so a fresh database and a restored one end
        /// up with identical DDL.
        /// </summary>
        public const string ProgrammeProfileTablesSql = """
            CREATE TABLE IF NOT EXISTS "ProgrammeCentreProfiles" (
                "SchoolInfoId" character varying(40) NOT NULL,
                "CentreType" character varying(60) NOT NULL DEFAULT '',
                "StateId" smallint NOT NULL,
                "LocalGovernmentId" smallint NULL,
                "Community" character varying(80) NOT NULL DEFAULT '',
                "ProprietorName" character varying(80) NOT NULL DEFAULT '',
                "YearEstablished" smallint NULL,
                "OnboardedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_ProgrammeCentreProfiles" PRIMARY KEY ("SchoolInfoId")
            );

            CREATE INDEX IF NOT EXISTS "IX_ProgrammeCentre_State"
                ON "ProgrammeCentreProfiles" ("StateId");

            CREATE TABLE IF NOT EXISTS "ProgrammeLearnerProfiles" (
                "StudentId" character varying(40) NOT NULL,
                "SchoolInfoId" character varying(40) NOT NULL DEFAULT '',
                "Category" smallint NOT NULL DEFAULT 0,
                "StateOfOriginId" smallint NULL,
                "LocalGovernmentOfOriginId" smallint NULL,
                "Tsangaya" character varying(80) NOT NULL DEFAULT '',
                "QuranJuzMemorised" smallint NULL,
                "PriorSchooling" character varying(80) NOT NULL DEFAULT '',
                "TransitionStatus" smallint NOT NULL DEFAULT 0,
                "TransitionDate" timestamp with time zone NULL,
                "TransitionDestination" character varying(120) NOT NULL DEFAULT '',
                CONSTRAINT "PK_ProgrammeLearnerProfiles" PRIMARY KEY ("StudentId")
            );

            CREATE INDEX IF NOT EXISTS "IX_ProgrammeLearner_School"
                ON "ProgrammeLearnerProfiles" ("SchoolInfoId");

            -- Absences are a small fraction of register marks; at-risk detection starts here.
            CREATE INDEX IF NOT EXISTS "IX_LearnerDaily_Absences"
                ON "LearnerDailyAttendances" ("AttendanceDate", "SchoolInfoId") INCLUDE ("StudentId")
                WHERE "Status" = 1;

            CREATE TABLE IF NOT EXISTS "CentreAffectiveItemLabels" (
                "SchoolInfoId" character varying(40) NOT NULL,
                "ItemId" smallint NOT NULL,
                "Label" character varying(25) NOT NULL DEFAULT '',
                CONSTRAINT "PK_CentreAffectiveItemLabels" PRIMARY KEY ("SchoolInfoId", "ItemId")
            );
            """;

        /// <summary>
        /// Daily attendance register for learners and facilitators (per person, per day).
        /// </summary>
        private static async Task ApplyDailyAttendanceTablesAsync(AppDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "LearnerDailyAttendances" (
                    "Id" character varying(40) NOT NULL,
                    "AttendanceDate" date NOT NULL,
                    "Status" smallint NOT NULL DEFAULT 0,
                    "Remark" character varying(150) NOT NULL DEFAULT '',
                    "MarkedByUserId" character varying(40) NULL,
                    "MarkedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
                    "SchoolInfoId" character varying(40) NOT NULL,
                    "StudentId" character varying(40) NOT NULL,
                    "ClassArmId" character varying(40) NOT NULL,
                    "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
                    CONSTRAINT "PK_LearnerDailyAttendances" PRIMARY KEY ("Id")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "UQ_LearnerDaily_Student_Date"
                    ON "LearnerDailyAttendances" ("StudentId", "AttendanceDate");
                CREATE INDEX IF NOT EXISTS "IX_LearnerDaily_School_Date"
                    ON "LearnerDailyAttendances" ("SchoolInfoId", "AttendanceDate");
                CREATE INDEX IF NOT EXISTS "IX_LearnerDaily_ClassArm_Date"
                    ON "LearnerDailyAttendances" ("ClassArmId", "AttendanceDate");
                CREATE INDEX IF NOT EXISTS "IX_LearnerDaily_Term_Student"
                    ON "LearnerDailyAttendances" ("YearlyAndTermlyStatusId", "StudentId");

                CREATE TABLE IF NOT EXISTS "FacilitatorDailyAttendances" (
                    "Id" character varying(40) NOT NULL,
                    "AttendanceDate" date NOT NULL,
                    "Status" smallint NOT NULL DEFAULT 0,
                    "Remark" character varying(150) NOT NULL DEFAULT '',
                    "MarkedByUserId" character varying(40) NULL,
                    "MarkedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
                    "SchoolInfoId" character varying(40) NOT NULL,
                    "EmployeeId" character varying(40) NOT NULL,
                    "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
                    CONSTRAINT "PK_FacilitatorDailyAttendances" PRIMARY KEY ("Id")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "UQ_FacilitatorDaily_Employee_Date"
                    ON "FacilitatorDailyAttendances" ("EmployeeId", "AttendanceDate");
                CREATE INDEX IF NOT EXISTS "IX_FacilitatorDaily_School_Date"
                    ON "FacilitatorDailyAttendances" ("SchoolInfoId", "AttendanceDate");
                """);
        }

        /// <summary>
        /// Some databases were created without the ClassTeachers.Id column even though
        /// the EF model expects it as the primary key for TxEmployee.
        /// </summary>
        private static async Task ApplyClassTeachersIdRepairAsync(AppDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync("""
                DO $repair$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'ClassTeachers'
                          AND column_name = 'Id'
                    ) THEN
                        ALTER TABLE "ClassTeachers" ADD COLUMN "Id" character varying(40);

                        UPDATE "ClassTeachers"
                        SET "Id" = "UserId"
                        WHERE "Id" IS NULL OR btrim("Id") = '';

                        UPDATE "ClassTeachers"
                        SET "Id" = gen_random_uuid()::text
                        WHERE "Id" IS NULL OR btrim("Id") = '';

                        ALTER TABLE "ClassTeachers" ALTER COLUMN "Id" SET NOT NULL;

                        IF NOT EXISTS (
                            SELECT 1
                            FROM pg_constraint
                            WHERE conname = 'PK_ClassTeachers'
                        ) THEN
                            ALTER TABLE "ClassTeachers"
                                ADD CONSTRAINT "PK_ClassTeachers" PRIMARY KEY ("Id");
                        END IF;
                    END IF;
                END
                $repair$;
                """);
        }
    }
}
