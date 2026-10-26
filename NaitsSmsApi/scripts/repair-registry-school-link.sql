-- Repair registry user <-> school link for SMS and registry endpoints.
--
-- Run against NaitsSMSApr2026 in pgAdmin (or: node scripts/run-repair-registry-school-link.mjs).
--
-- WHY SECTION 4 FAILS
-- Legacy imports stored SchoolRegistrationOfficer.Id in SchoolInfos.SchoolRegistrationOfficerId,
-- but SchoolRegistrationOfficer.UserId must be an AspNetUsers.Id. When the school scope id is an
-- orphan legacy officer id (not in AspNetUsers), INSERT into SchoolRegistrationOfficer fails with:
--   FK_SchoolRegistrationOfficer_AspNetUsers_UserId
--
-- DO NOT run the old blind section 4. Use the diagnostics below, then OPTION A.

-- =============================================================================
-- 1) Schools
-- =============================================================================
SELECT
    s."SchoolRegistrationOfficerId",
    s."SchoolName",
    s."EmailAddress",
    CASE
        WHEN u."Id" IS NULL THEN 'ORPHAN (not an AspNetUsers.Id)'
        ELSE 'valid AspNetUsers.Id'
    END AS scope_status,
    CASE
        WHEN o."UserId" IS NULL THEN 'missing SchoolRegistrationOfficer row'
        ELSE 'officer row exists'
    END AS officer_status
FROM "SchoolInfos" s
LEFT JOIN "AspNetUsers" u ON u."Id" = s."SchoolRegistrationOfficerId"
LEFT JOIN "SchoolRegistrationOfficer" o ON o."UserId" = s."SchoolRegistrationOfficerId"
ORDER BY s."SchoolName";

-- =============================================================================
-- 2) Registration officers
-- =============================================================================
SELECT o."UserId", u."UserName", u."Email"
FROM "SchoolRegistrationOfficer" o
LEFT JOIN "AspNetUsers" u ON u."Id" = o."UserId"
ORDER BY u."UserName";

-- =============================================================================
-- 3) Registry-role login accounts (use Id from here as login_user_id in OPTION A)
-- =============================================================================
SELECT u."Id", u."UserName", u."Email"
FROM "AspNetUsers" u
JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."Name" = 'Registry'
ORDER BY u."UserName";

-- =============================================================================
-- 3b) Orphan school scope ids (legacy officer.Id stored as school PK)
-- =============================================================================
SELECT
    s."SchoolRegistrationOfficerId" AS orphan_scope_id,
    s."SchoolName"
FROM "SchoolInfos" s
WHERE NOT EXISTS (
    SELECT 1 FROM "AspNetUsers" u WHERE u."Id" = s."SchoolRegistrationOfficerId"
)
ORDER BY s."SchoolName";

-- =============================================================================
-- 4) Safe backfill: only when school scope id IS a real AspNetUsers.Id
-- =============================================================================
INSERT INTO "SchoolRegistrationOfficer" (
    "UserId",
    "FullAddress",
    "SignatureImageString",
    "AgentUserId",
    "TimeStatus",
    "AccessStatus"
)
SELECT
    s."SchoolRegistrationOfficerId",
    COALESCE(NULLIF(s."SchoolAddress", ''), 'N/A'),
    '',
    COALESCE((SELECT a."UserId" FROM "Agents" a LIMIT 1), ''),
    0,
    0
FROM "SchoolInfos" s
JOIN "AspNetUsers" u ON u."Id" = s."SchoolRegistrationOfficerId"
WHERE NOT EXISTS (
    SELECT 1
    FROM "SchoolRegistrationOfficer" o
    WHERE o."UserId" = s."SchoolRegistrationOfficerId"
);

