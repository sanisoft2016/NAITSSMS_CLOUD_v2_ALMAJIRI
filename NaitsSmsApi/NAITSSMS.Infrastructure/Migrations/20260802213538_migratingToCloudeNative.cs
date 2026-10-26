using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAITSSMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class migratingToCloudeNative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubjectCategories_SchoolId",
                table: "SubjectCategories");

            migrationBuilder.RenameIndex(
                name: "IX_Subjects_SubjectCategoryId",
                table: "Subjects",
                newName: "IX_Subject_CategoryId");

            migrationBuilder.AlterColumn<string>(
                name: "SubjectStatusLabel",
                table: "SubjectCategories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectCategories_SchoolId_Status",
                table: "SubjectCategories",
                columns: new[] { "SchoolId", "SubjectStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubjectCategories_SchoolId_Status",
                table: "SubjectCategories");

            migrationBuilder.RenameIndex(
                name: "IX_Subject_CategoryId",
                table: "Subjects",
                newName: "IX_Subjects_SubjectCategoryId");

            migrationBuilder.AlterColumn<string>(
                name: "SubjectStatusLabel",
                table: "SubjectCategories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectCategories_SchoolId",
                table: "SubjectCategories",
                column: "SchoolId");
        }
    }
}
