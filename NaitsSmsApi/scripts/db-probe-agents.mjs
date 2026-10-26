import sql from 'mssql';

const MSSQL = {
  server: 'SQL6028.site4now.net',
  database: 'db_a9f78f_advanced',
  user: 'db_a9f78f_advanced_admin',
  password: 'g@sm^M19!25An',
  options: { encrypt: true, trustServerCertificate: true },
  connectionTimeout: 60000,
};

const pool = await sql.connect(MSSQL);
const rows = await pool.request().query(`SELECT * FROM Agents`);
console.log(JSON.stringify(rows.recordset, null, 2));
await sql.close();
