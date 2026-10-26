import pg from 'pg';

const c = new pg.Client({
  connectionString: 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb',
});
await c.connect();
try {
  const r = await c.query(`SELECT current_database(), current_user, current_schema()`);
  console.log(r.rows[0]);
  const t = await c.query(
    `SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY 1 LIMIT 15`,
  );
  console.log('pg_tables', t.rows.map((x) => x.tablename));
  for (const name of ['AspNetUsers', 'aspnetusers', 'StudentsDetails', 'studentsdetails']) {
    try {
      const e = await c.query(`SELECT COUNT(*)::bigint AS c FROM "${name}"`);
      console.log(`OK "${name}"`, e.rows[0].c);
    } catch (err) {
      console.log(`FAIL "${name}"`, err.message);
    }
  }
  const cols = await c.query(`
    SELECT table_name, column_name, data_type
    FROM information_schema.columns
    WHERE table_schema='public' AND table_name ILIKE 'aspnetusers'
    ORDER BY ordinal_position
  `);
  console.log('AspNetUsers cols sample', cols.rows.slice(0, 8));
} catch (err) {
  console.error('ERR', err.message);
}
await c.end();
