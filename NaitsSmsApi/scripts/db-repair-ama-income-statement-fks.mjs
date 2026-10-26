/**
 * Repair AMA import gaps that zero-out income statement (non-salary lines):
 * 1) Finance week/category SchoolId left as legacy "1" instead of school-scope GUID
 * 2) StudentTermlyFeePayments.StudentAcademicProgressHistoryId left as legacy ints
 *    while progress histories were remapped via stableGuid
 *
 * Usage: node db-repair-ama-income-statement-fks.mjs
 */
import { createHash } from 'crypto';
import pg from 'pg';

const DEST = 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb';
const SCHOOL_SCOPE_ID = 'c514a732-5d8b-4af0-a585-416b222d0c71';

function stableGuid(namespace, raw) {
  const h = createHash('sha256').update(`${namespace}:${raw}`).digest('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(12, 15)}-8${h.slice(15, 18)}-${h.slice(18, 30)}`;
}

const c = new pg.Client({ connectionString: DEST });
await c.connect();
await c.query('BEGIN');

try {
  const schoolTables = [
    'ExpenditureWeeks',
    'IncomeWeeks',
    'ExpensesCategories',
    'IncomeCategorys',
    'OPtionalFeesCategories',
  ];

  for (const table of schoolTables) {
    const r = await c.query(
      `UPDATE "${table}" SET "SchoolId" = $1 WHERE "SchoolId" = '1'`,
      [SCHOOL_SCOPE_ID],
    );
    console.log(`SchoolId fix ${table}: ${r.rowCount}`);
  }

  const fees = await c.query(
    `SELECT "Id", "StudentAcademicProgressHistoryId" FROM "StudentTermlyFeePayments"`,
  );
  let feeUpdated = 0;
  for (const row of fees.rows) {
    const legacy = String(row.StudentAcademicProgressHistoryId ?? '');
    // Already a GUID? skip
    if (legacy.includes('-') && legacy.length >= 32) continue;
    const mapped = stableGuid('studentacademictxprogresshistories', legacy);
    const u = await c.query(
      `UPDATE "StudentTermlyFeePayments"
       SET "StudentAcademicProgressHistoryId" = $1
       WHERE "Id" = $2`,
      [mapped, row.Id],
    );
    feeUpdated += u.rowCount;
  }
  console.log(`Fee history FK remapped: ${feeUpdated}`);

  // Remap fee payment Ids to stable GUIDs if discounts / others need them later
  // (keep string int Ids for now unless discounts reference them)

  const discounts = await c.query(`
    SELECT column_name FROM information_schema.columns
    WHERE table_name = 'FeePaymentDiscounts'
    ORDER BY ordinal_position
  `);
  console.log(
    'FeePaymentDiscounts cols',
    discounts.rows.map((r) => r.column_name),
  );

  // Verify Jan–Sep 2026 totals match legacy screenshot
  const expenses = await c.query(`
    SELECT COALESCE(SUM(e."Cost"),0) AS cost
    FROM "Expenses" e
    JOIN "ExpenditureWeeks" w ON w."Id" = e."WeekId"
    WHERE w."SchoolId" = $1
      AND date_part('year', e."CapturedDate") = 2026
      AND date_part('month', e."CapturedDate") BETWEEN 1 AND 9
  `, [SCHOOL_SCOPE_ID]);

  const incomes = await c.query(`
    SELECT COALESCE(SUM(i."Cost"),0) AS cost
    FROM "Incomes" i
    JOIN "IncomeWeeks" w ON w."Id" = i."WeekId"
    WHERE w."SchoolId" = $1
      AND date_part('year', i."CapturedDate") = 2026
      AND date_part('month', i."CapturedDate") BETWEEN 1 AND 9
  `, [SCHOOL_SCOPE_ID]);

  const feesSum = await c.query(`
    SELECT COALESCE(SUM(p."Amount"),0) AS amt
    FROM "StudentTermlyFeePayments" p
    JOIN "StudentAcademicProgressHistories" h ON h."Id" = p."StudentAcademicProgressHistoryId"
    JOIN "StudentsDetails" sd ON sd."UserId" = h."StudentId"
    WHERE sd."SchoolInfoId" = $1
      AND date_part('year', p."CreatedDate") = 2026
      AND date_part('month', p."CreatedDate") BETWEEN 1 AND 9
  `, [SCHOOL_SCOPE_ID]);

  console.log('Post-repair Jan–Sep 2026 (expect ~18054015 / 4995000 / 9866250):', {
    expenses: Number(expenses.rows[0].cost),
    incomes: Number(incomes.rows[0].cost),
    fees: Number(feesSum.rows[0].amt),
  });

  await c.query('COMMIT');
  console.log('COMMIT ok');
} catch (e) {
  await c.query('ROLLBACK');
  console.error('ROLLED BACK', e);
  process.exitCode = 1;
} finally {
  await c.end();
}