-- =============================================================================
-- 5) OPTION A (recommended): remap orphan school scope -> Registry login user
--
-- EDIT login_user_id and old_scope_id from steps 1 and 3 before running.
-- For NaitsSMSApr2026 / HILLS INTERNATIONAL SCHOOL (confirmed):
--   login_user_id = 47a11732-355f-410c-b47a-efdfab9d6982  (biochemist)
--   old_scope_id  = ed28afbf-2f27-4dab-8441-b3dbc03de5c5  (legacy officer.Id)
-- =============================================================================
-- BEGIN;
--
-- DO $$
-- DECLARE
--     login_user_id text := '47a11732-355f-410c-b47a-efdfab9d6982';
--     old_scope_id  text := 'ed28afbf-2f27-4dab-8441-b3dbc03de5c5';
--     agent_user_id text;
-- BEGIN
--     IF NOT EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "Id" = login_user_id) THEN
--         RAISE EXCEPTION 'login_user_id % is not in AspNetUsers', login_user_id;
--     END IF;
--
--     IF NOT EXISTS (SELECT 1 FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = old_scope_id) THEN
--         RAISE EXCEPTION 'old_scope_id % is not a SchoolInfos.SchoolRegistrationOfficerId', old_scope_id;
--     END IF;
--
--     IF login_user_id = old_scope_id THEN
--         RAISE NOTICE 'School already uses login user id as scope id. Nothing to do.';
--         RETURN;
--     END IF;
--
--     SELECT COALESCE((SELECT a."UserId" FROM "Agents" a LIMIT 1), '') INTO agent_user_id;
--
--     INSERT INTO "SchoolRegistrationOfficer" (
--         "UserId", "FullAddress", "SignatureImageString", "AgentUserId", "TimeStatus", "AccessStatus"
--     )
--     SELECT
--         login_user_id,
--         COALESCE(NULLIF(s."SchoolAddress", ''), 'N/A'),
--         '',
--         agent_user_id,
--         0,
--         0
--     FROM "SchoolInfos" s
--     WHERE s."SchoolRegistrationOfficerId" = old_scope_id
--       AND NOT EXISTS (
--           SELECT 1 FROM "SchoolRegistrationOfficer" o WHERE o."UserId" = login_user_id
--       );
--
--     INSERT INTO "SchoolInfos" (
--         "SchoolRegistrationOfficerId",
--         "AccountingStartTermId",
--         "ElectiveAccessStatus",
--         "EmailAddress",
--         "FeatureImplementationType",
--         "Motto",
--         "MyStatus",
--         "NetworkImplementationType",
--         "PoAddress",
--         "SchoolAddress",
--         "SchoolAdminAccountantStatus",
--         "SchoolLogoString",
--         "SchoolName",
--         "SmsApiKey",
--         "SmsApiToken",
--         "SsNo",
--         "SystemImplementationType",
--         "Telephone",
--         "TotalStudents",
--         "Website"
--     )
--     SELECT
--         login_user_id,
--         "AccountingStartTermId",
--         "ElectiveAccessStatus",
--         "EmailAddress",
--         "FeatureImplementationType",
--         "Motto",
--         "MyStatus",
--         "NetworkImplementationType",
--         "PoAddress",
--         "SchoolAddress",
--         "SchoolAdminAccountantStatus",
--         "SchoolLogoString",
--         "SchoolName",
--         "SmsApiKey",
--         "SmsApiToken",
--         "SsNo",
--         "SystemImplementationType",
--         "Telephone",
--         "TotalStudents",
--         "Website"
--     FROM "SchoolInfos"
--     WHERE "SchoolRegistrationOfficerId" = old_scope_id
--       AND NOT EXISTS (
--           SELECT 1 FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = login_user_id
--       );
--
--     UPDATE "AcademicSessions"           SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
--     UPDATE "Classes"                    SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
--     UPDATE "ClassTeachers"              SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "ContinuousAssessmentTypes"  SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "ExpenditureWeeks"           SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "ExpensesCategories"         SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "IncomeCategorys"            SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "IncomeWeeks"                SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "Licensings"                 SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
--     UPDATE "OPtionalFeesCategories"     SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "PrincipalDetails"           SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
--     UPDATE "SetFreshierRegistrationFees" SET "SchoolId"    = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "SmsLogs"                    SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
--     UPDATE "SmsPaymentBalances"         SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
--     UPDATE "SmsPurchases"               SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
--     UPDATE "SmsUseds"                   SET "SchoolSmsId"  = login_user_id WHERE "SchoolSmsId"  = old_scope_id;
--     UPDATE "StudentsDetails"            SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
--     UPDATE "SubjectCategories"          SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "Subjects"                   SET "SchoolTyInfoSchoolRegistrationOfficerId" = login_user_id
--                                         WHERE "SchoolTyInfoSchoolRegistrationOfficerId" = old_scope_id;
--     UPDATE "Years"                      SET "SchoolId"     = login_user_id WHERE "SchoolId"     = old_scope_id;
--     UPDATE "SchoolLevelCategoriesLabels" SET "SchoolInfoId" = login_user_id WHERE "SchoolInfoId" = old_scope_id;
--
--     DELETE FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = old_scope_id;
--
--     RAISE NOTICE 'Repaired school scope: % -> %', old_scope_id, login_user_id;
-- END $$;
--
-- COMMIT;

-- =============================================================================
-- 6) OPTION B: log in with the account whose Id already equals the school scope id
-- =============================================================================
-- Only works when step 1 scope_status = 'valid AspNetUsers.Id'. Not applicable for orphan legacy ids.

-- =============================================================================
-- 7) Verify after fix (replace login_user_id)
-- =============================================================================
-- SELECT s."SchoolRegistrationOfficerId", o."UserId", u."UserName", s."SchoolName"
-- FROM "SchoolInfos" s
-- JOIN "SchoolRegistrationOfficer" o ON o."UserId" = s."SchoolRegistrationOfficerId"
-- JOIN "AspNetUsers" u ON u."Id" = o."UserId"
-- WHERE o."UserId" = '47a11732-355f-410c-b47a-efdfab9d6982';
