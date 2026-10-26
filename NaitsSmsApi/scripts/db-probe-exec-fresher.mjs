import pg from 'pg';

const c = new pg.Client({
  connectionString: 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb',
});
await c.connect();

const terms = [
  ['First', '21df07e4-f4c6-4237-8f08-fe57034105b1'],
  ['Second', '3ec233a4-8a57-4855-85df-3e7c4782e569'],
  ['Third', 'f7d40d5e-6f9c-473c-81d9-6995d2349af4'],
];

for (const [name, id] of terms) {
  const r = await c.query(
    `
    SELECT
      COUNT(*) FILTER (
        WHERE d."AdmisnTermId" = $1 AND d."StudentInitialRegStatus" = 0
      )::int AS fresher_rows,
      COUNT(*) FILTER (
        WHERE y."Id" IS NOT NULL
      )::int AS admisn_valid_fk
    FROM "StudentAcademicProgressHistories" p
    JOIN "StudentsDetails" d ON d."UserId" = p."StudentId"
    LEFT JOIN "YearlyAndTermlyAssessmentStatus" y ON y."Id" = d."AdmisnTermId"
    WHERE p."YearlyAndTermlyStatusId" = $1
    `,
    [id],
  );
  console.log(name, r.rows[0]);
}

const fkOk = await c.query(`
  SELECT
    COUNT(*)::int AS total,
    COUNT(*) FILTER (WHERE y."Id" IS NOT NULL)::int AS valid_admisn_fk
  FROM "StudentsDetails" d
  LEFT JOIN "YearlyAndTermlyAssessmentStatus" y ON y."Id" = d."AdmisnTermId"
`);
console.log('all students AdmisnTerm FK:', fkOk.rows[0]);

await c.end();
