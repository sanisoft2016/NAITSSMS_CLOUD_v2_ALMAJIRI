/**
 * Import NaitsSmsOnlineDbJune2026 -> NaitsSMSApr2026
 * Usage:
 *   node db-import-june-to-apr.mjs validate
 *   node db-import-june-to-apr.mjs import
 */
import { createHash } from 'crypto';
import pg from 'pg';

const SOURCE = 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSmsOnlineDbJune2026';
const DEST = 'postgresql://postgres:123456%40Abc@localhost:5432/NaitsSMSApr2026';

const BATCH = 1000;
const intIdMaps = new Map();
const studentIdToUserId = new Map();
const legacyOfficerIdToUserId = new Map();

function stableGuid(namespace, raw) {
  const key = `${namespace}:${raw}`;
  if (!intIdMaps.has(key)) {
    const h = createHash('sha256').update(key).digest('hex');
    intIdMaps.set(
      key,
      `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(12, 15)}-8${h.slice(15, 18)}-${h.slice(18, 30)}`,
    );
  }
  return intIdMaps.get(key);
}

function mapInt(namespace, value) {
  if (value === null || value === undefined) return null;
  return stableGuid(namespace, value);
}

function field(row, name) {
  if (row == null) return undefined;
  if (Object.prototype.hasOwnProperty.call(row, name)) return row[name];
  const key = Object.keys(row).find((k) => k.toLowerCase() === name.toLowerCase());
  return key ? row[key] : undefined;
}

function str(value, required = false) {
  if (value === null || value === undefined) return required ? '' : null;
  return String(value);
}

function bool(value) {
  if (value === null || value === undefined) return false;
  if (typeof value === 'boolean') return value;
  if (typeof value === 'string') return value === '1' || value.toLowerCase() === 't' || value.toLowerCase() === 'true';
  return Number(value) === 1;
}

function small(value, fallback = 0) {
  if (value === null || value === undefined) return fallback;
  return Number(value);
}

function ts(value, required = false) {
  if (value === null || value === undefined) return required ? new Date(0).toISOString() : null;
  return value instanceof Date ? value.toISOString() : value;
}

function mapSchoolScopeId(value, required = false) {
  const raw = str(value, required);
  if (!raw) {
    return required ? '' : null;
  }

  const normalized = raw.trim().toLowerCase();
  return legacyOfficerIdToUserId.get(normalized) ?? raw;
}

