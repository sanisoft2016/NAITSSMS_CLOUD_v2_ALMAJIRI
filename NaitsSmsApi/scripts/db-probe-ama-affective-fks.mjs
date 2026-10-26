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
const sample = await pool.request().query(`
  SELECT TOP 5 a.Id, a.StudentId, a.ClassArmId, a.FormMasterId, a.YearlyAndTermlyStatusId, a.ItemId,
         s.Id AS StudentDetailId, s.UserId
  FROM AffectiveAndPsychomoTxtorAssessments a
  LEFT JOIN TxStudentDetails s ON CAST(s.Id AS nvarchar(50)) = a.StudentId OR s.UserId = a.StudentId
`);
console.log(sample.recordset);
const orphan = await pool.request().query(`
  SELECT COUNT(*) AS c
  FROM AffectiveAndPsychomoTxtorAssessments a
  WHERE NOT EXISTS (
    SELECT 1 FROM TxStudentDetails s
    WHERE CAST(s.Id AS nvarchar(50)) = a.StudentId OR s.UserId = a.StudentId
  )
`);
console.log('orphan student refs', orphan.recordset[0]);
await sql.close();

const c = new pg.Client({ connectionString: DEST });
await c.connect();
const arms = await c.query(`SELECT "Id" FROM "ClassArms" LIMIT 3`);
console.log('mapped ClassArms sample', arms.rows);
await c.end();
