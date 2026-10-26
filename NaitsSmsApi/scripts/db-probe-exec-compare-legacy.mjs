import { createHash } from 'crypto';
import pg from 'pg';
import sql from 'mssql';

const DEST = 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb';
const MSSQL = {
  server: 'SQL6028.site4now.net',
  database: 'db_a9f78f_advanced',
  user: 'db_a9f78f_advanced_admin',
  password: 'g@sm^M19!25An',
  options: { encrypt: true, trustServerCertificate: true },
  requestTimeout: 120000,
  connectionTimeout: 60000,
};

function stableGuid(namespace, raw) {
  const h = createHash('sha256').update(`${namespace}:${raw}`).digest('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(12, 15)}-8${h.slice(15, 18)}-${h.slice(18, 30)}`;
}

const SECOND_PG = '3ec233a4-8a57-4855-85df-3e7c4782e569';

const pgClient = new pg.Client({ connectionString: DEST });
await pgClient.connect();

try {
  // Which legacy ints map to 2025/2026 terms?
  const yearly = await pgClient.query(`
    SELECT y."Id", y."TermStatus", s."Session"
    FROM "YearlyAndTermlyAssessmentStatus" y
    JOIN "AcademicSessions" s ON s."Id"=y."SessionId"
    WHERE s."Session"='2025/2026'
    ORDER BY y."TermStatus"
  `);
  console.log('PG 2025/2026 terms', yearly.rows);
  for (let i = 0; i <= 200; i++) {
    const g = stableGuid('txyearlyandtermlystatus', i);
    const hit = yearly.rows.find((r) => r.Id === g);
    if (hit) console.log(`legacy yearly id ${i} -> TermStatus ${hit.TermStatus}`);
  }

  // Discount coverage for second term
  const disc = await pgClient.query(
    `
    SELECT COUNT(*)::int AS discount_rows,
           COALESCE(SUM(d."DiscountAmount"),0)::float AS discount_sum
    FROM "FeePaymentDiscounts" d
    JOIN "StudentAcademicProgressHistories" p ON p."Id"=d."StudentAcademicProgressId"
    WHERE p."YearlyAndTermlyStatusId"=$1
    `,
    [SECOND_PG],
  );
  console.log('PG second-term discounts via progress FK', disc.rows[0]);

  const discCols = await pgClient.query(`
    SELECT column_name FROM information_schema.columns
    WHERE table_name='FeePaymentDiscounts' ORDER BY ordinal_position
  `);
  console.log('FeePaymentDiscounts cols', discCols.rows.map((r) => r.column_name));

  const discAll = await pgClient.query(`SELECT COUNT(*)::int AS n, COALESCE(SUM("DiscountAmount"),0)::float AS s FROM "FeePaymentDiscounts"`);
  console.log('PG all discounts', discAll.rows[0]);

  // Fresher match breakdown by class category for second term
  const fresh = await pgClient.query(
    `
    SELECT d."ClassCategory", COUNT(*)::int AS n
    FROM "StudentAcademicProgressHistories" p
    JOIN "StudentsDetails" d ON d."UserId"=p."StudentId"
    WHERE p."YearlyAndTermlyStatusId"=$1
      AND d."AdmisnTermId"=$1
      AND d."StudentInitialRegStatus"=0
    GROUP BY 1 ORDER BY 1
    `,
    [SECOND_PG],
  );
  console.log('PG second-term report-fresher by ClassCategory', fresh.rows);

  // Connect legacy SQL Server and compare
  const pool = await sql.connect(MSSQL);
  const legacyTerms = await pool.request().query(`
    SELECT y.Id, y.TermStatus, s.Session
    FROM TxYearlyAndTermlyStatus y
    JOIN TxAcademicSessions s ON s.Id = y.SessionId
    WHERE s.Session = '2025/2026'
    ORDER BY y.TermStatus
  `);
  console.log('MSSQL 2025/2026 terms', legacyTerms.recordset);

  const secondLegacy = legacyTerms.recordset.find((r) => r.TermStatus === 1)?.Id;
  console.log('MSSQL second term id', secondLegacy);

  if (secondLegacy != null) {
    const legacyFresh = await pool.request().query(`
      SELECT COUNT(*) AS n
      FROM StudentAcademicTxProgressHistories p
      JOIN TxStudentDetails d ON d.UserId = p.StudentId
      WHERE p.YearlyAndTermlyStatusId = ${secondLegacy}
        AND d.AdmisnTermId = ${secondLegacy}
        AND d.StudentInitialRegStatus = 0
    `);
    console.log('MSSQL second-term report-fresher count', legacyFresh.recordset[0]);

    const legacyDisc = await pool.request().query(`
      SELECT COUNT(*) AS n, ISNULL(SUM(d.DiscountAmount),0) AS s
      FROM StudentTyTermlyFixedPaymentDiscounts d
      JOIN StudentAcademicTxProgressHistories p ON p.Id = d.StudentAcademicProgressId
      WHERE p.YearlyAndTermlyStatusId = ${secondLegacy}
    `);
    console.log('MSSQL second-term discounts', legacyDisc.recordset[0]);

    const legacyFreshByCat = await pool.request().query(`
      SELECT d.ClassCategory, COUNT(*) AS n
      FROM StudentAcademicTxProgressHistories p
      JOIN TxStudentDetails d ON d.UserId = p.StudentId
      WHERE p.YearlyAndTermlyStatusId = ${secondLegacy}
        AND d.AdmisnTermId = ${secondLegacy}
        AND d.StudentInitialRegStatus = 0
      GROUP BY d.ClassCategory
      ORDER BY d.ClassCategory
    `);
    console.log('MSSQL fresher by ClassCategory', legacyFreshByCat.recordset);

    // Sample AdmisnTermId values for nursery-like students with payments
    const sample = await pool.request().query(`
      SELECT TOP 20 d.IdNo, d.AdmisnTermId, d.StudentInitialRegStatus, d.Status, d.ClassCategory
      FROM StudentAcademicTxProgressHistories p
      JOIN TxStudentDetails d ON d.UserId = p.StudentId
      WHERE p.YearlyAndTermlyStatusId = ${secondLegacy}
        AND d.StudentInitialRegStatus = 0
        AND d.ClassCategory = 2
      ORDER BY d.IdNo
    `);
    console.log('MSSQL Nursery init-fresher sample AdmisnTermId', sample.recordset);
  }

  await pool.close();
} catch (err) {
  console.error(err);
} finally {
  await pgClient.end();
}
