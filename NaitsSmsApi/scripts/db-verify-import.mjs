#!/usr/bin/env node
import pg from 'pg';

const checks = [
  ['aspnetusers', 'AspNetUsers'],
  ['aspnetuserroles', 'AspNetUserRoles'],
  ['txstudentdetails', 'StudentsDetails'],
  ['studenxtaccessments', 'StudentAccessments'],
  ['affectiveandpsychomotxtorassessments', 'AffectiveAndPsychomotorAssessments'],
  ['studentacademictxprogresshistories', 'StudentAcademicProgressHistories'],
  ['rxclassses', 'Classes'],
  ['rxsubjects', 'Subjects'],
  ['txemployees', 'ClassTeachers'],
  ['teachtxersubjectarmassignments', 'TeacherSubjectArmAssignments'],
];

const source = new pg.Client({ connectionString: 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSmsOnlineDbJune2026' });
const dest = new pg.Client({ connectionString: 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSMSApr2026' });

await source.connect();
await dest.connect();

console.log('Table verification (source vs destination):');
for (const [src, dst] of checks) {
  const s = await source.query(`SELECT COUNT(*)::int AS c FROM "${src}"`);
  const d = await dest.query(`SELECT COUNT(*)::int AS c FROM "${dst}"`);
  const ok = s.rows[0].c === d.rows[0].c ? 'OK' : 'MISMATCH';
  console.log(`  ${ok} ${src}: ${s.rows[0].c} -> ${dst}: ${d.rows[0].c}`);
}

await source.end();
await dest.end();
