/**
 * Repair StudentsDetails.AdmisnTermId that were mapped with mapInt('terms', ...)
 * instead of mapInt('txyearlyandtermlystatus', ...) during SQL Server import.
 *
 * Usage: node db-repair-ama-admisn-term.mjs [--dry-run]
 */
import { createHash } from 'crypto';
import pg from 'pg';

const DEST = 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb';
const dryRun = process.argv.includes('--dry-run');

function stableGuid(namespace, raw) {
  const key = `${namespace}:${raw}`;
  const h = createHash('sha256').update(key).digest('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(12, 15)}-8${h.slice(15, 18)}-${h.slice(18, 30)}`;
}

const c = new pg.Client({ connectionString: DEST });
await c.connect();

try {
  const yearly = await c.query(`SELECT "Id" FROM "YearlyAndTermlyAssessmentStatus"`);
  const yearlyIds = new Set(yearly.rows.map((r) => r.Id));

  // Brute-force reverse map for plausible legacy int ids.
  const termsGuidToInt = new Map();
  const yearlyGuidByInt = new Map();
  for (let i = 0; i <= 5000; i++) {
    termsGuidToInt.set(stableGuid('terms', i), i);
    yearlyGuidByInt.set(i, stableGuid('txyearlyandtermlystatus', i));
  }

  const distinct = await c.query(`
    SELECT DISTINCT "AdmisnTermId" AS id
    FROM "StudentsDetails"
    WHERE "AdmisnTermId" IS NOT NULL AND "AdmisnTermId" <> ''
  `);

  const remaps = [];
  const unresolved = [];
  for (const { id } of distinct.rows) {
    if (yearlyIds.has(id)) continue; // already correct
    const legacyInt = termsGuidToInt.get(id);
    if (legacyInt == null) {
      unresolved.push(id);
      continue;
    }
    const correct = yearlyGuidByInt.get(legacyInt);
    if (!yearlyIds.has(correct)) {
      unresolved.push(`${id} -> int ${legacyInt} -> missing yearly ${correct}`);
      continue;
    }
    remaps.push({ from: id, to: correct, legacyInt });
  }

  console.log(`distinct AdmisnTermId: ${distinct.rows.length}`);
  console.log(`to remap: ${remaps.length}`);
  console.log(`unresolved: ${unresolved.length}`);
  console.log('sample remaps', remaps.slice(0, 10));
  if (unresolved.length) console.log('unresolved sample', unresolved.slice(0, 10));

  if (!dryRun && remaps.length) {
    await c.query('BEGIN');
    let updated = 0;
    for (const m of remaps) {
      const r = await c.query(
        `UPDATE "StudentsDetails" SET "AdmisnTermId" = $1 WHERE "AdmisnTermId" = $2`,
        [m.to, m.from],
      );
      updated += r.rowCount;
    }
    await c.query('COMMIT');
    console.log(`updated rows: ${updated}`);
  } else if (dryRun) {
    console.log('dry-run only; no updates');
  }

  // Verify second term fresher match after (or projected) remap
  const second = '3ec233a4-8a57-4855-85df-3e7c4782e569';
  const check = await c.query(
    `
    SELECT
      COUNT(*) FILTER (
        WHERE d."AdmisnTermId" = $1 AND d."StudentInitialRegStatus" = 0
      )::int AS report_fresher_match,
      COUNT(*) FILTER (WHERE d."Status" = 0)::int AS status_fresher,
      COUNT(*)::int AS progress_count
    FROM "StudentAcademicProgressHistories" p
    JOIN "StudentsDetails" d ON d."UserId" = p."StudentId"
    WHERE p."YearlyAndTermlyStatusId" = $1
    `,
    [second],
  );
  console.log('second-term check:', check.rows[0]);
} catch (err) {
  try {
    await c.query('ROLLBACK');
  } catch {
    // ignore
  }
  console.error(err);
  process.exitCode = 1;
}

await c.end();