const TABLE_MAP = {
  regions: 'Regions',
  states: 'States',
  localgovernments: 'LocalGovernments',
  terms: 'Terms',
  programyinfoes: 'ProgramInfos',
  affectivexzandpsychomotors: 'AffectiveAndPsychomotors',
  schoolheadtxcategories: 'SchoolHeadCategories',
  aspnetroles: 'AspNetRoles',
  aspnetusers: 'AspNetUsers',
  aspnetuserroles: 'AspNetUserRoles',
  agents: 'Agents',
  schoolregistrationofficers: 'SchoolRegistrationOfficer',
  schooltyinfoes: 'SchoolInfos',
  schoolheadtxes: 'PrincipalDetails',
  schoollevelcategorieslabels: 'SchoolLevelCategoriesLabels',
  txacademicsessions: 'AcademicSessions',
  txyears: 'Years',
  txmonths: 'Months',
  subjectrxcategories: 'SubjectCategories',
  rxclassses: 'Classes',
  rxsubjects: 'Subjects',
  axclassarms: 'ClassArms',
  txemployees: 'ClassTeachers',
  txemployeeincomestructures: 'EmployeeIncomeStructures',
  continuousassessmenttypes: 'ContinuousAssessmentTypes',
  txyearlyandtermlystatus: 'YearlyAndTermlyAssessmentStatus',
  txstudentdetails: 'StudentsDetails',
  studentacademictxprogresshistories: 'StudentAcademicProgressHistories',
  studenttytermlyfixedpayments: 'StudentTermlyFeePayments',
  studenttytermlyfixedpaymentdiscounts: 'FeePaymentDiscounts',
  studentoptionalchargesassignments: 'StudentOPtionalFeesAssignments',
  studentsattexzndanceandcommentsreports: 'StudentsAttendanceReports',
  studenttermlyoptionalcharges: 'StudentTermlyOptionalCharges',
  studenttermlyoptionalpaymentdiscounts: 'StudentTermlyOptionalPaymentDiscounts',
  studenttermlyoptionalpayments: 'StudentOptionalFeePayments',
  studenxtaccessments: 'StudentAccessments',
  subjectstudenttxassignments: 'SubjectStudentAssignments',
  teacherclassarmtxassignments: 'TeacherClassArmAssignments',
  teachtxersubjectarmassignments: 'TeacherSubjectArmAssignments',
  txemployeemonthlypaymentlevels: 'TxEmployeeMonthlyPaymentLevel',
  txemployeesmonthlypayments: 'EmployeesMonthlyPayments',
  setemployeesalaries: 'SetEmployeeSalaries',
  setschoolfeestrs: 'SetFreshierRegistrationFees',
  smspaymentbalances: 'SmsPaymentBalances',
  smspurchases: 'SmsPurchases',
  smsuseds: 'SmsUseds',
  licenstyings: 'Licensings',
  cbtquestionandanswers: 'CbtQuestionsAndAnswers',
  cbtsetquestions: 'CbtSetQuestions',
  affectiveandpsychomotxtorassessments: 'AffectiveAndPsychomotorAssessments',
  xyexpenditureweeks: 'ExpenditureWeeks',
  xyexpensescategories: 'ExpensesCategories',
  xyincomecategories: 'IncomeCategorys',
  xyincomeweeks: 'IncomeWeeks',
  xyexpenses: 'Expenses',
  xyincomes: 'Incomes',
  xyoptionalchargecategories: 'OPtionalFeesCategories',
  optionalxychargeitems: 'OptionalFeesItems',
};

const IMPORT_ORDER = Object.keys(TABLE_MAP);

