import pg from 'pg';
import sql from 'mssql';

const DEST = 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb';
const MSSQL = {
  server: 'SQL6028.site4now.net',
  database: 'db_a9f78f_advanced',
  user: 'db_a9f78f_advanced_admin',
  password: 'g@sm^M19!25An',
  options: { encrypt: true, trustServerCertificate: true },
  connectionTimeout: 60000,
};

const tables = {
  XyExpenditureWeeks: 'ExpenditureWeeks',
  XyExpensesCategories: 'ExpensesCategories',
  XyIncomeCategories: 'IncomeCategorys',
  XyIncomeWeeks: 'IncomeWeeks',
  XyOPtionalChargeCategories: 'OPtionalFeesCategories',
  SmsPaymentBalances: 'SmsPaymentBalances',
  SmsPurchases: 'SmsPurchases',
  SmsUseds: 'SmsUseds',
  LicensTyings: 'Licensings',
};

const pool = await sql.connect(MSSQL);
const c = new pg.Client({ connectionString: DEST });
await c.connect();
for (const [src, dst] of Object.entries(tables)) {
  const s = await pool
    .request()
    .query(
      `SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=N'${src}' ORDER BY ORDINAL_POSITION`,
    );
  const d = await c.query(
    `SELECT column_name FROM information_schema.columns WHERE table_name='${dst}' ORDER BY ordinal_position`,
  );
  console.log(`\n${src} -> ${dst}`);
  console.log('  SRC:', s.recordset.map((r) => r.COLUMN_NAME).join(', '));
  console.log('  DST:', d.rows.map((r) => r.column_name).join(', '));
}
await sql.close();
await c.end();
