import pg from 'pg';

const updates = [
  ['44690e20-e343-4e7a-8cf1-61f52aae582d', '2019/2020'],
  ['465093a3-d97a-4f45-82bd-957df95e2a17', '2020/2021'],
  ['59624fb6-ffa2-48fd-8615-ac519ce7b941', '2021/2022'],
  ['8fb03b8c-ad31-42c5-8693-29ec48d1b9c4', '2022/2023'],
  ['9ebfdb21-0bec-4b1a-8442-b8d36218cf57', '2023/2024'],
  ['ed70b55d-0da2-4e85-88db-a3bff7dbe700', '2024/2025'],
  ['f15fdf72-577d-4522-80fa-2694c213c7cc', '2025/2026'],
];

const client = new pg.Client({
  connectionString: 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSMSApr2026',
});

await client.connect();
try {
  for (const [id, session] of updates) {
    const result = await client.query(
      'UPDATE "AcademicSessions" SET "Session" = $1 WHERE "Id" = $2',
      [session, id],
    );
    console.log(`${session}: rowCount ${result.rowCount}`);
  }

  const { rows } = await client.query(
    'SELECT "Id", "Session", "Status" FROM "AcademicSessions" ORDER BY "Session"',
  );
  console.table(rows);
} finally {
  await client.end();
}