const COLUMN_OVERRIDES = {
  aspnetroles: {
    NormalizedName: (r) => str(r.name, true)?.toUpperCase() ?? '',
    ConcurrencyStamp: () => stableGuid('role-stamp', Math.random()),
  },
  aspnetusers: {
    NormalizedUserName: (r) => str(r.username, true)?.toUpperCase() ?? '',
    NormalizedEmail: (r) => str(r.email, true)?.toUpperCase() ?? '',
    ConcurrencyStamp: () => stableGuid('user-stamp', Math.random()),
    EmailConfirmed: (r) => bool(r.emailconfirmed),
    PhoneNumberConfirmed: (r) => bool(r.phonenumberconfirmed),
    TwoFactorEnabled: (r) => bool(r.twofactorenabled),
    LockoutEnabled: (r) => bool(r.lockoutenabled),
    LockoutEnd: (r) => ts(r.lockoutenddateutc),
    FirstName: (r) => str(r.firstname, true),
    MiddleName: (r) => str(r.middlename, true),
    SurName: (r) => str(r.surname, true),
  },
  agents: {
    UserId: (r) => str(r.userid, true),
  },
  schoolregistrationofficers: {
    UserId: (r) => {
      const userId = str(r.userid, true);
      const legacyOfficerId = str(field(r, 'id'), false);
      if (legacyOfficerId && userId) {
        legacyOfficerIdToUserId.set(legacyOfficerId.trim().toLowerCase(), userId);
      }
      return userId;
    },
    FullAddress: (r) => str(r.fulladdress, true),
    SignatureImageString: (r) => str(r.signatureimagestring, true),
    AgentUserId: (r) => mapInt('agents', r.agentuserid),
  },
  schooltyinfoes: {
    SchoolRegistrationOfficerId: (r) => mapSchoolScopeId(r.schoolregistrationofficerid, true),
    SchoolName: (r) => str(r.schoolname, true),
    SchoolAddress: (r) => str(r.schooladdress, true),
    Telephone: (r) => str(r.telephone, true),
    PoAddress: (r) => str(r.poaddress, true),
    EmailAddress: (r) => str(r.emailaddress, true),
    Website: (r) => str(r.website, true),
    SchoolLogoString: (r) => str(r.schoollogostring, true),
    Motto: (r) => str(r.motto, true),
    AccountingStartTermId: (r) => (r.accountingstarttermid == null ? null : mapInt('terms', r.accountingstarttermid)),
    SystemImplementationType: (r) => str(r.systemimplementationtype, true),
    SmsApiKey: (r) => str(r.smsapikey, true),
    SmsApiToken: (r) => str(r.smsapitoken, true),
    TotalStudents: (r) => str(r.totalstudents, true),
    FeatureImplementationType: (r) => str(r.featureimplementationtype, true),
    NetworkImplementationType: (r) => str(r.networkimplementationtype, true),
  },
  schoolheadtxes: {
    Id: (r) => mapInt('schoolheadtxes', r.id),
    SchoolInfoId: (r) => mapSchoolScopeId(r.schoolinfoid, true),
    SchoolHeadCategoryId: (r) => small(r.schoolheadcategoryid),
    HeadsName: (r) => str(r.headsname, true),
    PhoneNumber: (r) => str(r.phonenumber, true),
    SignatureImageString: (r) => str(r.signatureimagestring, true),
  },
  schoollevelcategorieslabels: {
    SchoolInfoId: (r) => mapSchoolScopeId(r.schoolinfoid, true),
    GeneralSujectLabel: (r) => str(r.generalsujectlabel, true),
    ReligionSubjectLabel: (r) => str(r.religionsubjectlabel, true),
    PlayClassLabel: (r) => str(r.playclasslabel, true),
    PreNurseryLabel: (r) => str(r.prenurserylabel, true),
    NurseryLabel: (r) => str(r.nurserylabel, true),
    PrimaryLabel: (r) => str(r.primarylabel, true),
    JssLabel: (r) => str(r.jsslabel, true),
    SsLabel: (r) => str(r.sslabel, true),
    CustomLevelLabel: (r) => str(r.customlevellabel, true),
  },
  txacademicsessions: {
    Id: (r) => mapInt('txacademicsessions', field(r, 'id')),
    Session: (r) => str(field(r, 'session'), true),
    SchoolInfoId: (r) => mapSchoolScopeId(field(r, 'schoolinfoid'), true),
  },
  txyears: {
    Id: (r) => mapInt('txyears', r.id),
    SchoolId: (r) => mapSchoolScopeId(r.schoolid, true),
  },
  txmonths: {
    Id: (r) => mapInt('txmonths', r.id),
    YearId: (r) => mapInt('txyears', r.yearid),
  },
  subjectrxcategories: {
    Id: (r) => mapInt('subjectrxcategories', r.id),
    SchoolId: (r) => mapSchoolScopeId(r.schoolid, true),
    SubjectStatusLabel: (r) => str(r.subjectstatuslabel, true),
  },
  rxclassses: {
    Id: (r) => mapInt('rxclassses', r.id),
    SchoolInfoId: (r) => mapSchoolScopeId(r.schoolinfoid, true),
    ClassLabel: (r) => str(r.classlabel, true),
  },
  rxsubjects: {
    Id: (r) => mapInt('rxsubjects', r.id),
    SubjectName: (r) => str(r.subjectname, true),
    SubjectCategoryId: (r) => mapInt('subjectrxcategories', r.subjectcategoryid),
    SchoolTyInfoSchoolRegistrationOfficerId: (r) => null,
  },
  axclassarms: {
    Id: (r) => mapInt('axclassarms', r.id),
    ClassId: (r) => mapInt('rxclassses', r.classid),
    FormMaster: (r) => str(r.formmaster, true),
  },
  txemployees: {
    Id: (r) => mapInt('txemployees', r.id),
    UserId: (r) => str(r.userid, true),
    FullAddress: (r) => str(r.fulladdress, true),
    SignatureImageString: (r) => str(r.signatureimagestring, true),
    SchoolId: (r) => mapSchoolScopeId(r.schoolid, true),
    EmployeeIdNo: (r) => str(r.employeeidno, true),
    Decipline: (r) => str(r.decipline, true),
  },
  txemployeeincomestructures: {
    Id: (r) => mapInt('txemployees', r.employeeid),
    BasicSalary: (r) => Number(r.basicsalary ?? 0),
    Allowance: (r) => Number(r.allowance ?? 0),
  },
  continuousassessmenttypes: {
    Id: (r) => mapInt('continuousassessmenttypes', r.id),
    SchoolId: (r) => mapSchoolScopeId(r.schoolid, true),
  },
  txyearlyandtermlystatus: {
    Id: (r) => mapInt('txyearlyandtermlystatus', r.id),
    SessionId: (r) => mapInt('txacademicsessions', r.sessionid),
    HeadmasterId: (r) => (r.headmasterid == null ? null : mapInt('schoolheadtxes', r.headmasterid)),
    PrincipalId: (r) => (r.principalid == null ? null : mapInt('schoolheadtxes', r.principalid)),
    CreatedDate: (r) => ts(r.createddate, true),
  },
  txstudentdetails: {
    UserId: (r) => {
      studentIdToUserId.set(String(r.id), String(r.userid));
      return str(r.userid, true);
    },
    ClassArmId: (r) => mapInt('axclassarms', r.classarmid),
    SchoolInfoId: (r) => mapSchoolScopeId(r.schoolinfoid, true),
    PlaceOfBirth: (r) => str(r.placeofbirth, true),
    BloodGroup: (r) => str(r.bloodgroup, true),
    TransferedFromWhere: (r) => str(r.transferedfromwhere, true),
    StartClass: (r) => str(r.startclass, true),
    Passport: (r) => str(r.passport, true),
    GuardiansName: (r) => str(r.guardiansname, true),
    Relationship: (r) => str(r.relationship, true),
    GuardianAddress: (r) => str(r.guardianaddress, true),
    Occupation: (r) => str(r.occupation, true),
    MobileNumber: (r) => str(r.mobilenumber, true),
    HomeNumber: (r) => str(r.homenumber, true),
    EmailAddress: (r) => str(r.emailaddress, true),
    MailingAddress: (r) => str(r.mailingaddress, true),
    Country: (r) => str(r.country, true),
    TimeStatus: (r) => small(r.timestatus ?? 0, 0),
    // Legacy AdmisnTermId FK -> YearlyAndTermlyStatus (same as selectedTermId), NOT Terms.
    AdmisnTermId: (r) => mapInt('txyearlyandtermlystatus', r.admisntermid),
  },
  studentacademictxprogresshistories: {
    Id: (r) => mapInt('studentacademictxprogresshistories', r.id),
    StudentId: (r) => studentIdToUserId.get(String(r.studentid)) ?? str(r.studentid, true),
    YearlyAndTermlyStatusId: (r) => mapInt('txyearlyandtermlystatus', r.yearlyandtermlystatusid),
    ClassId: (r) => mapInt('rxclassses', r.classid),
  },
  studenxtaccessments: {
    Id: (r) => str(r.id, true),
    StudentId: (r) => studentIdToUserId.get(String(r.studentid)) ?? str(r.studentid, true),
    SubjectId: (r) => mapInt('rxsubjects', r.subjectid),
    EmployeeId: (r) => mapInt('txemployees', r.employeeid),
    ClassArmId: (r) => mapInt('axclassarms', r.classarmid),
    YearlyAndTermlyStatusId: (r) => mapInt('txyearlyandtermlystatus', r.yearlyandtermlystatusid),
    LastUpdateDate: (r) => ts(r.lastupdatedate, true),
  },
  teacherclassarmtxassignments: {
    Id: (r) => mapInt('teacherclassarmtxassignments', r.id),
    EmployeeId: (r) => mapInt('txemployees', r.employeeid),
    ArmId: (r) => mapInt('axclassarms', r.armid),
    ArmTermlyAssessmentStatus: (r) => small(r.armtermlyassessmentstatus),
  },
  teachtxersubjectarmassignments: {
    Id: (r) => mapInt('teachtxersubjectarmassignments', r.id),
    SubjectId: (r) => mapInt('rxsubjects', r.subjectid),
    EmployeeId: (r) => mapInt('txemployees', r.employeeid),
    ArmId: (r) => mapInt('axclassarms', r.armid),
  },
  studentsattexzndanceandcommentsreports: {
    Id: (r) => mapInt('studentsattexzndanceandcommentsreports', r.id),
    StudentId: (r) => studentIdToUserId.get(String(r.studentid)) ?? str(r.studentid, true),
    ClassArmId: (r) => mapInt('axclassarms', r.classarmid),
    YearlyAndTermlyStatusId: (r) => mapInt('txyearlyandtermlystatus', r.yearlyandtermlystatusid),
    TeacherComments: (r) => str(r.teachercomments, true),
    SchoolHeadComments: (r) => str(r.schoolheadcomments, true),
  },
  affectiveandpsychomotxtorassessments: {
    Id: (r) => str(r.id, true),
    YearlyAndTermlyStatusId: (r) => mapInt('txyearlyandtermlystatus', r.yearlyandtermlystatusid),
    StudentId: (r) => studentIdToUserId.get(String(r.studentid)) ?? str(r.studentid, true),
    ClassArmId: (r) => mapInt('axclassarms', r.classarmid),
    FormMasterId: (r) => mapInt('txemployees', r.formmasterid),
  },
  licenstyings: {
    Id: (r) => mapInt('licenstyings', r.id),
    SchoolSmsId: (r) => mapSchoolScopeId(r.schoolsmsid, true),
    AgentUserId: (r) => mapInt('agents', r.agentuserid),
    PaymentDate: (r) => ts(r.paymentdate, true),
    ExpiryDate: (r) => ts(r.expirydate, true),
  },
  smspurchases: {
    Id: (r) => mapInt('smspurchases', r.id),
    SchoolSmsId: (r) => mapSchoolScopeId(r.schoolsmsid, true),
    AgentUserId: (r) => mapInt('agents', r.agentuserid),
    PaymentDate: (r) => ts(r.paymentdate, true),
  },
  smsuseds: {
    Id: (r) => mapInt('smsuseds', r.id),
    SchoolSmsId: (r) => mapSchoolScopeId(r.schoolsmsid, true),
    UsedDate: (r) => ts(r.useddate, true),
  },
  smspaymentbalances: {
    SchoolSmsId: (r) => mapSchoolScopeId(r.schoolsmsid, true),
  },
  cbtquestionandanswers: {
    Id: (r) => mapInt('cbtquestionandanswers', r.id),
    Question: (r) => str(r.question, true),
    AnswerOptions: (r) => str(r.answeroptions, true),
    ClassId: (r) => mapInt('rxclassses', r.classid),
    SubjectId: (r) => mapInt('rxsubjects', r.subjectid),
  },
  cbtsetquestions: {
    Id: (r) => mapInt('cbtsetquestions', r.id),
    QuestionAndAnswerId: (r) => mapInt('cbtquestionandanswers', r.questionandanswerid),
  },
};

