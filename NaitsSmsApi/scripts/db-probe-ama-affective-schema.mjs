import sql from 'mssql';
import pg from 'pg';

const MSSQL = {
  server: 'SQL6028.site4now.net',
  database: 'db_a9f78f_advanced',
  user: 'db_a9f78f_advanced_admin',
  password: 'g@sm^M19!25An',
  options: { encrypt: true, trustServerCertificate: true },
  connectionTimeout: 60000,
  requestTimeout: 180000,
};

const DEST = 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb';

const pool = await sql.connect(MSSQL);
const srcCols = await pool
  .request()
  .query(
    `SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='AffectiveAndPsychomoTxtorAssessments' ORDER BY ORDINAL_POSITION`,
  );
console.log('SRC cols:', srcCols.recordset.map((r) => `${r.COLUMN_NAME}:${r.DATA_TYPE}`).join(', '));
const sample = await pool.request().query(`SELECT TOP 2 * FROM AffectiveAndPsychomoTxtorAssessments`);
console.log('SRC sample keys', Object.keys(sample.recordset[0] || {}));
console.log('SRC count', (await pool.request().query(`SELECT COUNT_BIG(*) AS c FROM AffectiveAndPsychomoTxtorAssessments`)).recordset[0].c);
await sql.close();

const c = new pg.Client({ connectionString: DEST });
await c.connect();
const dstCols = await c.query(
  `SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name='AffectiveAndPsychomotorAssessments' ORDER BY ordinal_position`,
);
console.log('DST cols:', dstCols.rows.map((r) => `${r.column_name}:${r.data_type}:${r.is_nullable}`).join(', '));
try {
  await c.query(`SET session_replication_role = 'replica'`);
  console.log('session_replication_role=replica OK');
  await c.query(`SET session_replication_role = 'origin'`);
} catch (e) {
  console.log('session_replication_role FAIL:', e.message);
}
await c.end();
