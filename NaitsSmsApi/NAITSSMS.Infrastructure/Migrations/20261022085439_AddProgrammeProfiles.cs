using Microsoft.EntityFrameworkCore.Migrations;
using NAITSSMS.Infrastructure.Data;

#nullable disable

namespace NAITSSMS.Infrastructure.Migrations
{
    /// <summary>
    /// Commission programme side tables: centre profiles, learner profiles and per-centre
    /// affective item labels. Idempotent SQL shared with SchemaRepair, for the same reason as
    /// AddDailyAttendanceRegister: restored databases may already have these tables.
    /// </summary>
    public partial class AddProgrammeProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SchemaRepair.ProgrammeProfileTablesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "CentreAffectiveItemLabels";
                DROP TABLE IF EXISTS "ProgrammeCentreProfiles";
                DROP TABLE IF EXISTS "ProgrammeLearnerProfiles";
                """);
        }
    }
}