// Source columns with mixed case must be read via field(); autoMapRow already matches case-insensitively.
async function validateMixedCaseColumns(source) {
  const { rows } = await source.query(`
    SELECT table_name, column_name
    FROM information_schema.columns
    WHERE table_schema = 'public' AND column_name <> lower(column_name)
    ORDER BY table_name, column_name
  `);

  const mixedCase = rows
    .filter((r) => TABLE_MAP[r.table_name])
    .map((r) => `${r.table_name}.${r.column_name}`);

  const issues = [];
  for (const { table_name, column_name } of rows) {
    if (!TABLE_MAP[table_name]) continue;
    const overrides = COLUMN_OVERRIDES[table_name];
    if (!overrides) continue;

    const destCol = snakeToPascal(column_name);
    if (!overrides[destCol] && !overrides[column_name]) continue;

    const lower = column_name.toLowerCase();
    const overrideSrc = overrides[destCol]?.toString() ?? overrides[column_name]?.toString() ?? '';
    if (/\br\.[a-z][a-z0-9]*\b/.test(overrideSrc) && !overrideSrc.includes('field(r,')) {
      issues.push(`${table_name}.${column_name}: override uses r.${lower} instead of field(r, '${lower}')`);
    }
  }

  return { mixedCase, issues };
}

