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
const srcTables = await pool.request().query(`
  SELECT TABLE_NAME AS name
  FROM INFORMATION_SCHEMA.TABLES
  WHERE TABLE_TYPE = 'BASE TABLE'
  ORDER BY TABLE_NAME`);
console.log('MSSQL tables:', srcTables.recordset.length);
console.log(srcTables.recordset.map((r) => r.name).join(', '));

const counts = await pool.request().query(`
  SELECT t.name AS name, SUM(p.rows) AS rows
  FROM sys.tables t
  JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
  GROUP BY t.name
  ORDER BY t.name`);
for (const r of counts.recordset) {
  const n = Number(r.rows);
  if (n > 0) console.log(`SRC ${r.name}: ${n}`);
}
await sql.close();

const client = new pg.Client({ connectionString: DEST });
await client.connect();
const dst = await client.query(`
  SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY 1`);
console.log('PG tables:', dst.rows.length);
console.log(dst.rows.map((r) => r.tablename).join(', '));

const dstCounts = await client.query(`
  SELECT c.relname AS name, c.reltuples::bigint AS approx
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
  WHERE n.nspname = 'public' AND c.relkind = 'r'
  ORDER BY 1`);
for (const r of dstCounts.rows) {
  console.log(`DST ${r.name}: ~${r.approx}`);
}
await client.end();
