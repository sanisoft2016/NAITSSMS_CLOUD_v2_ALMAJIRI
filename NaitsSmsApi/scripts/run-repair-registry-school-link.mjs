import pg from 'pg';

const loginUserId = process.argv[2] ?? '47a11732-355f-410c-b47a-efdfab9d6982';
const oldScopeId = process.argv[3] ?? 'ed28afbf-2f27-4dab-8441-b3dbc03de5c5';

const c = new pg.Client('postgresql://postgres:123456%40Abc@localhost:5432/NaitsSMSApr2026');
await c.connect();

const repairSql = `
DO $$
DECLARE
    login_user_id text := '${loginUserId}';
    old_scope_id  text := '${oldScopeId}';
    agent_user_id text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "Id" = login_user_id) THEN
        RAISE EXCEPTION 'login_user_id % is not in AspNetUsers', login_user_id;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = old_scope_id) THEN
        RAISE EXCEPTION 'old_scope_id % is not a SchoolInfos.SchoolRegistrationOfficerId', old_scope_id;
    END IF;

    IF login_user_id = old_scope_id THEN
        RAISE NOTICE 'School already uses login user id as scope id. Nothing to do.';
        RETURN;
    END IF;

    SELECT COALESCE((SELECT a."UserId" FROM "Agents" a LIMIT 1), '') INTO agent_user_id;

    INSERT INTO "SchoolRegistrationOfficer" (
        "UserId", "FullAddress", "SignatureImageString", "AgentUserId", "TimeStatus", "AccessStatus"
    )
    SELECT
        login_user_id,
        COALESCE(NULLIF(s."SchoolAddress", ''), 'N/A'),
        '',
        agent_user_id,
        0,
        0
    FROM "SchoolInfos" s
    WHERE s."SchoolRegistrationOfficerId" = old_scope_id
      AND NOT EXISTS (
          SELECT 1 FROM "SchoolRegistrationOfficer" o WHERE o."UserId" = login_user_id
      );

    INSERT INTO "SchoolInfos" (
        "SchoolRegistrationOfficerId",
        "AccountingStartTermId",
        "ElectiveAccessStatus",
        "EmailAddress",
        "FeatureImplementationType",
        "Motto",
        "MyStatus",
        "NetworkImplementationType",
        "PoAddress",
        "SchoolAddress",
        "SchoolAdminAccountantStatus",
        "SchoolLogoString",
        "SchoolName",
        "SmsApiKey",
        "SmsApiToken",
        "SsNo",
        "SystemImplementationType",
        "Telephone",
        "TotalStudents",
        "Website"
    )
    SELECT
        login_user_id,
        "AccountingStartTermId",
        "ElectiveAccessStatus",
        "EmailAddress",
        "FeatureImplementationType",
        "Motto",
        "MyStatus",
        "NetworkImplementationType",
        "PoAddress",
        "SchoolAddress",
        "SchoolAdminAccountantStatus",
        "SchoolLogoString",
        "SchoolName",
        "SmsApiKey",
        "SmsApiToken",
        "SsNo",
        "SystemImplementationType",
        "Telephone",
        "TotalStudents",
        "Website"
    FROM "SchoolInfos"
    WHERE "SchoolRegistrationOfficerId" = old_scope_id
      AND NOT EXISTS (
          SELECT 1 FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = login_user_id
      );

    UPDATE "AcademicSessions"           SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
    UPDATE "Classes"                    SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
    UPDATE "ClassTeachers"              SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "ContinuousAssessmentTypes"  SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "ExpenditureWeeks"           SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "ExpensesCategories"         SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "IncomeCategorys"            SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "IncomeWeeks"                SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "Licensings"                 SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
    UPDATE "OPtionalFeesCategories"     SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "PrincipalDetails"           SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
    UPDATE "SetFreshierRegistrationFees" SET "SchoolId"    = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "SmsLogs"                    SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
    UPDATE "SmsPaymentBalances"         SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
    UPDATE "SmsPurchases"               SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
    UPDATE "SmsUseds"                   SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
    UPDATE "StudentsDetails"            SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
    UPDATE "SubjectCategories"          SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "Subjects"                   SET "SchoolTyInfoSchoolRegistrationOfficerId" = login_user_id
                                        WHERE "SchoolTyInfoSchoolRegistrationOfficerId" = old_scope_id;
    UPDATE "Years"                      SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
    UPDATE "SchoolLevelCategoriesLabels" SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;

    DELETE FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = old_scope_id;

    RAISE NOTICE 'Repaired school scope: % -> %', old_scope_id, login_user_id;
END $$;
`;

try {
  await c.query('BEGIN');
  await c.query(repairSql);
  await c.query('COMMIT');
  console.log(`Repaired school scope ${oldScopeId} -> ${loginUserId}`);

  const verify = await c.query(`
    SELECT s."SchoolRegistrationOfficerId", o."UserId", u."UserName", s."SchoolName"
    FROM "SchoolInfos" s
    JOIN "SchoolRegistrationOfficer" o ON o."UserId" = s."SchoolRegistrationOfficerId"
    JOIN "AspNetUsers" u ON u."Id" = o."UserId"
    WHERE o."UserId" = $1
  `, [loginUserId]);
  console.log(JSON.stringify(verify.rows, null, 2));
} catch (err) {
  await c.query('ROLLBACK');
  console.error(err);
  process.exitCode = 1;
} finally {
  await c.end();
}