function snakeToPascal(name) {
  return name
    .replace(/(^|_)([a-z])/g, (_, __, c) => c.toUpperCase())
    .replace(/_/g, '');
}

async function loadSchema(client) {
  const { rows } = await client.query(`
    SELECT table_name, column_name, data_type, character_maximum_length, is_nullable
    FROM information_schema.columns
    WHERE table_schema = 'public'
  `);
  const schema = new Map();
  for (const row of rows) {
    if (!schema.has(row.table_name)) schema.set(row.table_name, new Map());
    schema.get(row.table_name).set(row.column_name, row);
  }
  return schema;
}

function autoMapRow(sourceRow, destTable, destSchema, overrides = {}) {
  const out = {};
  for (const [destCol, meta] of destSchema.entries()) {
    if (overrides[destCol]) {
      out[destCol] = overrides[destCol](sourceRow);
      continue;
    }

    const sourceKey = Object.keys(sourceRow).find((k) => k.toLowerCase() === destCol.toLowerCase());
    if (!sourceKey) {
      if (meta.is_nullable === 'NO' && meta.data_type.includes('character')) out[destCol] = '';
      else if (meta.is_nullable === 'NO' && ['smallint', 'integer', 'bigint', 'real', 'double precision', 'numeric'].includes(meta.data_type))
        out[destCol] = 0;
      else if (meta.is_nullable === 'NO' && meta.data_type.includes('timestamp')) out[destCol] = new Date(0).toISOString();
      continue;
    }

    let value = sourceRow[sourceKey];
    if (value === null || value === undefined) {
      out[destCol] = meta.is_nullable === 'NO' ? (meta.data_type.includes('character') ? '' : value) : null;
      continue;
    }

    if (meta.data_type === 'boolean' && typeof value !== 'boolean') out[destCol] = bool(value);
    else out[destCol] = value;
  }
  return out;
}

