import { spawn } from 'child_process';
import fs from 'fs';
import path from 'path';
import pg from 'pg';

const PSQL = 'C:\\Program Files\\PostgreSQL\\18\\bin\\psql.exe';
const PG_DUMP = 'C:\\Program Files\\PostgreSQL\\18\\bin\\pg_dump.exe';

const LOCAL = {
  host: 'localhost',
  port: 5432,
  database: 'HillsAugDb',
  user: 'postgres',
  password: 'REDACTED',
};

const REMOTE = {
  host: 'PG6001.site4now.net',
  port: 6432,
  database: 'db_a9f78f_hill2nd',
  user: 'a9f78f_hill2nd',
  password: 'REDACTED',
};

const scriptsDir = path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1'));
const dumpDir = path.join(scriptsDir, '_export-tmp');
const preData = path.join(dumpDir, '01-pre-data.sql');
const dataFile = path.join(dumpDir, '02-data.sql');
const postData = path.join(dumpDir, '03-post-data.sql');

function run(cmd, args, env = {}) {
  return new Promise((resolve, reject) => {
    console.log(`> ${cmd} ${args.join(' ')}`);
    const child = spawn(cmd, args, {
      env: { ...process.env, ...env },
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (d) => {
      stdout += d;
      process.stdout.write(d);
    });
    child.stderr.on('data', (d) => {
      stderr += d;
      process.stderr.write(d);
    });
    child.on('close', (code) => {
      if (code === 0) resolve({ stdout, stderr });
      else reject(new Error(`${cmd} exited ${code}\n${stderr}`));
    });
  });
}

async function main() {
  fs.mkdirSync(dumpDir, { recursive: true });

  const remote = new pg.Client({
    host: REMOTE.host,
    port: REMOTE.port,
    database: REMOTE.database,
    user: REMOTE.user,
    password: REMOTE.password,
    ssl: false,
  });
  await remote.connect();
  const priv = await remote.query('SELECT current_user, rolsuper FROM pg_roles WHERE rolname = current_user');
  console.log('Remote role:', priv.rows[0]);

  console.log('Dropping existing remote objects owned by current user...');
  // Shared hosts often disallow DROP SCHEMA public; drop tables/sequences/types instead.
  await remote.query(`
    DO $$
    DECLARE r RECORD;
    BEGIN
      -- Drop FKs first
      FOR r IN (
        SELECT conname, conrelid::regclass AS tbl
        FROM pg_constraint
        WHERE contype = 'f'
          AND connamespace = 'public'::regnamespace
      ) LOOP
        EXECUTE format('ALTER TABLE %s DROP CONSTRAINT IF EXISTS %I', r.tbl, r.conname);
      END LOOP;

      FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
        EXECUTE format('DROP TABLE IF EXISTS public.%I CASCADE', r.tablename);
      END LOOP;

      FOR r IN (
        SELECT sequence_name FROM information_schema.sequences WHERE sequence_schema = 'public'
      ) LOOP
        EXECUTE format('DROP SEQUENCE IF EXISTS public.%I CASCADE', r.sequence_name);
      END LOOP;

      FOR r IN (
        SELECT t.typname
        FROM pg_type t
        JOIN pg_namespace n ON n.oid = t.typnamespace
        WHERE n.nspname = 'public' AND t.typtype = 'e'
      ) LOOP
        EXECUTE format('DROP TYPE IF EXISTS public.%I CASCADE', r.typname);
      END LOOP;
    END $$;
  `);
  const left = await remote.query(`SELECT COUNT(*)::int AS c FROM pg_tables WHERE schemaname='public'`);
  console.log('Remote tables remaining:', left.rows[0].c);
  await remote.end();

  const localEnv = { PGPASSWORD: LOCAL.password };
  const remoteEnv = { PGPASSWORD: REMOTE.password };

  console.log('Dumping pre-data / data / post-data...');
  await run(
    PG_DUMP,
    [
      '-h', LOCAL.host, '-p', String(LOCAL.port), '-U', LOCAL.user, '-d', LOCAL.database,
      '--no-owner', '--no-privileges', '--section=pre-data', '-f', preData,
    ],
    localEnv,
  );
  await run(
    PG_DUMP,
    [
      '-h', LOCAL.host, '-p', String(LOCAL.port), '-U', LOCAL.user, '-d', LOCAL.database,
      '--no-owner', '--no-privileges', '--section=data', '-f', dataFile,
    ],
    localEnv,
  );
  await run(
    PG_DUMP,
    [
      '-h', LOCAL.host, '-p', String(LOCAL.port), '-U', LOCAL.user, '-d', LOCAL.database,
      '--no-owner', '--no-privileges', '--section=post-data', '-f', postData,
    ],
    localEnv,
  );

  for (const f of [preData, dataFile, postData]) {
    console.log(path.basename(f), (fs.statSync(f).size / 1024 / 1024).toFixed(1), 'MB');
  }

  const common = [
    '-h', REMOTE.host, '-p', String(REMOTE.port), '-U', REMOTE.user, '-d', REMOTE.database,
    '-v', 'ON_ERROR_STOP=1',
  ];

  console.log('Restoring pre-data...');
  await run(PSQL, [...common, '-f', preData], remoteEnv);

  console.log('Restoring data...');
  await run(PSQL, [...common, '-f', dataFile], remoteEnv);

  // Strip the known-bad AdmisnTermId FK (maps admission term GUIDs to YearlyAndTermlyAssessmentStatus).
  let postSql = fs.readFileSync(postData, 'utf8');
  postSql = postSql.replace(
    /ALTER TABLE ONLY public\."StudentsDetails"\s+ADD CONSTRAINT "FK_StudentsDetails_YearlyAndTermlyAssessmentStatus_AdmisnTermId"[\s\S]*?;\s*/g,
    '-- skipped bad FK_StudentsDetails_YearlyAndTermlyAssessmentStatus_AdmisnTermId\n',
  );
  fs.writeFileSync(postData, postSql);

  console.log('Restoring post-data (constraints/indexes)...');
  await run(PSQL, [...common, '-f', postData], remoteEnv);

  const verify = new pg.Client({
    host: REMOTE.host,
    port: REMOTE.port,
    database: REMOTE.database,
    user: REMOTE.user,
    password: REMOTE.password,
  });
  await verify.connect();
  await verify.query(`SET search_path TO public`);
  const tables = await verify.query(`SELECT COUNT(*)::int AS c FROM pg_tables WHERE schemaname='public'`);
  const users = await verify.query(`SELECT COUNT(*)::int AS c FROM public."AspNetUsers"`);
  const assessments = await verify.query(`SELECT COUNT(*)::int AS c FROM public."StudentAccessments"`);
  const students = await verify.query(`SELECT COUNT(*)::int AS c FROM public."StudentsDetails"`);
  const history = await verify.query(`SELECT COUNT(*)::int AS c FROM public."__EFMigrationsHistory"`);
  const fks = await verify.query(`
    SELECT COUNT(*)::int AS c FROM information_schema.table_constraints
    WHERE constraint_schema='public' AND constraint_type='FOREIGN KEY'
  `);
  console.log('Remote verify:', {
    tables: tables.rows[0].c,
    AspNetUsers: users.rows[0].c,
    StudentsDetails: students.rows[0].c,
    StudentAccessments: assessments.rows[0].c,
    EFMigrationsHistory: history.rows[0].c,
    foreignKeys: fks.rows[0].c,
  });
  await verify.end();
  console.log('Export complete.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
