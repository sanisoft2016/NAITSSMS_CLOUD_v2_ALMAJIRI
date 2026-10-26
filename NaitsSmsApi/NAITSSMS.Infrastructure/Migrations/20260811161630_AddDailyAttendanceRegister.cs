using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAITSSMS.Infrastructure.Migrations
{
    /// <summary>
    /// Daily attendance register for learners and facilitators.
    /// Written as idempotent SQL because the same DDL is applied by SchemaRepair on databases
    /// restored from the legacy system, where __EFMigrationsHistory does not match this project.
    /// A database may therefore already have these tables when this migration runs.
    /// </summary>
    public partial class AddDailyAttendanceRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "FacilitatorDailyAttendances";
                DROP TABLE IF EXISTS "LearnerDailyAttendances";
                """);
        }
    }
}