async function validateLengths(source, destSchema) {
  const issues = [];

  for (const [sourceTable, destTable] of Object.entries(TABLE_MAP)) {
    const destCols = destSchema.get(destTable);
    if (!destCols) continue;

    const { rows: sourceCols } = await source.query(`
      SELECT column_name, character_maximum_length
      FROM information_schema.columns
      WHERE table_schema='public' AND table_name=$1 AND character_maximum_length IS NOT NULL
    `, [sourceTable]);

    for (const sc of sourceCols) {
      const destColName = [...destCols.keys()].find((c) => c.toLowerCase() === sc.column_name.toLowerCase());
      if (!destColName) continue;
      const destLen = destCols.get(destColName).character_maximum_length;
      if (!destLen) continue;

      const { rows } = await source.query(
        `SELECT MAX(LENGTH("${sc.column_name}")) AS maxlen FROM "${sourceTable}" WHERE "${sc.column_name}" IS NOT NULL`,
      );
      const maxlen = Number(rows[0]?.maxlen ?? 0);
      if (maxlen > destLen) {
        issues.push({ sourceTable, destTable, column: destColName, sourceMax: maxlen, destMax: destLen });
      }
    }
  }

  return issues;
}

async function importTable(source, dest, sourceTable, destTable, destSchema) {
  const overrides = COLUMN_OVERRIDES[sourceTable] ?? {};
  const countRes = await source.query(`SELECT COUNT(*)::int AS c FROM "${sourceTable}"`);
  const total = countRes.rows[0].c;
  if (total === 0) {
    console.log(`  skip ${sourceTable} (0 rows)`);
    return 0;
  }

  console.log(`  import ${sourceTable} -> ${destTable} (${total} rows)`);
  let offset = 0;
  let inserted = 0;

  while (offset < total) {
    const { rows } = await source.query(`SELECT * FROM "${sourceTable}" ORDER BY 1 LIMIT ${BATCH} OFFSET ${offset}`);
    if (!rows.length) break;

    const mapped = rows.map((row) => autoMapRow(row, destTable, destSchema, overrides));
    const columns = Object.keys(mapped[0]);
    const values = [];
    const placeholders = mapped.map((row, ri) => {
      const base = ri * columns.length;
      columns.forEach((col) => values.push(row[col]));
      return `(${columns.map((_, ci) => `$${base + ci + 1}`).join(',')})`;
    });

    const sql = `INSERT INTO "${destTable}" (${columns.map((c) => `"${c}"`).join(',')}) VALUES ${placeholders.join(',')}`;
    await dest.query(sql, values);
    inserted += rows.length;
    offset += BATCH;
    if (inserted % 10000 === 0 || inserted === total) {
      console.log(`    ${inserted}/${total}`);
    }
  }

  return inserted;
}

