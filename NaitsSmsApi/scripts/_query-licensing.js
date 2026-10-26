const { Client } = require('pg');
const conn = 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSMSApr2026';
(async () => {
  const c = new Client({ connectionString: conn });
  await c.connect();
  const tables = await c.query(`SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND (table_name ILIKE '%licen%' OR table_name ILIKE '%subscr%' OR table_name ILIKE '%payment%' OR table_name ILIKE '%purchase%' OR table_name ILIKE '%agent%') ORDER BY table_name`);
  console.log('=== License-related tables ===');
  console.log(tables.rows.map(r=>r.table_name).join('\n'));
  const count = await c.query('SELECT COUNT(*)::int AS cnt FROM "Licensings"');
  console.log('\n=== Licensings COUNT ===', count.rows[0]);
  for (const q of [
    'SELECT * FROM "SmsPaymentBalances" LIMIT 5',
    'SELECT * FROM "SmsPurchases" LIMIT 5',
    'SELECT * FROM "Agents" LIMIT 3'
  ]) {
    console.log('\n===', q, '===');
    try { console.log(JSON.stringify((await c.query(q)).rows, null, 2)); } catch(e) { console.log('ERR', e.message); }
  }
  const cols = await c.query(`SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name='Licensings' ORDER BY ordinal_position`);
  console.log('\n=== Licensings columns ===');
  console.log(cols.rows);
  const school = await c.query(`SELECT "Id", "SchoolName" FROM "SchoolInfos" WHERE "Id" = '47a11732-355f-410c-b47a-efdfab9d6982' OR "SchoolRegistrationOfficerId" = '47a11732-355f-410c-b47a-efdfab9d6982' LIMIT 3`);
  console.log('\n=== School lookup ===', JSON.stringify(school.rows, null, 2));
  await c.end();
})().catch(e=>{console.error(e); process.exit(1);});
