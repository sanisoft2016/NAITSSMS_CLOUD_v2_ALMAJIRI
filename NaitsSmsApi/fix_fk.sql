CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AffectiveAndPsychomotors" (
        "Id" smallint NOT NULL,
        "ItemsName" character varying(25) NOT NULL,
        CONSTRAINT "PK_AffectiveAndPsychomotors" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AspNetRoles" (
        "Id" character varying(40) NOT NULL,
        "Name" character varying(20),
        "NormalizedName" character varying(20),
        "ConcurrencyStamp" text,
        CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AspNetUsers" (
        "Id" character varying(40) NOT NULL,
        "UserName" character varying(50) NOT NULL,
        "Email" character varying(50) NOT NULL,
        "PhoneNumber" character varying(25),
        "PasswordHash" character varying(150) NOT NULL,
        "SecurityStamp" character varying(40) NOT NULL,
        "FirstName" character varying(20) NOT NULL,
        "MiddleName" character varying(20) NOT NULL,
        "SurName" character varying(20) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "Gender" smallint NOT NULL,
        "AccessStatus" smallint NOT NULL,
        "UserStatus" smallint NOT NULL,
        "NormalizedUserName" character varying(50),
        "NormalizedEmail" character varying(50),
        "EmailConfirmed" boolean NOT NULL,
        "ConcurrencyStamp" text,
        "PhoneNumberConfirmed" boolean NOT NULL,
        "TwoFactorEnabled" boolean NOT NULL,
        "LockoutEnd" timestamp with time zone,
        "LockoutEnabled" boolean NOT NULL,
        "AccessFailedCount" integer NOT NULL,
        CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "ProgramInfos" (
        "Id" smallint NOT NULL,
        "Machines" character varying(150) NOT NULL,
        "Matches" character varying(70) NOT NULL,
        CONSTRAINT "PK_ProgramInfos" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Regions" (
        "Id" smallint NOT NULL,
        "Name" character varying(50) NOT NULL,
        CONSTRAINT "PK_Regions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SchoolHeadCategories" (
        "Id" smallint NOT NULL,
        "HeadStatus" smallint NOT NULL,
        CONSTRAINT "PK_SchoolHeadCategories" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SchoolInfos" (
        "SchoolRegistrationOfficerId" character varying(40) NOT NULL,
        "SsNo" smallint NOT NULL,
        "SchoolName" character varying(70) NOT NULL,
        "SchoolAddress" character varying(150) NOT NULL,
        "Telephone" character varying(50) NOT NULL,
        "PoAddress" character varying(100) NOT NULL,
        "EmailAddress" character varying(50) NOT NULL,
        "Website" character varying(70) NOT NULL,
        "SchoolLogoString" character varying(40) NOT NULL,
        "Motto" character varying(70) NOT NULL,
        "SchoolAdminAccountantStatus" smallint NOT NULL,
        "AccountingStartTermId" character varying(40),
        "ElectiveAccessStatus" smallint NOT NULL,
        "SystemImplementationType" character varying(162) NOT NULL,
        "MyStatus" smallint NOT NULL,
        "SmsApiKey" character varying(150) NOT NULL,
        "SmsApiToken" character varying(70) NOT NULL,
        "TotalStudents" character varying(170) NOT NULL,
        "FeatureImplementationType" character varying(162) NOT NULL,
        "NetworkImplementationType" character varying(162) NOT NULL,
        CONSTRAINT "PK_SchoolInfos" PRIMARY KEY ("SchoolRegistrationOfficerId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Terms" (
        "Id" smallint NOT NULL,
        "TermStatus" smallint NOT NULL,
        CONSTRAINT "PK_Terms" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AspNetRoleClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "RoleId" character varying(40) NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Agents" (
        "Id" character varying(40) NOT NULL,
        "UserId" character varying(40) NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "FullAddress" character varying(100),
        "SchoolRegId" character varying(40) NOT NULL,
        CONSTRAINT "PK_Agents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Agents_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AspNetUserClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "UserId" character varying(40) NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AspNetUserLogins" (
        "LoginProvider" text NOT NULL,
        "ProviderKey" text NOT NULL,
        "ProviderDisplayName" text,
        "UserId" character varying(40) NOT NULL,
        CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
        CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AspNetUserRoles" (
        "UserId" character varying(40) NOT NULL,
        "RoleId" character varying(40) NOT NULL,
        CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AspNetUserTokens" (
        "UserId" character varying(40) NOT NULL,
        "LoginProvider" text NOT NULL,
        "Name" text NOT NULL,
        "Value" text,
        CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
        CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "States" (
        "Id" smallint NOT NULL,
        "Name" character varying(50) NOT NULL,
        "RegionId" smallint NOT NULL,
        CONSTRAINT "PK_States" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_States_Regions_RegionId" FOREIGN KEY ("RegionId") REFERENCES "Regions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AcademicSessions" (
        "Id" character varying(40) NOT NULL,
        "Session" character varying(9) NOT NULL,
        "Status" smallint NOT NULL,
        "SchoolInfoId" character varying(40) NOT NULL,
        CONSTRAINT "PK_AcademicSessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AcademicSessions_SchoolInfos_SchoolInfoId" FOREIGN KEY ("SchoolInfoId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Classes" (
        "Id" character varying(40) NOT NULL,
        "SNo" smallint NOT NULL,
        "ClassName" smallint NOT NULL,
        "ClassLabel" character varying(40) NOT NULL,
        "SchoolInfoId" character varying(40) NOT NULL,
        CONSTRAINT "PK_Classes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Classes_SchoolInfos_SchoolInfoId" FOREIGN KEY ("SchoolInfoId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "ClassTeachers" (
        "Id" character varying(40) NOT NULL,
        "FullAddress" character varying(100) NOT NULL,
        "LastLogin" timestamp with time zone,
        "DateOfBirth" timestamp with time zone,
        "UserId" character varying(50) NOT NULL,
        "SignatureImageString" character varying(50) NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "EmployeeStatus" smallint NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        "EmployeeIncomeStructureStatus" smallint NOT NULL,
        "EmployeeIdNo" character varying(20) NOT NULL,
        "Decipline" character varying(30) NOT NULL,
        "EmployeeQualificationStatus" smallint NOT NULL,
        CONSTRAINT "PK_ClassTeachers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ClassTeachers_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ClassTeachers_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "ContinuousAssessmentTypes" (
        "Id" character varying(40) NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        "AssessmentType" smallint NOT NULL,
        CONSTRAINT "PK_ContinuousAssessmentTypes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ContinuousAssessmentTypes_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "ExpenditureWeeks" (
        "Id" text NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "WeekOfMonth" smallint NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        CONSTRAINT "PK_ExpenditureWeeks" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExpenditureWeeks_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "ExpensesCategories" (
        "Id" character varying(40) NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        "CategoryName" character varying(50) NOT NULL,
        CONSTRAINT "PK_ExpensesCategories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExpensesCategories_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "IncomeCategorys" (
        "Id" character varying(40) NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        "CategoryName" character varying(50) NOT NULL,
        CONSTRAINT "PK_IncomeCategorys" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_IncomeCategorys_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "IncomeWeeks" (
        "Id" character varying(40) NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "WeekOfMonth" smallint NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        CONSTRAINT "PK_IncomeWeeks" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_IncomeWeeks_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "OPtionalFeesCategories" (
        "Id" character varying(40) NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        "CategoryName" character varying(50) NOT NULL,
        CONSTRAINT "PK_OPtionalFeesCategories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OPtionalFeesCategories_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "PrincipalDetails" (
        "Id" character varying(40) NOT NULL,
        "HeadsName" character varying(40) NOT NULL,
        "PhoneNumber" character varying(15) NOT NULL,
        "SignatureImageString" character varying(40) NOT NULL,
        "Status" smallint NOT NULL,
        "SchoolInfoId" character varying(40) NOT NULL,
        "SchoolHeadCategoryId" smallint NOT NULL,
        "Gender" smallint NOT NULL,
        CONSTRAINT "PK_PrincipalDetails" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PrincipalDetails_SchoolHeadCategories_SchoolHeadCategoryId" FOREIGN KEY ("SchoolHeadCategoryId") REFERENCES "SchoolHeadCategories" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PrincipalDetails_SchoolInfos_SchoolInfoId" FOREIGN KEY ("SchoolInfoId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SchoolLevelCategoriesLabels" (
        "SchoolInfoId" character varying(40) NOT NULL,
        "GeneralSujectLabel" character varying(30) NOT NULL,
        "ReligionSubjectLabel" character varying(30) NOT NULL,
        "PlayClassLabel" character varying(30) NOT NULL,
        "PreNurseryLabel" character varying(30) NOT NULL,
        "NurseryLabel" character varying(30) NOT NULL,
        "PrimaryLabel" character varying(30) NOT NULL,
        "JssLabel" character varying(30) NOT NULL,
        "SsLabel" character varying(30) NOT NULL,
        "CustomLevelLabel" character varying(30) NOT NULL,
        CONSTRAINT "PK_SchoolLevelCategoriesLabels" PRIMARY KEY ("SchoolInfoId"),
        CONSTRAINT "FK_SchoolLevelCategoriesLabels_SchoolInfos_SchoolInfoId" FOREIGN KEY ("SchoolInfoId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SmsPaymentBalances" (
        "SchoolSmsId" character varying(40) NOT NULL,
        "PricePerSms" real NOT NULL,
        "Amount" real NOT NULL,
        CONSTRAINT "PK_SmsPaymentBalances" PRIMARY KEY ("SchoolSmsId"),
        CONSTRAINT "FK_SmsPaymentBalances_SchoolInfos_SchoolSmsId" FOREIGN KEY ("SchoolSmsId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SmsUseds" (
        "Id" character varying(40) NOT NULL,
        "SchoolSmsId" character varying(40) NOT NULL,
        "UsedDate" timestamp with time zone NOT NULL,
        "PricePerSms" real NOT NULL,
        "NoOfPages" smallint NOT NULL,
        "NoOfRecipient" smallint NOT NULL,
        "BalB4SendindSms" real NOT NULL,
        CONSTRAINT "PK_SmsUseds" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SmsUseds_SchoolInfos_SchoolSmsId" FOREIGN KEY ("SchoolSmsId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SubjectCategories" (
        "Id" text NOT NULL,
        "SubjectStatus" smallint NOT NULL,
        "SubjectStatusLabel" character varying(50) NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        CONSTRAINT "PK_SubjectCategories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SubjectCategories_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Years" (
        "Id" character varying(40) NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        "Year" smallint NOT NULL,
        CONSTRAINT "PK_Years" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Years_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Licensings" (
        "Id" character varying(40) NOT NULL,
        "SchoolSmsId" character varying(40) NOT NULL,
        "SubscriptionId" integer NOT NULL,
        "PaymentDate" timestamp with time zone NOT NULL,
        "ExpiryDate" timestamp with time zone NOT NULL,
        "AgentUserId" character varying(40) NOT NULL,
        "LicenseCategory" smallint NOT NULL,
        "Charges" real NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "RenewalConsumptionStatus" smallint NOT NULL,
        CONSTRAINT "PK_Licensings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Licensings_Agents_AgentUserId" FOREIGN KEY ("AgentUserId") REFERENCES "Agents" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Licensings_SchoolInfos_SchoolSmsId" FOREIGN KEY ("SchoolSmsId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SchoolRegistrationOfficer" (
        "Id" character varying(40) NOT NULL,
        "FullAddress" character varying(100) NOT NULL,
        "LastLogin" timestamp with time zone,
        "DateOfBirth" timestamp with time zone,
        "UserId" character varying(40) NOT NULL,
        "SignatureImageString" character varying(50) NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "AccessStatus" smallint NOT NULL,
        "AgentUserId" character varying(40) NOT NULL,
        CONSTRAINT "PK_SchoolRegistrationOfficer" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SchoolRegistrationOfficer_Agents_AgentUserId" FOREIGN KEY ("AgentUserId") REFERENCES "Agents" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SchoolRegistrationOfficer_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SchoolRegistrationOfficer_SchoolInfos_Id" FOREIGN KEY ("Id") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SmsPurchases" (
        "Id" character varying(40) NOT NULL,
        "SchoolSmsId" character varying(40) NOT NULL,
        "PaymentDate" timestamp with time zone NOT NULL,
        "AgentUserId" character varying(40) NOT NULL,
        "BalB4Purchase" real NOT NULL,
        "AmountPurchase" real NOT NULL,
        "TimeStatus" smallint NOT NULL,
        CONSTRAINT "PK_SmsPurchases" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SmsPurchases_Agents_AgentUserId" FOREIGN KEY ("AgentUserId") REFERENCES "Agents" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SmsPurchases_SchoolInfos_SchoolSmsId" FOREIGN KEY ("SchoolSmsId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "LocalGovernments" (
        "Id" smallint GENERATED BY DEFAULT AS IDENTITY,
        "StateId" smallint NOT NULL,
        "Name" character varying(50) NOT NULL,
        CONSTRAINT "PK_LocalGovernments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LocalGovernments_States_StateId" FOREIGN KEY ("StateId") REFERENCES "States" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "YearlyAndTermlyAssessmentStatus" (
        "Id" character varying(40) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "TermlyAccessmentStatus" smallint NOT NULL,
        "TermStatus" smallint NOT NULL,
        "CasCategory" smallint NOT NULL,
        "PerCasValue" smallint,
        "MinPassMark" smallint,
        "NextTermBeginsDate" timestamp with time zone,
        "AssessmtEndDate" timestamp with time zone,
        "TotalAttendance" smallint,
        "SessionId" character varying(40) NOT NULL,
        "HeadmasterId" character varying(40) NOT NULL,
        "PrincipalId" character varying(40) NOT NULL,
        CONSTRAINT "PK_YearlyAndTermlyAssessmentStatus" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_YearlyAndTermlyAssessmentStatus_AcademicSessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES "AcademicSessions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "ClassArms" (
        "Id" character varying(40) NOT NULL,
        "SNo" smallint NOT NULL,
        "ClassId" character varying(40) NOT NULL,
        "ArmName" smallint NOT NULL,
        "FormMaster" character varying(40) NOT NULL,
        CONSTRAINT "PK_ClassArms" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ClassArms_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "EmployeeIncomeStructures" (
        "Id" character varying(40) NOT NULL,
        "BasicSalary" double precision NOT NULL,
        "Allowance" double precision NOT NULL,
        CONSTRAINT "PK_EmployeeIncomeStructures" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_EmployeeIncomeStructures_ClassTeachers_Id" FOREIGN KEY ("Id") REFERENCES "ClassTeachers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SetEmployeeSalaries" (
        "Id" character varying(40) NOT NULL,
        "EmployeeId" character varying(40) NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "Salary" numeric NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_SetEmployeeSalaries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SetEmployeeSalaries_ClassTeachers_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "ClassTeachers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Expenses" (
        "Id" character varying(40) NOT NULL,
        "WeekId" character varying(40) NOT NULL,
        "ItemCategoryId" character varying(40) NOT NULL,
        "CapturedDate" timestamp with time zone NOT NULL,
        "Details" character varying(150) NOT NULL,
        "Cost" double precision NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_Expenses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Expenses_ExpenditureWeeks_WeekId" FOREIGN KEY ("WeekId") REFERENCES "ExpenditureWeeks" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Expenses_ExpensesCategories_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "ExpensesCategories" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Incomes" (
        "Id" character varying(40) NOT NULL,
        "WeekId" character varying(40) NOT NULL,
        "ItemCategoryId" character varying(40) NOT NULL,
        "CapturedDate" timestamp with time zone NOT NULL,
        "Details" character varying(150) NOT NULL,
        "Cost" double precision NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_Incomes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Incomes_IncomeCategorys_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "IncomeCategorys" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Incomes_IncomeWeeks_WeekId" FOREIGN KEY ("WeekId") REFERENCES "IncomeWeeks" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Subjects" (
        "Id" character varying(40) NOT NULL,
        "SubjectName" character varying(50) NOT NULL,
        "TimeStatus" smallint NOT NULL,
        "SubjectCategoryId" character varying(40) NOT NULL,
        "SchoolTyInfoSchoolRegistrationOfficerId" character varying(40),
        CONSTRAINT "PK_Subjects" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Subjects_SchoolInfos_SchoolTyInfoSchoolRegistrationOfficerId" FOREIGN KEY ("SchoolTyInfoSchoolRegistrationOfficerId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId"),
        CONSTRAINT "FK_Subjects_SubjectCategories_SubjectCategoryId" FOREIGN KEY ("SubjectCategoryId") REFERENCES "SubjectCategories" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "Months" (
        "Id" character varying(40) NOT NULL,
        "YearId" character varying(40) NOT NULL,
        "Month" smallint NOT NULL,
        CONSTRAINT "PK_Months" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Months_Years_YearId" FOREIGN KEY ("YearId") REFERENCES "Years" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "OptionalFeesItems" (
        "Id" character varying(40) NOT NULL,
        "ItemCategoryId" character varying(40) NOT NULL,
        "ItemName" character varying(30) NOT NULL,
        "Cost" double precision NOT NULL,
        "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_OptionalFeesItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OptionalFeesItems_OPtionalFeesCategories_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "OPtionalFeesCategories" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_OptionalFeesItems_YearlyAndTermlyAssessmentStatus_YearlyAnd~" FOREIGN KEY ("YearlyAndTermlyStatusId") REFERENCES "YearlyAndTermlyAssessmentStatus" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SetFreshierRegistrationFees" (
        "Id" character varying(40) NOT NULL,
        "SchoolId" character varying(40) NOT NULL,
        "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
        "FreshPlayClass" double precision NOT NULL,
        "FreshPreNursery" double precision NOT NULL,
        "FreshNursery" double precision NOT NULL,
        "FreshPrimary" double precision NOT NULL,
        "FreshJss" double precision NOT NULL,
        "FreshScience" double precision NOT NULL,
        "FreshArt" double precision NOT NULL,
        "FreshCommercial" double precision NOT NULL,
        "RetnPlayClass" double precision NOT NULL,
        "RetnPreNursery" double precision NOT NULL,
        "RetnNursery" double precision NOT NULL,
        "RetnPrimary" double precision NOT NULL,
        "RetnJss" double precision NOT NULL,
        "RetnScience" double precision NOT NULL,
        "RetnArt" double precision NOT NULL,
        "RetnCommercial" double precision NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_SetFreshierRegistrationFees" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SetFreshierRegistrationFees_SchoolInfos_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE,
        CONSTRAINT "FK_SetFreshierRegistrationFees_YearlyAndTermlyAssessmentStatus~" FOREIGN KEY ("YearlyAndTermlyStatusId") REFERENCES "YearlyAndTermlyAssessmentStatus" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentsDetails" (
        "Id" character varying(40) NOT NULL,
        "SNo" smallint NOT NULL,
        "IdNo" character varying(15) NOT NULL,
        "DateOfBirth" timestamp with time zone NOT NULL,
        "PlaceOfBirth" character varying(50) NOT NULL,
        "Religion" smallint NOT NULL,
        "BloodGroup" character varying(10) NOT NULL,
        "DateOfAdmsn" timestamp with time zone NOT NULL,
        "DateOfDischarge" timestamp with time zone,
        "TransferedFromWhere" character varying(150) NOT NULL,
        "StartClass" character varying(40) NOT NULL,
        "CurrentClass" smallint NOT NULL,
        "Passport" character varying(40) NOT NULL,
        "ClassArmId" character varying(40) NOT NULL,
        "GuardiansName" character varying(50) NOT NULL,
        "Relationship" character varying(50) NOT NULL,
        "GuardianAddress" character varying(200) NOT NULL,
        "Occupation" character varying(50) NOT NULL,
        "MobileNumber" character varying(50) NOT NULL,
        "HomeNumber" character varying(50) NOT NULL,
        "EmailAddress" character varying(100) NOT NULL,
        "MailingAddress" character varying(150) NOT NULL,
        "Country" character varying(50) NOT NULL,
        "UserId" character varying(40) NOT NULL,
        "SchoolInfoId" character varying(40) NOT NULL,
        "ClassCategory" smallint NOT NULL,
        "Status" smallint NOT NULL,
        "StudentInitialRegStatus" smallint NOT NULL,
        "AdmisnTermId" character varying(40) NOT NULL,
        CONSTRAINT "PK_StudentsDetails" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentsDetails_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentsDetails_ClassArms_ClassArmId" FOREIGN KEY ("ClassArmId") REFERENCES "ClassArms" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentsDetails_SchoolInfos_SchoolInfoId" FOREIGN KEY ("SchoolInfoId") REFERENCES "SchoolInfos" ("SchoolRegistrationOfficerId") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentsDetails_YearlyAndTermlyAssessmentStatus_AdmisnTermId" FOREIGN KEY ("AdmisnTermId") REFERENCES "YearlyAndTermlyAssessmentStatus" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "TeacherClassArmAssignments" (
        "Id" character varying(40) NOT NULL,
        "EmployeeId" character varying(40) NOT NULL,
        "ArmId" character varying(40) NOT NULL,
        "ArmTermlyAssessmentStatus" smallint NOT NULL,
        CONSTRAINT "PK_TeacherClassArmAssignments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TeacherClassArmAssignments_ClassArms_ArmId" FOREIGN KEY ("ArmId") REFERENCES "ClassArms" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TeacherClassArmAssignments_ClassTeachers_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "ClassTeachers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "CbtQuestionsAndAnswers" (
        "Id" character varying(40) NOT NULL,
        "AssessmentType" smallint NOT NULL,
        "Question" character varying(600) NOT NULL,
        "AnswerOptions" character varying(2010) NOT NULL,
        "Answer" smallint NOT NULL,
        "TermId" smallint NOT NULL,
        "ClassId" character varying(40) NOT NULL,
        "SubjectId" character varying(40) NOT NULL,
        CONSTRAINT "PK_CbtQuestionsAndAnswers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CbtQuestionsAndAnswers_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CbtQuestionsAndAnswers_Subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES "Subjects" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CbtQuestionsAndAnswers_Terms_TermId" FOREIGN KEY ("TermId") REFERENCES "Terms" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "TeacherSubjectArmAssignments" (
        "Id" character varying(40) NOT NULL,
        "SubjectId" character varying(40) NOT NULL,
        "EmployeeId" character varying(40) NOT NULL,
        "ArmId" character varying(40) NOT NULL,
        CONSTRAINT "PK_TeacherSubjectArmAssignments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TeacherSubjectArmAssignments_ClassArms_ArmId" FOREIGN KEY ("ArmId") REFERENCES "ClassArms" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TeacherSubjectArmAssignments_ClassTeachers_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "ClassTeachers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TeacherSubjectArmAssignments_Subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES "Subjects" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "TxEmployeeMonthlyPaymentLevel" (
        "Id" character varying(40) NOT NULL,
        "MonthId" character varying(40) NOT NULL,
        "EmployeeMonthlyPaymentLevel" smallint NOT NULL,
        CONSTRAINT "PK_TxEmployeeMonthlyPaymentLevel" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TxEmployeeMonthlyPaymentLevel_Months_MonthId" FOREIGN KEY ("MonthId") REFERENCES "Months" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "AffectiveAndPsychomotorAssessments" (
        "Id" character varying(40) NOT NULL,
        "ItemId" smallint NOT NULL,
        "Score" smallint NOT NULL,
        "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
        "StudentId" character varying(40) NOT NULL,
        "ClassArmId" character varying(40) NOT NULL,
        "FormMasterId" character varying(40) NOT NULL,
        CONSTRAINT "PK_AffectiveAndPsychomotorAssessments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AffectiveAndPsychomotorAssessments_AffectiveAndPsychomotors~" FOREIGN KEY ("ItemId") REFERENCES "AffectiveAndPsychomotors" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AffectiveAndPsychomotorAssessments_ClassArms_ClassArmId" FOREIGN KEY ("ClassArmId") REFERENCES "ClassArms" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AffectiveAndPsychomotorAssessments_ClassTeachers_FormMaster~" FOREIGN KEY ("FormMasterId") REFERENCES "ClassTeachers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AffectiveAndPsychomotorAssessments_StudentsDetails_StudentId" FOREIGN KEY ("StudentId") REFERENCES "StudentsDetails" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AffectiveAndPsychomotorAssessments_YearlyAndTermlyAssessmen~" FOREIGN KEY ("YearlyAndTermlyStatusId") REFERENCES "YearlyAndTermlyAssessmentStatus" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentAcademicProgressHistories" (
        "Id" character varying(40) NOT NULL,
        "StudentId" character varying(40) NOT NULL,
        "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
        "ClassId" character varying(40) NOT NULL,
        "ClassCategory" smallint NOT NULL,
        CONSTRAINT "PK_StudentAcademicProgressHistories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentAcademicProgressHistories_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentAcademicProgressHistories_StudentsDetails_StudentId" FOREIGN KEY ("StudentId") REFERENCES "StudentsDetails" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentAcademicProgressHistories_YearlyAndTermlyAssessmentS~" FOREIGN KEY ("YearlyAndTermlyStatusId") REFERENCES "YearlyAndTermlyAssessmentStatus" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentAccessments" (
        "Id" character varying(40) NOT NULL,
        "LastUpdateDate" timestamp with time zone NOT NULL,
        "SubjectId" character varying(40) NOT NULL,
        "StudentId" character varying(40) NOT NULL,
        "EmployeeId" character varying(40) NOT NULL,
        "ClassArmId" character varying(40) NOT NULL,
        "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
        "Ca1" real NOT NULL,
        "Ca2" real NOT NULL,
        "Ca3" real NOT NULL,
        "Exams" real NOT NULL,
        CONSTRAINT "PK_StudentAccessments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentAccessments_ClassArms_ClassArmId" FOREIGN KEY ("ClassArmId") REFERENCES "ClassArms" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentAccessments_ClassTeachers_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "ClassTeachers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentAccessments_StudentsDetails_StudentId" FOREIGN KEY ("StudentId") REFERENCES "StudentsDetails" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentAccessments_Subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES "Subjects" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentAccessments_YearlyAndTermlyAssessmentStatus_YearlyAn~" FOREIGN KEY ("YearlyAndTermlyStatusId") REFERENCES "YearlyAndTermlyAssessmentStatus" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentOPtionalFeesAssignments" (
        "Id" character varying(40) NOT NULL,
        "StudentId" character varying(40) NOT NULL,
        "OptionalFeesItemId" character varying(40) NOT NULL,
        CONSTRAINT "PK_StudentOPtionalFeesAssignments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentOPtionalFeesAssignments_OptionalFeesItems_OptionalFe~" FOREIGN KEY ("OptionalFeesItemId") REFERENCES "OptionalFeesItems" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentOPtionalFeesAssignments_StudentsDetails_StudentId" FOREIGN KEY ("StudentId") REFERENCES "StudentsDetails" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentsAttendanceReports" (
        "Id" character varying(40) NOT NULL,
        "Attendance" smallint NOT NULL,
        "TeacherComments" character varying(150) NOT NULL,
        "SchoolHeadComments" character varying(150) NOT NULL,
        "YearlyAndTermlyStatusId" character varying(40) NOT NULL,
        "StudentId" character varying(40) NOT NULL,
        "ClassArmId" character varying(40) NOT NULL,
        CONSTRAINT "PK_StudentsAttendanceReports" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentsAttendanceReports_ClassArms_ClassArmId" FOREIGN KEY ("ClassArmId") REFERENCES "ClassArms" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentsAttendanceReports_StudentsDetails_StudentId" FOREIGN KEY ("StudentId") REFERENCES "StudentsDetails" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentsAttendanceReports_YearlyAndTermlyAssessmentStatus_Y~" FOREIGN KEY ("YearlyAndTermlyStatusId") REFERENCES "YearlyAndTermlyAssessmentStatus" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentTermlyOptionalCharges" (
        "Id" character varying(40) NOT NULL,
        "StudentId" character varying(40) NOT NULL,
        "ClassArmId" character varying(40) NOT NULL,
        "OptionalFeesItemId" character varying(40) NOT NULL,
        "AsignedDate" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_StudentTermlyOptionalCharges" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentTermlyOptionalCharges_ClassArms_ClassArmId" FOREIGN KEY ("ClassArmId") REFERENCES "ClassArms" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentTermlyOptionalCharges_OptionalFeesItems_OptionalFees~" FOREIGN KEY ("OptionalFeesItemId") REFERENCES "OptionalFeesItems" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudentTermlyOptionalCharges_StudentsDetails_StudentId" FOREIGN KEY ("StudentId") REFERENCES "StudentsDetails" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "SubjectStudentAssignments" (
        "Id" character varying(40) NOT NULL,
        "StudentId" character varying(40) NOT NULL,
        "SubjectId" character varying(40) NOT NULL,
        CONSTRAINT "PK_SubjectStudentAssignments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SubjectStudentAssignments_StudentsDetails_StudentId" FOREIGN KEY ("StudentId") REFERENCES "StudentsDetails" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SubjectStudentAssignments_Subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES "Subjects" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "CbtSetQuestions" (
        "Id" character varying(40) NOT NULL,
        "QuestionAndAnswerId" character varying(40) NOT NULL,
        CONSTRAINT "PK_CbtSetQuestions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CbtSetQuestions_CbtQuestionsAndAnswers_QuestionAndAnswerId" FOREIGN KEY ("QuestionAndAnswerId") REFERENCES "CbtQuestionsAndAnswers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "EmployeesMonthlyPayments" (
        "Id" character varying(40) NOT NULL,
        "EmployeeMonthlyPaymentLevelId" character varying(40) NOT NULL,
        "EmployeeId" character varying(40) NOT NULL,
        "BasicSalary" double precision NOT NULL,
        "Allowance" double precision NOT NULL,
        "Bonus" double precision NOT NULL,
        "Deduction" double precision NOT NULL,
        "EmployeeMonthlyPaymentStatus" smallint NOT NULL,
        "PaymentType" smallint NOT NULL,
        "Comments" character varying(170) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_EmployeesMonthlyPayments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_EmployeesMonthlyPayments_ClassTeachers_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "ClassTeachers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_EmployeesMonthlyPayments_TxEmployeeMonthlyPaymentLevel_Empl~" FOREIGN KEY ("EmployeeMonthlyPaymentLevelId") REFERENCES "TxEmployeeMonthlyPaymentLevel" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "FeePaymentDiscounts" (
        "Id" character varying(40) NOT NULL,
        "StudentAcademicProgressId" character varying(40) NOT NULL,
        "DiscountAmount" double precision NOT NULL,
        "ReasonsForDiscount" character varying(100) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_FeePaymentDiscounts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_FeePaymentDiscounts_StudentAcademicProgressHistories_Studen~" FOREIGN KEY ("StudentAcademicProgressId") REFERENCES "StudentAcademicProgressHistories" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentTermlyFeePayments" (
        "Id" character varying(40) NOT NULL,
        "StudentAcademicProgressHistoryId" character varying(40) NOT NULL,
        "PaymentType" smallint NOT NULL,
        "PaymentId" character varying(10) NOT NULL,
        "PaymentDate" timestamp with time zone NOT NULL,
        "Amount" double precision NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_StudentTermlyFeePayments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentTermlyFeePayments_StudentAcademicProgressHistories_S~" FOREIGN KEY ("StudentAcademicProgressHistoryId") REFERENCES "StudentAcademicProgressHistories" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentOptionalFeePayments" (
        "Id" character varying(40) NOT NULL,
        "StudentTermlyOptionalChargesId" character varying(40) NOT NULL,
        "PaymentDate" timestamp with time zone NOT NULL,
        "PaymentType" smallint NOT NULL,
        "PaymentId" character varying(10) NOT NULL,
        "Amount" double precision NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_StudentOptionalFeePayments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentOptionalFeePayments_StudentTermlyOptionalCharges_Stu~" FOREIGN KEY ("StudentTermlyOptionalChargesId") REFERENCES "StudentTermlyOptionalCharges" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE TABLE "StudentTermlyOptionalPaymentDiscounts" (
        "Id" character varying(40) NOT NULL,
        "StudentTermlyOptionalChargesId" character varying(40) NOT NULL,
        "DiscountAmount" double precision NOT NULL,
        "ReasonsForDiscount" character varying(100) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(70) NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(70) NOT NULL,
        CONSTRAINT "PK_StudentTermlyOptionalPaymentDiscounts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudentTermlyOptionalPaymentDiscounts_StudentTermlyOptional~" FOREIGN KEY ("StudentTermlyOptionalChargesId") REFERENCES "StudentTermlyOptionalCharges" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AcademicSessions_SchoolInfoId" ON "AcademicSessions" ("SchoolInfoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Affective_ClassArm_Term" ON "AffectiveAndPsychomotorAssessments" ("ClassArmId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Affective_FormMaster_Term" ON "AffectiveAndPsychomotorAssessments" ("FormMasterId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AffectiveAndPsychomotorAssessments_ItemId" ON "AffectiveAndPsychomotorAssessments" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AffectiveAndPsychomotorAssessments_YearlyAndTermlyStatusId" ON "AffectiveAndPsychomotorAssessments" ("YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE UNIQUE INDEX "UQ_Affective_Student_Term_Item" ON "AffectiveAndPsychomotorAssessments" ("StudentId", "YearlyAndTermlyStatusId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Agents_UserId" ON "Agents" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE UNIQUE INDEX "IX_AspNetRoles_Name" ON "AspNetRoles" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_AspNetUserRoles_UserId" ON "AspNetUserRoles" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE UNIQUE INDEX "IX_AspNetUsers_UserName" ON "AspNetUsers" ("UserName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_CbtQuestionsAndAnswers_ClassId" ON "CbtQuestionsAndAnswers" ("ClassId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_CbtQuestionsAndAnswers_SubjectId" ON "CbtQuestionsAndAnswers" ("SubjectId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_CbtQuestionsAndAnswers_TermId" ON "CbtQuestionsAndAnswers" ("TermId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_CbtSetQuestions_QuestionAndAnswerId" ON "CbtSetQuestions" ("QuestionAndAnswerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_ClassArms_ClassId" ON "ClassArms" ("ClassId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Classes_SchoolInfoId" ON "Classes" ("SchoolInfoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_ClassTeachers_SchoolId" ON "ClassTeachers" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_ClassTeachers_UserId" ON "ClassTeachers" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_ContinuousAssessmentTypes_SchoolId" ON "ContinuousAssessmentTypes" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_EmployeesMonthlyPayments_EmployeeId" ON "EmployeesMonthlyPayments" ("EmployeeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_EmployeesMonthlyPayments_EmployeeMonthlyPaymentLevelId" ON "EmployeesMonthlyPayments" ("EmployeeMonthlyPaymentLevelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_ExpenditureWeeks_SchoolId" ON "ExpenditureWeeks" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Expenses_ItemCategoryId" ON "Expenses" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Expenses_WeekId" ON "Expenses" ("WeekId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_ExpensesCategories_SchoolId" ON "ExpensesCategories" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_FeePaymentDiscounts_StudentAcademicProgressId" ON "FeePaymentDiscounts" ("StudentAcademicProgressId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_IncomeCategorys_SchoolId" ON "IncomeCategorys" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Incomes_ItemCategoryId" ON "Incomes" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Incomes_WeekId" ON "Incomes" ("WeekId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_IncomeWeeks_SchoolId" ON "IncomeWeeks" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Licensings_AgentUserId" ON "Licensings" ("AgentUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Licensings_SchoolSmsId" ON "Licensings" ("SchoolSmsId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_LocalGovernments_StateId" ON "LocalGovernments" ("StateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Months_YearId" ON "Months" ("YearId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_OPtionalFeesCategories_SchoolId" ON "OPtionalFeesCategories" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_OptionalFeesItems_ItemCategoryId" ON "OptionalFeesItems" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_OptionalFeesItems_YearlyAndTermlyStatusId" ON "OptionalFeesItems" ("YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_PrincipalDetails_SchoolHeadCategoryId" ON "PrincipalDetails" ("SchoolHeadCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_PrincipalDetails_SchoolInfoId" ON "PrincipalDetails" ("SchoolInfoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SchoolRegistrationOfficer_AgentUserId" ON "SchoolRegistrationOfficer" ("AgentUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SchoolRegistrationOfficer_UserId" ON "SchoolRegistrationOfficer" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SetEmployeeSalaries_EmployeeId" ON "SetEmployeeSalaries" ("EmployeeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SetFreshierRegistrationFees_SchoolId" ON "SetFreshierRegistrationFees" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SetFreshierRegistrationFees_YearlyAndTermlyStatusId" ON "SetFreshierRegistrationFees" ("YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SmsPurchases_AgentUserId" ON "SmsPurchases" ("AgentUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SmsPurchases_SchoolSmsId" ON "SmsPurchases" ("SchoolSmsId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SmsUseds_SchoolSmsId" ON "SmsUseds" ("SchoolSmsId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_States_RegionId" ON "States" ("RegionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentAcademicProgressHistories_ClassId" ON "StudentAcademicProgressHistories" ("ClassId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentAcademicProgressHistories_StudentId" ON "StudentAcademicProgressHistories" ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentAcademicProgressHistories_YearlyAndTermlyStatusId" ON "StudentAcademicProgressHistories" ("YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Assess_ClassArm_Term" ON "StudentAccessments" ("ClassArmId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Assess_Student_Term" ON "StudentAccessments" ("StudentId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Assess_Subject_ClassArm_Term" ON "StudentAccessments" ("SubjectId", "ClassArmId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Assess_Teacher_Term" ON "StudentAccessments" ("EmployeeId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentAccessments_YearlyAndTermlyStatusId" ON "StudentAccessments" ("YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentOptionalFeePayments_StudentTermlyOptionalChargesId" ON "StudentOptionalFeePayments" ("StudentTermlyOptionalChargesId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentOPtionalFeesAssignments_OptionalFeesItemId" ON "StudentOPtionalFeesAssignments" ("OptionalFeesItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentOPtionalFeesAssignments_StudentId" ON "StudentOPtionalFeesAssignments" ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Attendance_ClassArm_Term" ON "StudentsAttendanceReports" ("ClassArmId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentsAttendanceReports_YearlyAndTermlyStatusId" ON "StudentsAttendanceReports" ("YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE UNIQUE INDEX "UQ_Attendance_Student_Term" ON "StudentsAttendanceReports" ("StudentId", "YearlyAndTermlyStatusId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentsDetails_AdmisnTermId" ON "StudentsDetails" ("AdmisnTermId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentsDetails_ClassArmId" ON "StudentsDetails" ("ClassArmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentsDetails_SchoolInfoId" ON "StudentsDetails" ("SchoolInfoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentsDetails_UserId" ON "StudentsDetails" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentTermlyFeePayments_StudentAcademicProgressHistoryId" ON "StudentTermlyFeePayments" ("StudentAcademicProgressHistoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentTermlyOptionalCharges_ClassArmId" ON "StudentTermlyOptionalCharges" ("ClassArmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentTermlyOptionalCharges_OptionalFeesItemId" ON "StudentTermlyOptionalCharges" ("OptionalFeesItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentTermlyOptionalCharges_StudentId" ON "StudentTermlyOptionalCharges" ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_StudentTermlyOptionalPaymentDiscounts_StudentTermlyOptional~" ON "StudentTermlyOptionalPaymentDiscounts" ("StudentTermlyOptionalChargesId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SubjectCategories_SchoolId" ON "SubjectCategories" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Subjects_SchoolTyInfoSchoolRegistrationOfficerId" ON "Subjects" ("SchoolTyInfoSchoolRegistrationOfficerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Subjects_SubjectCategoryId" ON "Subjects" ("SubjectCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SubjectStudentAssignments_StudentId" ON "SubjectStudentAssignments" ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_SubjectStudentAssignments_SubjectId" ON "SubjectStudentAssignments" ("SubjectId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_TeacherClassArmAssignments_ArmId" ON "TeacherClassArmAssignments" ("ArmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_TeacherClassArmAssignments_EmployeeId" ON "TeacherClassArmAssignments" ("EmployeeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_TeacherSubjectArmAssignments_ArmId" ON "TeacherSubjectArmAssignments" ("ArmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_TeacherSubjectArmAssignments_EmployeeId" ON "TeacherSubjectArmAssignments" ("EmployeeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_TeacherSubjectArmAssignments_SubjectId" ON "TeacherSubjectArmAssignments" ("SubjectId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_TxEmployeeMonthlyPaymentLevel_MonthId" ON "TxEmployeeMonthlyPaymentLevel" ("MonthId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_YearlyAndTermlyAssessmentStatus_SessionId" ON "YearlyAndTermlyAssessmentStatus" ("SessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    CREATE INDEX "IX_Years_SchoolId" ON "Years" ("SchoolId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019161443_Initial') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251019161443_Initial', '9.0.9');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019234144_Initial2nd') THEN
    DROP INDEX "IX_AspNetUsers_UserName";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019234144_Initial2nd') THEN
    DROP INDEX "UserNameIndex";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019234144_Initial2nd') THEN
    ALTER TABLE "SubjectCategories" ALTER COLUMN "Id" TYPE character varying(40);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019234144_Initial2nd') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "Email" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019234144_Initial2nd') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName") WHERE "NormalizedUserName" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251019234144_Initial2nd') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251019234144_Initial2nd', '9.0.9');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251020002945_Initial3rd') THEN
    ALTER TABLE "YearlyAndTermlyAssessmentStatus" ALTER COLUMN "PrincipalId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251020002945_Initial3rd') THEN
    ALTER TABLE "YearlyAndTermlyAssessmentStatus" ALTER COLUMN "HeadmasterId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251020002945_Initial3rd') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251020002945_Initial3rd', '9.0.9');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314221453_FixSchoolRegistrarSchoolInfoRelation') THEN
    ALTER TABLE "SchoolRegistrationOfficer" DROP CONSTRAINT "FK_SchoolRegistrationOfficer_SchoolInfos_Id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314221453_FixSchoolRegistrarSchoolInfoRelation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260314221453_FixSchoolRegistrarSchoolInfoRelation', '9.0.9');
    END IF;
END $EF$;
COMMIT;