async function clearDestinationTable(dest, destTable) {
  // import-table uses row DELETE; disable FK triggers like the full import path does.
  await dest.query(`SET session_replication_role = 'replica'`);
  try {
    if (destTable === 'AcademicSessions') {
      await dest.query(`
        DELETE FROM "YearlyAndTermlyAssessmentStatus"
        WHERE "SessionId" IN (SELECT "Id" FROM "AcademicSessions")
      `);
    }

    await dest.query(`DELETE FROM "${destTable}"`);
  } finally {
    await dest.query(`SET session_replication_role = 'origin'`);
  }
}

async function truncateDestination(dest) {
  const { rows } = await dest.query(`
    SELECT tablename FROM pg_tables
    WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory'
    ORDER BY tablename
  `);
  const names = rows.map((r) => `"${r.tablename}"`).join(', ');
  if (names) {
    await dest.query(`TRUNCATE TABLE ${names} RESTART IDENTITY CASCADE`);
  }
}

async function main() {
  const mode = process.argv[2] ?? 'validate';
  const singleTable = process.argv[3];
  const source = new pg.Client({ connectionString: SOURCE });
  const dest = new pg.Client({ connectionString: DEST });
  await source.connect();
  await dest.connect();

  try {
    const destSchema = await loadSchema(dest);

    if (mode === 'validate') {
      console.log('Validating string lengths...');
      const issues = await validateLengths(source, destSchema);
      if (issues.length) {
        console.error('\nSTRING LENGTH VIOLATIONS (import paused):');
        for (const i of issues) {
          console.error(`  ${i.sourceTable}.${i.column}: source max ${i.sourceMax} > dest max ${i.destMax}`);
        }
        process.exitCode = 2;
        return;
      }
      console.log('No string length violations found.');

      console.log('\nChecking mixed-case source columns...');
      const { mixedCase, issues: mixedCaseIssues } = await validateMixedCaseColumns(source);
      if (mixedCase.length) {
        console.log(`Mixed-case columns in import tables: ${mixedCase.join(', ')}`);
      } else {
        console.log('No mixed-case columns in import tables.');
      }
      if (mixedCaseIssues.length) {
        console.error('\nMIXED-CASE OVERRIDE ISSUES (import paused):');
        for (const i of mixedCaseIssues) console.error(`  ${i}`);
        process.exitCode = 2;
        return;
      }
      console.log('No mixed-case override issues found.');
      return;
    }

    if (mode === 'import-table') {
      if (!singleTable || !TABLE_MAP[singleTable]) {
        console.error('Usage: node db-import-june-to-apr.mjs import-table <sourceTable>');
        process.exitCode = 1;
        return;
      }
      const destTable = TABLE_MAP[singleTable];
      await clearDestinationTable(dest, destTable);
      const count = await importTable(source, dest, singleTable, destTable, destSchema.get(destTable));
      console.log(`Imported ${count} rows into ${destTable}`);
      return;
    }

    if (mode !== 'import') {
      console.error('Usage: node db-import-june-to-apr.mjs [validate|import]');
      process.exitCode = 1;
      return;
    }

    console.log('Truncating destination tables...');
    await truncateDestination(dest);
    await dest.query(`SET session_replication_role = 'replica'`);

    let grandTotal = 0;
    for (const sourceTable of IMPORT_ORDER) {
      const destTable = TABLE_MAP[sourceTable];
      if (!destSchema.has(destTable)) {
        console.warn(`  missing dest table ${destTable}, skip`);
        continue;
      }
      grandTotal += await importTable(source, dest, sourceTable, destTable, destSchema.get(destTable));
    }

    await dest.query(`SET session_replication_role = 'origin'`);
    console.log(`\nImport complete. Rows inserted: ${grandTotal}`);
  } finally {
    await source.end();
    await dest.end();
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
