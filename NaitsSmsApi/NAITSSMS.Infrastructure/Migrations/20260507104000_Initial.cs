using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NAITSSMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AffectiveAndPsychomotors",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    ItemsName = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffectiveAndPsychomotors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UserName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MiddleName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SurName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Gender = table.Column<byte>(type: "smallint", nullable: false),
                    AccessStatus = table.Column<byte>(type: "smallint", nullable: false),
                    UserStatus = table.Column<byte>(type: "smallint", nullable: false),
                    NormalizedUserName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramInfos",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Machines = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Matches = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolHeadCategories",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    HeadStatus = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolHeadCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    TermStatus = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "character varying(40)", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    FullAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SchoolRegId = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Agents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(40)", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RoleId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(40)", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "States",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegionId = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_States", x => x.Id);
                    table.ForeignKey(
                        name: "FK_States_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolRegistrationOfficer",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FullAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignatureImageString = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    AccessStatus = table.Column<byte>(type: "smallint", nullable: false),
                    AgentUserId = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolRegistrationOfficer", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_SchoolRegistrationOfficer_Agents_AgentUserId",
                        column: x => x.AgentUserId,
                        principalTable: "Agents",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolRegistrationOfficer_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalGovernments",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateId = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalGovernments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalGovernments_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolInfos",
                columns: table => new
                {
                    SchoolRegistrationOfficerId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SsNo = table.Column<short>(type: "smallint", nullable: false),
                    SchoolName = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    SchoolAddress = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Telephone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Website = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    SchoolLogoString = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Motto = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    SchoolAdminAccountantStatus = table.Column<byte>(type: "smallint", nullable: false),
                    AccountingStartTermId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ElectiveAccessStatus = table.Column<byte>(type: "smallint", nullable: false),
                    SystemImplementationType = table.Column<string>(type: "character varying(162)", maxLength: 162, nullable: false),
                    MyStatus = table.Column<byte>(type: "smallint", nullable: false),
                    SmsApiKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SmsApiToken = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    TotalStudents = table.Column<string>(type: "character varying(170)", maxLength: 170, nullable: false),
                    FeatureImplementationType = table.Column<string>(type: "character varying(162)", maxLength: 162, nullable: false),
                    NetworkImplementationType = table.Column<string>(type: "character varying(162)", maxLength: 162, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolInfos", x => x.SchoolRegistrationOfficerId);
                    table.ForeignKey(
                        name: "FK_SchoolInfos_SchoolRegistrationOfficer_SchoolRegistrationOff~",
                        column: x => x.SchoolRegistrationOfficerId,
                        principalTable: "SchoolRegistrationOfficer",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcademicSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Session = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    SchoolInfoId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicSessions_SchoolInfos_SchoolInfoId",
                        column: x => x.SchoolInfoId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SNo = table.Column<byte>(type: "smallint", nullable: false),
                    ClassName = table.Column<byte>(type: "smallint", nullable: false),
                    ClassLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolInfoId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classes_SchoolInfos_SchoolInfoId",
                        column: x => x.SchoolInfoId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassTeachers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FullAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SignatureImageString = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    EmployeeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeIncomeStructureStatus = table.Column<byte>(type: "smallint", nullable: false),
                    EmployeeIdNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Decipline = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmployeeQualificationStatus = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassTeachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassTeachers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassTeachers_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContinuousAssessmentTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", nullable: false),
                    AssessmentType = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContinuousAssessmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContinuousAssessmentTypes_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenditureWeeks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    WeekOfMonth = table.Column<byte>(type: "smallint", nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenditureWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenditureWeeks_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpensesCategories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpensesCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpensesCategories_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeCategorys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeCategorys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeCategorys_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeWeeks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    WeekOfMonth = table.Column<byte>(type: "smallint", nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeWeeks_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Licensings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolSmsId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AgentUserId = table.Column<string>(type: "character varying(40)", nullable: false),
                    LicenseCategory = table.Column<byte>(type: "smallint", nullable: false),
                    Charges = table.Column<float>(type: "real", nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    RenewalConsumptionStatus = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licensings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Licensings_Agents_AgentUserId",
                        column: x => x.AgentUserId,
                        principalTable: "Agents",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Licensings_SchoolInfos_SchoolSmsId",
                        column: x => x.SchoolSmsId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OPtionalFeesCategories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OPtionalFeesCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OPtionalFeesCategories_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalDetails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HeadsName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    SignatureImageString = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    SchoolInfoId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolHeadCategoryId = table.Column<byte>(type: "smallint", nullable: false),
                    Gender = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrincipalDetails_SchoolHeadCategories_SchoolHeadCategoryId",
                        column: x => x.SchoolHeadCategoryId,
                        principalTable: "SchoolHeadCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrincipalDetails_SchoolInfos_SchoolInfoId",
                        column: x => x.SchoolInfoId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolLevelCategoriesLabels",
                columns: table => new
                {
                    SchoolInfoId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GeneralSujectLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReligionSubjectLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PlayClassLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreNurseryLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NurseryLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PrimaryLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    JssLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SsLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomLevelLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolLevelCategoriesLabels", x => x.SchoolInfoId);
                    table.ForeignKey(
                        name: "FK_SchoolLevelCategoriesLabels_SchoolInfos_SchoolInfoId",
                        column: x => x.SchoolInfoId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmsLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RecipientNumber = table.Column<string>(type: "text", nullable: false),
                    MessageText = table.Column<string>(type: "text", nullable: false),
                    SchoolInfoId = table.Column<string>(type: "character varying(40)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsLogs_SchoolInfos_SchoolInfoId",
                        column: x => x.SchoolInfoId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId");
                });

            migrationBuilder.CreateTable(
                name: "SmsPaymentBalances",
                columns: table => new
                {
                    SchoolSmsId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PricePerSms = table.Column<float>(type: "real", nullable: false),
                    Amount = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsPaymentBalances", x => x.SchoolSmsId);
                    table.ForeignKey(
                        name: "FK_SmsPaymentBalances_SchoolInfos_SchoolSmsId",
                        column: x => x.SchoolSmsId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmsPurchases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolSmsId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AgentUserId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BalB4Purchase = table.Column<float>(type: "real", nullable: false),
                    AmountPurchase = table.Column<float>(type: "real", nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsPurchases_Agents_AgentUserId",
                        column: x => x.AgentUserId,
                        principalTable: "Agents",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SmsPurchases_SchoolInfos_SchoolSmsId",
                        column: x => x.SchoolSmsId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmsUseds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolSmsId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UsedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PricePerSms = table.Column<float>(type: "real", nullable: false),
                    NoOfPages = table.Column<byte>(type: "smallint", nullable: false),
                    NoOfRecipient = table.Column<short>(type: "smallint", nullable: false),
                    BalB4SendindSms = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsUseds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsUseds_SchoolInfos_SchoolSmsId",
                        column: x => x.SchoolSmsId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubjectCategories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectStatus = table.Column<byte>(type: "smallint", nullable: false),
                    SubjectStatusLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectCategories_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Years",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Years", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Years_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyAndTermlyAssessmentStatus",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TermlyAccessmentStatus = table.Column<byte>(type: "smallint", nullable: false),
                    TermStatus = table.Column<byte>(type: "smallint", nullable: false),
                    CasCategory = table.Column<byte>(type: "smallint", nullable: false),
                    PerCasValue = table.Column<byte>(type: "smallint", nullable: true),
                    MinPassMark = table.Column<byte>(type: "smallint", nullable: true),
                    NextTermBeginsDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssessmtEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalAttendance = table.Column<byte>(type: "smallint", nullable: true),
                    SessionId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HeadmasterId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PrincipalId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyAndTermlyAssessmentStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyAndTermlyAssessmentStatus_AcademicSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassArms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SNo = table.Column<byte>(type: "smallint", nullable: false),
                    ClassId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ArmName = table.Column<byte>(type: "smallint", nullable: false),
                    FormMaster = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassArms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassArms_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeIncomeStructures",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BasicSalary = table.Column<double>(type: "double precision", nullable: false),
                    Allowance = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeIncomeStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeIncomeStructures_ClassTeachers_Id",
                        column: x => x.Id,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetEmployeeSalaries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(40)", nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    Salary = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetEmployeeSalaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetEmployeeSalaries_ClassTeachers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WeekId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ItemCategoryId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CapturedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cost = table.Column<double>(type: "double precision", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenditureWeeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "ExpenditureWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpensesCategories_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ExpensesCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incomes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WeekId = table.Column<string>(type: "character varying(40)", nullable: false),
                    ItemCategoryId = table.Column<string>(type: "character varying(40)", nullable: false),
                    CapturedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cost = table.Column<double>(type: "double precision", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incomes_IncomeCategorys_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "IncomeCategorys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Incomes_IncomeWeeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "IncomeWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    SubjectCategoryId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolTyInfoSchoolRegistrationOfficerId = table.Column<string>(type: "character varying(40)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subjects_SchoolInfos_SchoolTyInfoSchoolRegistrationOfficerId",
                        column: x => x.SchoolTyInfoSchoolRegistrationOfficerId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId");
                    table.ForeignKey(
                        name: "FK_Subjects_SubjectCategories_SubjectCategoryId",
                        column: x => x.SubjectCategoryId,
                        principalTable: "SubjectCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Months",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    YearId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Month = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Months", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Months_Years_YearId",
                        column: x => x.YearId,
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OptionalFeesItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ItemCategoryId = table.Column<string>(type: "character varying(40)", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Cost = table.Column<double>(type: "double precision", nullable: false),
                    YearlyAndTermlyStatusId = table.Column<string>(type: "character varying(40)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionalFeesItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptionalFeesItems_OPtionalFeesCategories_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "OPtionalFeesCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OptionalFeesItems_YearlyAndTermlyAssessmentStatus_YearlyAnd~",
                        column: x => x.YearlyAndTermlyStatusId,
                        principalTable: "YearlyAndTermlyAssessmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetFreshierRegistrationFees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchoolId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    YearlyAndTermlyStatusId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FreshPlayClass = table.Column<double>(type: "double precision", nullable: false),
                    FreshPreNursery = table.Column<double>(type: "double precision", nullable: false),
                    FreshNursery = table.Column<double>(type: "double precision", nullable: false),
                    FreshPrimary = table.Column<double>(type: "double precision", nullable: false),
                    FreshJss = table.Column<double>(type: "double precision", nullable: false),
                    FreshScience = table.Column<double>(type: "double precision", nullable: false),
                    FreshArt = table.Column<double>(type: "double precision", nullable: false),
                    FreshCommercial = table.Column<double>(type: "double precision", nullable: false),
                    RetnPlayClass = table.Column<double>(type: "double precision", nullable: false),
                    RetnPreNursery = table.Column<double>(type: "double precision", nullable: false),
                    RetnNursery = table.Column<double>(type: "double precision", nullable: false),
                    RetnPrimary = table.Column<double>(type: "double precision", nullable: false),
                    RetnJss = table.Column<double>(type: "double precision", nullable: false),
                    RetnScience = table.Column<double>(type: "double precision", nullable: false),
                    RetnArt = table.Column<double>(type: "double precision", nullable: false),
                    RetnCommercial = table.Column<double>(type: "double precision", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetFreshierRegistrationFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetFreshierRegistrationFees_SchoolInfos_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetFreshierRegistrationFees_YearlyAndTermlyAssessmentStatus~",
                        column: x => x.YearlyAndTermlyStatusId,
                        principalTable: "YearlyAndTermlyAssessmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentsDetails",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SNo = table.Column<short>(type: "smallint", nullable: false),
                    IdNo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlaceOfBirth = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Religion = table.Column<byte>(type: "smallint", nullable: false),
                    BloodGroup = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateOfAdmsn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateOfDischarge = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransferedFromWhere = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StartClass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CurrentClass = table.Column<byte>(type: "smallint", nullable: false),
                    Passport = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassArmId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GuardiansName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Relationship = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GuardianAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Occupation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HomeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MailingAddress = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TimeStatus = table.Column<byte>(type: "smallint", nullable: false),
                    SchoolInfoId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassCategory = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    StudentInitialRegStatus = table.Column<byte>(type: "smallint", nullable: false),
                    AdmisnTermId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentsDetails", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StudentsDetails_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentsDetails_ClassArms_ClassArmId",
                        column: x => x.ClassArmId,
                        principalTable: "ClassArms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentsDetails_SchoolInfos_SchoolInfoId",
                        column: x => x.SchoolInfoId,
                        principalTable: "SchoolInfos",
                        principalColumn: "SchoolRegistrationOfficerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentsDetails_YearlyAndTermlyAssessmentStatus_AdmisnTermId",
                        column: x => x.AdmisnTermId,
                        principalTable: "YearlyAndTermlyAssessmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherClassArmAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ArmId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ArmTermlyAssessmentStatus = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherClassArmAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherClassArmAssignments_ClassArms_ArmId",
                        column: x => x.ArmId,
                        principalTable: "ClassArms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherClassArmAssignments_ClassTeachers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CbtQuestionsAndAnswers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AssessmentType = table.Column<byte>(type: "smallint", nullable: false),
                    Question = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    AnswerOptions = table.Column<string>(type: "character varying(2010)", maxLength: 2010, nullable: false),
                    Answer = table.Column<byte>(type: "smallint", nullable: false),
                    TermId = table.Column<byte>(type: "smallint", nullable: false),
                    ClassId = table.Column<string>(type: "character varying(40)", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CbtQuestionsAndAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CbtQuestionsAndAnswers_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CbtQuestionsAndAnswers_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CbtQuestionsAndAnswers_Terms_TermId",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherSubjectArmAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ArmId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubjectArmAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectArmAssignments_ClassArms_ArmId",
                        column: x => x.ArmId,
                        principalTable: "ClassArms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectArmAssignments_ClassTeachers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectArmAssignments_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TxEmployeeMonthlyPaymentLevel",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MonthId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeMonthlyPaymentLevel = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxEmployeeMonthlyPaymentLevel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TxEmployeeMonthlyPaymentLevel_Months_MonthId",
                        column: x => x.MonthId,
                        principalTable: "Months",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffectiveAndPsychomotorAssessments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ItemId = table.Column<byte>(type: "smallint", nullable: false),
                    Score = table.Column<byte>(type: "smallint", nullable: false),
                    YearlyAndTermlyStatusId = table.Column<string>(type: "character varying(40)", nullable: false),
                    StudentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassArmId = table.Column<string>(type: "character varying(40)", nullable: false),
                    FormMasterId = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffectiveAndPsychomotorAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffectiveAndPsychomotorAssessments_AffectiveAndPsychomotors~",
                        column: x => x.ItemId,
                        principalTable: "AffectiveAndPsychomotors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffectiveAndPsychomotorAssessments_ClassArms_ClassArmId",
                        column: x => x.ClassArmId,
                        principalTable: "ClassArms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffectiveAndPsychomotorAssessments_ClassTeachers_FormMaster~",
                        column: x => x.FormMasterId,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffectiveAndPsychomotorAssessments_StudentsDetails_StudentId",
                        column: x => x.StudentId,
                        principalTable: "StudentsDetails",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffectiveAndPsychomotorAssessments_YearlyAndTermlyAssessmen~",
                        column: x => x.YearlyAndTermlyStatusId,
                        principalTable: "YearlyAndTermlyAssessmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentAcademicProgressHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    YearlyAndTermlyStatusId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassCategory = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAcademicProgressHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAcademicProgressHistories_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAcademicProgressHistories_StudentsDetails_StudentId",
                        column: x => x.StudentId,
                        principalTable: "StudentsDetails",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAcademicProgressHistories_YearlyAndTermlyAssessmentS~",
                        column: x => x.YearlyAndTermlyStatusId,
                        principalTable: "YearlyAndTermlyAssessmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentAccessments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassArmId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    YearlyAndTermlyStatusId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Ca1 = table.Column<float>(type: "real", nullable: false),
                    Ca2 = table.Column<float>(type: "real", nullable: false),
                    Ca3 = table.Column<float>(type: "real", nullable: false),
                    Exams = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAccessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAccessments_ClassArms_ClassArmId",
                        column: x => x.ClassArmId,
                        principalTable: "ClassArms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAccessments_ClassTeachers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAccessments_StudentsDetails_StudentId",
                        column: x => x.StudentId,
                        principalTable: "StudentsDetails",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAccessments_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAccessments_YearlyAndTermlyAssessmentStatus_YearlyAn~",
                        column: x => x.YearlyAndTermlyStatusId,
                        principalTable: "YearlyAndTermlyAssessmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentOPtionalFeesAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OptionalFeesItemId = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentOPtionalFeesAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentOPtionalFeesAssignments_OptionalFeesItems_OptionalFe~",
                        column: x => x.OptionalFeesItemId,
                        principalTable: "OptionalFeesItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentOPtionalFeesAssignments_StudentsDetails_StudentId",
                        column: x => x.StudentId,
                        principalTable: "StudentsDetails",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentsAttendanceReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Attendance = table.Column<byte>(type: "smallint", nullable: false),
                    TeacherComments = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SchoolHeadComments = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    YearlyAndTermlyStatusId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassArmId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentsAttendanceReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentsAttendanceReports_ClassArms_ClassArmId",
                        column: x => x.ClassArmId,
                        principalTable: "ClassArms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentsAttendanceReports_StudentsDetails_StudentId",
                        column: x => x.StudentId,
                        principalTable: "StudentsDetails",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentsAttendanceReports_YearlyAndTermlyAssessmentStatus_Y~",
                        column: x => x.YearlyAndTermlyStatusId,
                        principalTable: "YearlyAndTermlyAssessmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentTermlyOptionalCharges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassArmId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OptionalFeesItemId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AsignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTermlyOptionalCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTermlyOptionalCharges_ClassArms_ClassArmId",
                        column: x => x.ClassArmId,
                        principalTable: "ClassArms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentTermlyOptionalCharges_OptionalFeesItems_OptionalFees~",
                        column: x => x.OptionalFeesItemId,
                        principalTable: "OptionalFeesItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentTermlyOptionalCharges_StudentsDetails_StudentId",
                        column: x => x.StudentId,
                        principalTable: "StudentsDetails",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubjectStudentAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectStudentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectStudentAssignments_StudentsDetails_StudentId",
                        column: x => x.StudentId,
                        principalTable: "StudentsDetails",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubjectStudentAssignments_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CbtSetQuestions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    QuestionAndAnswerId = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CbtSetQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CbtSetQuestions_CbtQuestionsAndAnswers_QuestionAndAnswerId",
                        column: x => x.QuestionAndAnswerId,
                        principalTable: "CbtQuestionsAndAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeesMonthlyPayments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeMonthlyPaymentLevelId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BasicSalary = table.Column<double>(type: "double precision", nullable: false),
                    Allowance = table.Column<double>(type: "double precision", nullable: false),
                    Bonus = table.Column<double>(type: "double precision", nullable: false),
                    Deduction = table.Column<double>(type: "double precision", nullable: false),
                    EmployeeMonthlyPaymentStatus = table.Column<byte>(type: "smallint", nullable: false),
                    PaymentType = table.Column<byte>(type: "smallint", nullable: false),
                    Comments = table.Column<string>(type: "character varying(170)", maxLength: 170, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeesMonthlyPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeesMonthlyPayments_ClassTeachers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeesMonthlyPayments_TxEmployeeMonthlyPaymentLevel_Empl~",
                        column: x => x.EmployeeMonthlyPaymentLevelId,
                        principalTable: "TxEmployeeMonthlyPaymentLevel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeePaymentDiscounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentAcademicProgressId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DiscountAmount = table.Column<double>(type: "double precision", nullable: false),
                    ReasonsForDiscount = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeePaymentDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeePaymentDiscounts_StudentAcademicProgressHistories_Studen~",
                        column: x => x.StudentAcademicProgressId,
                        principalTable: "StudentAcademicProgressHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentTermlyFeePayments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentAcademicProgressHistoryId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentType = table.Column<byte>(type: "smallint", nullable: false),
                    PaymentId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTermlyFeePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTermlyFeePayments_StudentAcademicProgressHistories_S~",
                        column: x => x.StudentAcademicProgressHistoryId,
                        principalTable: "StudentAcademicProgressHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentOptionalFeePayments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentTermlyOptionalChargesId = table.Column<string>(type: "character varying(40)", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentType = table.Column<byte>(type: "smallint", nullable: false),
                    PaymentId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentOptionalFeePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentOptionalFeePayments_StudentTermlyOptionalCharges_Stu~",
                        column: x => x.StudentTermlyOptionalChargesId,
                        principalTable: "StudentTermlyOptionalCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentTermlyOptionalPaymentDiscounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentTermlyOptionalChargesId = table.Column<string>(type: "character varying(40)", nullable: false),
                    DiscountAmount = table.Column<double>(type: "double precision", nullable: false),
                    ReasonsForDiscount = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTermlyOptionalPaymentDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTermlyOptionalPaymentDiscounts_StudentTermlyOptional~",
                        column: x => x.StudentTermlyOptionalChargesId,
                        principalTable: "StudentTermlyOptionalCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSessions_SchoolInfoId",
                table: "AcademicSessions",
                column: "SchoolInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_Affective_ClassArm_Term",
                table: "AffectiveAndPsychomotorAssessments",
                columns: new[] { "ClassArmId", "YearlyAndTermlyStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Affective_FormMaster_Term",
                table: "AffectiveAndPsychomotorAssessments",
                columns: new[] { "FormMasterId", "YearlyAndTermlyStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_AffectiveAndPsychomotorAssessments_ItemId",
                table: "AffectiveAndPsychomotorAssessments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AffectiveAndPsychomotorAssessments_YearlyAndTermlyStatusId",
                table: "AffectiveAndPsychomotorAssessments",
                column: "YearlyAndTermlyStatusId");

            migrationBuilder.CreateIndex(
                name: "UQ_Affective_Student_Term_Item",
                table: "AffectiveAndPsychomotorAssessments",
                columns: new[] { "StudentId", "YearlyAndTermlyStatusId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoles_Name",
                table: "AspNetRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_UserId",
                table: "AspNetUserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "\"NormalizedUserName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CbtQuestionsAndAnswers_ClassId",
                table: "CbtQuestionsAndAnswers",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_CbtQuestionsAndAnswers_SubjectId",
                table: "CbtQuestionsAndAnswers",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CbtQuestionsAndAnswers_TermId",
                table: "CbtQuestionsAndAnswers",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_CbtSetQuestions_QuestionAndAnswerId",
                table: "CbtSetQuestions",
                column: "QuestionAndAnswerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassArms_ClassId",
                table: "ClassArms",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SchoolInfoId",
                table: "Classes",
                column: "SchoolInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeachers_SchoolId",
                table: "ClassTeachers",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeachers_UserId",
                table: "ClassTeachers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContinuousAssessmentTypes_SchoolId",
                table: "ContinuousAssessmentTypes",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeesMonthlyPayments_EmployeeId",
                table: "EmployeesMonthlyPayments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeesMonthlyPayments_EmployeeMonthlyPaymentLevelId",
                table: "EmployeesMonthlyPayments",
                column: "EmployeeMonthlyPaymentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenditureWeeks_SchoolId",
                table: "ExpenditureWeeks",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ItemCategoryId",
                table: "Expenses",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_WeekId",
                table: "Expenses",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpensesCategories_SchoolId",
                table: "ExpensesCategories",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentDiscounts_StudentAcademicProgressId",
                table: "FeePaymentDiscounts",
                column: "StudentAcademicProgressId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeCategorys_SchoolId",
                table: "IncomeCategorys",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_ItemCategoryId",
                table: "Incomes",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_WeekId",
                table: "Incomes",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeWeeks_SchoolId",
                table: "IncomeWeeks",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Licensings_AgentUserId",
                table: "Licensings",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Licensings_SchoolSmsId",
                table: "Licensings",
                column: "SchoolSmsId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalGovernments_StateId",
                table: "LocalGovernments",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_Months_YearId",
                table: "Months",
                column: "YearId");

            migrationBuilder.CreateIndex(
                name: "IX_OPtionalFeesCategories_SchoolId",
                table: "OPtionalFeesCategories",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_OptionalFeesItems_ItemCategoryId",
                table: "OptionalFeesItems",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OptionalFeesItems_YearlyAndTermlyStatusId",
                table: "OptionalFeesItems",
                column: "YearlyAndTermlyStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalDetails_SchoolHeadCategoryId",
                table: "PrincipalDetails",
                column: "SchoolHeadCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalDetails_SchoolInfoId",
                table: "PrincipalDetails",
                column: "SchoolInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolRegistrationOfficer_AgentUserId",
                table: "SchoolRegistrationOfficer",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SetEmployeeSalaries_EmployeeId",
                table: "SetEmployeeSalaries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SetFreshierRegistrationFees_SchoolId",
                table: "SetFreshierRegistrationFees",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_SetFreshierRegistrationFees_YearlyAndTermlyStatusId",
                table: "SetFreshierRegistrationFees",
                column: "YearlyAndTermlyStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_SchoolInfoId",
                table: "SmsLogs",
                column: "SchoolInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsPurchases_AgentUserId",
                table: "SmsPurchases",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsPurchases_SchoolSmsId",
                table: "SmsPurchases",
                column: "SchoolSmsId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsUseds_SchoolSmsId",
                table: "SmsUseds",
                column: "SchoolSmsId");

            migrationBuilder.CreateIndex(
                name: "IX_States_RegionId",
                table: "States",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicProgressHistories_ClassId",
                table: "StudentAcademicProgressHistories",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicProgressHistories_StudentId",
                table: "StudentAcademicProgressHistories",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicProgressHistories_YearlyAndTermlyStatusId",
                table: "StudentAcademicProgressHistories",
                column: "YearlyAndTermlyStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Assess_ClassArm_Term",
                table: "StudentAccessments",
                columns: new[] { "ClassArmId", "YearlyAndTermlyStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assess_Student_Term",
                table: "StudentAccessments",
                columns: new[] { "StudentId", "YearlyAndTermlyStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assess_Subject_ClassArm_Term",
                table: "StudentAccessments",
                columns: new[] { "SubjectId", "ClassArmId", "YearlyAndTermlyStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assess_Teacher_Term",
                table: "StudentAccessments",
                columns: new[] { "EmployeeId", "YearlyAndTermlyStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAccessments_YearlyAndTermlyStatusId",
                table: "StudentAccessments",
                column: "YearlyAndTermlyStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentOptionalFeePayments_StudentTermlyOptionalChargesId",
                table: "StudentOptionalFeePayments",
                column: "StudentTermlyOptionalChargesId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentOPtionalFeesAssignments_OptionalFeesItemId",
                table: "StudentOPtionalFeesAssignments",
                column: "OptionalFeesItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentOPtionalFeesAssignments_StudentId",
                table: "StudentOPtionalFeesAssignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_ClassArm_Term",
                table: "StudentsAttendanceReports",
                columns: new[] { "ClassArmId", "YearlyAndTermlyStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentsAttendanceReports_YearlyAndTermlyStatusId",
                table: "StudentsAttendanceReports",
                column: "YearlyAndTermlyStatusId");

            migrationBuilder.CreateIndex(
                name: "UQ_Attendance_Student_Term",
                table: "StudentsAttendanceReports",
                columns: new[] { "StudentId", "YearlyAndTermlyStatusId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentsDetails_AdmisnTermId",
                table: "StudentsDetails",
                column: "AdmisnTermId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentsDetails_ClassArmId",
                table: "StudentsDetails",
                column: "ClassArmId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentsDetails_SchoolInfoId",
                table: "StudentsDetails",
                column: "SchoolInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermlyFeePayments_StudentAcademicProgressHistoryId",
                table: "StudentTermlyFeePayments",
                column: "StudentAcademicProgressHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermlyOptionalCharges_ClassArmId",
                table: "StudentTermlyOptionalCharges",
                column: "ClassArmId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermlyOptionalCharges_OptionalFeesItemId",
                table: "StudentTermlyOptionalCharges",
                column: "OptionalFeesItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermlyOptionalCharges_StudentId",
                table: "StudentTermlyOptionalCharges",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermlyOptionalPaymentDiscounts_StudentTermlyOptional~",
                table: "StudentTermlyOptionalPaymentDiscounts",
                column: "StudentTermlyOptionalChargesId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectCategories_SchoolId",
                table: "SubjectCategories",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_SchoolTyInfoSchoolRegistrationOfficerId",
                table: "Subjects",
                column: "SchoolTyInfoSchoolRegistrationOfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_SubjectCategoryId",
                table: "Subjects",
                column: "SubjectCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectStudentAssignments_StudentId",
                table: "SubjectStudentAssignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectStudentAssignments_SubjectId",
                table: "SubjectStudentAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherClassArmAssignments_ArmId",
                table: "TeacherClassArmAssignments",
                column: "ArmId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherClassArmAssignments_EmployeeId",
                table: "TeacherClassArmAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectArmAssignments_ArmId",
                table: "TeacherSubjectArmAssignments",
                column: "ArmId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectArmAssignments_EmployeeId",
                table: "TeacherSubjectArmAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectArmAssignments_SubjectId",
                table: "TeacherSubjectArmAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TxEmployeeMonthlyPaymentLevel_MonthId",
                table: "TxEmployeeMonthlyPaymentLevel",
                column: "MonthId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyAndTermlyAssessmentStatus_SessionId",
                table: "YearlyAndTermlyAssessmentStatus",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Years_SchoolId",
                table: "Years",
                column: "SchoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffectiveAndPsychomotorAssessments");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CbtSetQuestions");

            migrationBuilder.DropTable(
                name: "ContinuousAssessmentTypes");

            migrationBuilder.DropTable(
                name: "EmployeeIncomeStructures");

            migrationBuilder.DropTable(
                name: "EmployeesMonthlyPayments");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "FeePaymentDiscounts");

            migrationBuilder.DropTable(
                name: "Incomes");

            migrationBuilder.DropTable(
                name: "Licensings");

            migrationBuilder.DropTable(
                name: "LocalGovernments");

            migrationBuilder.DropTable(
                name: "PrincipalDetails");

            migrationBuilder.DropTable(
                name: "ProgramInfos");

            migrationBuilder.DropTable(
                name: "SchoolLevelCategoriesLabels");

            migrationBuilder.DropTable(
                name: "SetEmployeeSalaries");

            migrationBuilder.DropTable(
                name: "SetFreshierRegistrationFees");

            migrationBuilder.DropTable(
                name: "SmsLogs");

            migrationBuilder.DropTable(
                name: "SmsPaymentBalances");

            migrationBuilder.DropTable(
                name: "SmsPurchases");

            migrationBuilder.DropTable(
                name: "SmsUseds");

            migrationBuilder.DropTable(
                name: "StudentAccessments");

            migrationBuilder.DropTable(
                name: "StudentOptionalFeePayments");

            migrationBuilder.DropTable(
                name: "StudentOPtionalFeesAssignments");

            migrationBuilder.DropTable(
                name: "StudentsAttendanceReports");

            migrationBuilder.DropTable(
                name: "StudentTermlyFeePayments");

            migrationBuilder.DropTable(
                name: "StudentTermlyOptionalPaymentDiscounts");

            migrationBuilder.DropTable(
                name: "SubjectStudentAssignments");

            migrationBuilder.DropTable(
                name: "TeacherClassArmAssignments");

            migrationBuilder.DropTable(
                name: "TeacherSubjectArmAssignments");

            migrationBuilder.DropTable(
                name: "AffectiveAndPsychomotors");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "CbtQuestionsAndAnswers");

            migrationBuilder.DropTable(
                name: "TxEmployeeMonthlyPaymentLevel");

            migrationBuilder.DropTable(
                name: "ExpenditureWeeks");

            migrationBuilder.DropTable(
                name: "ExpensesCategories");

            migrationBuilder.DropTable(
                name: "IncomeCategorys");

            migrationBuilder.DropTable(
                name: "IncomeWeeks");

            migrationBuilder.DropTable(
                name: "States");

            migrationBuilder.DropTable(
                name: "SchoolHeadCategories");

            migrationBuilder.DropTable(
                name: "StudentAcademicProgressHistories");

            migrationBuilder.DropTable(
                name: "StudentTermlyOptionalCharges");

            migrationBuilder.DropTable(
                name: "ClassTeachers");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropTable(
                name: "Months");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropTable(
                name: "OptionalFeesItems");

            migrationBuilder.DropTable(
                name: "StudentsDetails");

            migrationBuilder.DropTable(
                name: "SubjectCategories");

            migrationBuilder.DropTable(
                name: "Years");

            migrationBuilder.DropTable(
                name: "OPtionalFeesCategories");

            migrationBuilder.DropTable(
                name: "ClassArms");

            migrationBuilder.DropTable(
                name: "YearlyAndTermlyAssessmentStatus");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "AcademicSessions");

            migrationBuilder.DropTable(
                name: "SchoolInfos");

            migrationBuilder.DropTable(
                name: "SchoolRegistrationOfficer");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
