import pg from 'pg';

const src = new pg.Client({ connectionString: 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSmsOnlineDbJune2026' });
const dst = new pg.Client({ connectionString: 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSMSApr2026' });

await src.connect();
await dst.connect();

const s = await src.query('SELECT * FROM txacademicsessions ORDER BY id LIMIT 10');
console.log('SOURCE row keys:', Object.keys(s.rows[0] ?? {}));
console.log('SOURCE rows:');
console.table(
  s.rows.map((r) => ({
    id: r.id,
    Session: r.Session,
    session_lower: r.session,
    status: r.status,
    schoolinfoid: r.schoolinfoid,
  })),
);

const d = await dst.query(`
  SELECT "Id", "Session", "Status", "SchoolInfoId"
  FROM "AcademicSessions"
  ORDER BY "Status"
  LIMIT 10
`);
console.log('DEST rows:');
console.table(d.rows);

await src.end();
await dst.end();
