import pg from 'pg';

const conn = 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSMSApr2026';
const client = new pg.Client(conn);

async function q(sql, params = []) {
  const res = await client.query(sql, params);
  return res.rows;
}

function log(title, rows) {
  console.log(`\n=== ${title} ===`);
  console.log(JSON.stringify(rows, null, 2));
}

try {
  await client.connect();
  console.log('Connected to NaitsSMSApr2026');

  const registryUsers = await q(`
    SELECT u."Id", u."UserName", u."Email"
    FROM "AspNetUsers" u
    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
    WHERE r."Name" = 'Registry'
    ORDER BY u."UserName"
  `);
  log('Registry users', registryUsers);

  const schools = await q(`
    SELECT
      s."SchoolRegistrationOfficerId" AS school_scope_id,
      s."SchoolName",
      CASE
        WHEN u."Id" IS NULL THEN 'ORPHAN (not AspNetUsers.Id)'
        ELSE 'valid AspNetUsers.Id'
      END AS scope_status,
      CASE
        WHEN o."UserId" IS NULL THEN 'missing officer row'
        ELSE 'officer row exists'
      END AS officer_status
    FROM "SchoolInfos" s
    LEFT JOIN "AspNetUsers" u ON u."Id" = s."SchoolRegistrationOfficerId"
    LEFT JOIN "SchoolRegistrationOfficer" o ON o."UserId" = s."SchoolRegistrationOfficerId"
    ORDER BY s."SchoolName"
  `);
  log('Schools', schools);

  const licences = await q(`
    SELECT
      l."Id",
      l."SchoolSmsId",
      l."TimeStatus",
      l."ExpiryDate",
      l."PaymentDate",
      s."SchoolName",
      s."SchoolRegistrationOfficerId" AS school_scope_id,
      CASE
        WHEN l."SchoolSmsId" = s."SchoolRegistrationOfficerId" THEN 'matches school scope'
        ELSE 'MISMATCH'
      END AS link_status
    FROM "Licensings" l
    LEFT JOIN "SchoolInfos" s ON s."SchoolRegistrationOfficerId" = l."SchoolSmsId"
    ORDER BY l."ExpiryDate" DESC
  `);
  log('All licences', licences);

  const mismatches = await q(`
    SELECT
      l."Id" AS licence_id,
      l."SchoolSmsId" AS licence_school_sms_id,
      l."TimeStatus",
      l."ExpiryDate",
      s."SchoolRegistrationOfficerId" AS school_scope_id,
      s."SchoolName",
      ru."Id" AS registry_login_id,
      ru."UserName" AS registry_login
    FROM "Licensings" l
    CROSS JOIN LATERAL (
      SELECT u."Id", u."UserName"
      FROM "AspNetUsers" u
      JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
      JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
      WHERE r."Name" = 'Registry'
      ORDER BY u."UserName"
      LIMIT 1
    ) ru
    LEFT JOIN "SchoolInfos" s ON TRUE
    WHERE l."SchoolSmsId" <> s."SchoolRegistrationOfficerId"
      AND (
        l."SchoolSmsId" = ru."Id"
        OR s."SchoolRegistrationOfficerId" = ru."Id"
        OR EXISTS (
          SELECT 1 FROM "SchoolInfos" s2
          WHERE s2."SchoolRegistrationOfficerId" = l."SchoolSmsId"
        )
      )
    ORDER BY l."ExpiryDate" DESC
  `);

  // Simpler mismatch detection: licence SchoolSmsId not equal to any school scope but school exists with different id
  const licenceSchoolGaps = await q(`
    SELECT
      l."Id" AS licence_id,
      l."SchoolSmsId",
      l."TimeStatus",
      l."ExpiryDate",
      s."SchoolRegistrationOfficerId",
      s."SchoolName"
    FROM "Licensings" l
    LEFT JOIN "SchoolInfos" s ON s."SchoolRegistrationOfficerId" = l."SchoolSmsId"
    WHERE s."SchoolRegistrationOfficerId" IS NULL
    ORDER BY l."ExpiryDate" DESC
  `);
  log('Licences with no direct school scope match', licenceSchoolGaps);

  const orphanSchools = schools.filter(s => s.scope_status?.includes('ORPHAN'));
  log('Orphan schools', orphanSchools);

  await client.query('BEGIN');

  let fixes = [];

  // Fix 1: remap Licensings.SchoolSmsId from orphan scope -> registry login when school was already remapped
  for (const reg of registryUsers) {
    const loginId = reg.Id;
    for (const orphan of orphanSchools) {
      const oldScopeId = orphan.school_scope_id;
      if (!oldScopeId || oldScopeId === loginId) continue;

      const schoolAtLogin = await q(
        `SELECT 1 FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = $1`,
        [loginId]
      );
      if (schoolAtLogin.length === 0) continue;

      const licenceAtOld = await q(
        `SELECT "Id" FROM "Licensings" WHERE "SchoolSmsId" = $1`,
        [oldScopeId]
      );
      if (licenceAtOld.length === 0) continue;

      const r = await client.query(
        `UPDATE "Licensings" SET "SchoolSmsId" = $1 WHERE "SchoolSmsId" = $2`,
        [loginId, oldScopeId]
      );
      fixes.push(`Licensings ${oldScopeId} -> ${loginId} (${r.rowCount} rows)`);
    }
  }

  // Fix 2: licence on login id but school still on orphan -> run full scope repair for first registry+orphan pair
  if (registryUsers.length === 1 && orphanSchools.length === 1) {
    const loginUserId = registryUsers[0].Id;
    const oldScopeId = orphanSchools[0].school_scope_id;
    const schoolAtLogin = await q(
      `SELECT 1 FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = $1`,
      [loginUserId]
    );
    if (schoolAtLogin.length === 0) {
      console.log(`\nRunning full school scope repair: ${oldScopeId} -> ${loginUserId}`);
      const agent = await q(`SELECT COALESCE((SELECT a."UserId" FROM "Agents" a LIMIT 1), '') AS id`);
      const agentUserId = agent[0]?.id ?? '';

      await client.query(`
        INSERT INTO "SchoolRegistrationOfficer" (
          "UserId", "FullAddress", "SignatureImageString", "AgentUserId", "TimeStatus", "AccessStatus"
        )
        SELECT $1, COALESCE(NULLIF(s."SchoolAddress", ''), 'N/A'), '', $2, 0, 0
        FROM "SchoolInfos" s
        WHERE s."SchoolRegistrationOfficerId" = $3
          AND NOT EXISTS (SELECT 1 FROM "SchoolRegistrationOfficer" o WHERE o."UserId" = $1)
      `, [loginUserId, agentUserId, oldScopeId]);

      await client.query(`
        INSERT INTO "SchoolInfos" (
          "SchoolRegistrationOfficerId", "AccountingStartTermId", "ElectiveAccessStatus", "EmailAddress",
          "FeatureImplementationType", "Motto", "MyStatus", "NetworkImplementationType", "PoAddress",
          "SchoolAddress", "SchoolAdminAccountantStatus", "SchoolLogoString", "SchoolName", "SmsApiKey",
          "SmsApiToken", "SsNo", "SystemImplementationType", "Telephone", "TotalStudents", "Website"
        )
        SELECT $1, "AccountingStartTermId", "ElectiveAccessStatus", "EmailAddress", "FeatureImplementationType",
          "Motto", "MyStatus", "NetworkImplementationType", "PoAddress", "SchoolAddress",
          "SchoolAdminAccountantStatus", "SchoolLogoString", "SchoolName", "SmsApiKey", "SmsApiToken", "SsNo",
          "SystemImplementationType", "Telephone", "TotalStudents", "Website"
        FROM "SchoolInfos"
        WHERE "SchoolRegistrationOfficerId" = $2
          AND NOT EXISTS (SELECT 1 FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = $1)
      `, [loginUserId, oldScopeId]);

      const tables = [
        ['"AcademicSessions"', '"SchoolInfoId"'],
        ['"Classes"', '"SchoolInfoId"'],
        ['"ClassTeachers"', '"SchoolId"'],
        ['"ContinuousAssessmentTypes"', '"SchoolId"'],
        ['"ExpenditureWeeks"', '"SchoolId"'],
        ['"ExpensesCategories"', '"SchoolId"'],
        ['"IncomeCategorys"', '"SchoolId"'],
        ['"IncomeWeeks"', '"SchoolId"'],
        ['"Licensings"', '"SchoolSmsId"'],
        ['"OPtionalFeesCategories"', '"SchoolId"'],
        ['"PrincipalDetails"', '"SchoolInfoId"'],
        ['"SetFreshierRegistrationFees"', '"SchoolId"'],
        ['"SmsLogs"', '"SchoolInfoId"'],
        ['"SmsPaymentBalances"', '"SchoolSmsId"'],
        ['"SmsPurchases"', '"SchoolSmsId"'],
        ['"SmsUseds"', '"SchoolSmsId"'],
        ['"StudentsDetails"', '"SchoolInfoId"'],
        ['"SubjectCategories"', '"SchoolId"'],
        ['"Years"', '"SchoolId"'],
        ['"SchoolLevelCategoriesLabels"', '"SchoolInfoId"'],
      ];
      for (const [table, col] of tables) {
        const r = await client.query(
          `UPDATE ${table} SET ${col} = $1 WHERE ${col} = $2`,
          [loginUserId, oldScopeId]
        );
        if (r.rowCount > 0) fixes.push(`${table}.${col} ${oldScopeId} -> ${loginUserId} (${r.rowCount})`);
      }
      await client.query(
        `UPDATE "Subjects" SET "SchoolTyInfoSchoolRegistrationOfficerId" = $1 WHERE "SchoolTyInfoSchoolRegistrationOfficerId" = $2`,
        [loginUserId, oldScopeId]
      );
      await client.query(`DELETE FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = $1`, [oldScopeId]);
      fixes.push(`Deleted orphan SchoolInfos ${oldScopeId}`);
    }
  }

  // Fix 3: licence exists for school scope but TimeStatus is Previous only — promote latest to Current
  const schoolsNeedingCurrent = await q(`
    SELECT s."SchoolRegistrationOfficerId", s."SchoolName"
    FROM "SchoolInfos" s
    WHERE NOT EXISTS (
      SELECT 1 FROM "Licensings" l
      WHERE l."SchoolSmsId" = s."SchoolRegistrationOfficerId" AND l."TimeStatus" = 0
    )
    AND EXISTS (
      SELECT 1 FROM "Licensings" l WHERE l."SchoolSmsId" = s."SchoolRegistrationOfficerId"
    )
  `);

  for (const school of schoolsNeedingCurrent) {
    const scopeId = school.SchoolRegistrationOfficerId;
    const latest = await q(`
      SELECT "Id" FROM "Licensings"
      WHERE "SchoolSmsId" = $1
      ORDER BY "ExpiryDate" DESC
      LIMIT 1
    `, [scopeId]);
    if (latest.length === 0) continue;

    await client.query(
      `UPDATE "Licensings" SET "TimeStatus" = 1 WHERE "SchoolSmsId" = $1 AND "TimeStatus" = 0 AND "Id" <> $2`,
      [scopeId, latest[0].Id]
    );
    const r = await client.query(
      `UPDATE "Licensings" SET "TimeStatus" = 0 WHERE "Id" = $1`,
      [latest[0].Id]
    );
    if (r.rowCount > 0) {
      fixes.push(`Set licence ${latest[0].Id} to Current for school ${school.SchoolName}`);
    }
  }

  // Fix 4: licence on orphan id, school on login id (partial repair) — point licence to login
  for (const reg of registryUsers) {
    const loginId = reg.Id;
    const school = await q(`SELECT "SchoolRegistrationOfficerId", "SchoolName" FROM "SchoolInfos" WHERE "SchoolRegistrationOfficerId" = $1`, [loginId]);
    if (school.length === 0) continue;

    for (const gap of licenceSchoolGaps) {
      const orphanLicenceId = gap.licence_id;
      const orphanSmsId = gap.SchoolSmsId;
      const r = await client.query(
        `UPDATE "Licensings" SET "SchoolSmsId" = $1 WHERE "Id" = $2 AND "SchoolSmsId" = $3`,
        [loginId, orphanLicenceId, orphanSmsId]
      );
      if (r.rowCount > 0) {
        fixes.push(`Licence ${orphanLicenceId}: SchoolSmsId ${orphanSmsId} -> ${loginId}`);
      }
    }
  }

  await client.query('COMMIT');
  console.log('\n=== Fixes applied ===');
  console.log(fixes.length ? fixes.join('\n') : '(none needed)');

  const verify = await q(`
    SELECT
      s."SchoolRegistrationOfficerId",
      s."SchoolName",
      l."Id" AS licence_id,
      l."SchoolSmsId",
      l."TimeStatus",
      l."ExpiryDate",
      CASE WHEN l."SchoolSmsId" = s."SchoolRegistrationOfficerId" AND l."TimeStatus" = 0 THEN 'OK' ELSE 'CHECK' END AS status
    FROM "SchoolInfos" s
    LEFT JOIN "Licensings" l ON l."SchoolSmsId" = s."SchoolRegistrationOfficerId" AND l."TimeStatus" = 0
    ORDER BY s."SchoolName"
  `);
  log('Post-fix verification', verify);

  const registryVerify = await q(`
    SELECT s."SchoolRegistrationOfficerId", o."UserId", u."UserName", s."SchoolName",
      (SELECT COUNT(*) FROM "Licensings" l WHERE l."SchoolSmsId" = s."SchoolRegistrationOfficerId" AND l."TimeStatus" = 0) AS current_licences
    FROM "SchoolInfos" s
    JOIN "SchoolRegistrationOfficer" o ON o."UserId" = s."SchoolRegistrationOfficerId"
    JOIN "AspNetUsers" u ON u."Id" = o."UserId"
    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" AND r."Name" = 'Registry'
  `);
  log('Registry school + current licence count', registryVerify);
} catch (err) {
  try { await client.query('ROLLBACK'); } catch {}
  console.error(err);
  process.exitCode = 1;
} finally {
  await client.end();
}
