import pg from 'pg';

const DEST = 'postgresql://naitsAdmin:naits123%25A%21bc@localhost:5434/NaitsAmaDb';
const tables = [
  'TeacherClassArmAssignments',
  'TeacherSubjectArmAssignments',
  'TxEmployeeMonthlyPaymentLevel',
  'EmployeesMonthlyPayments',
  'SetEmployeeSalaries',
  'SetFreshierRegistrationFees',
  'SmsPaymentBalances',
  'SmsPurchases',
  'SmsUseds',
  'Licensings',
  'CbtQuestionsAndAnswers',
  'CbtSetQuestions',
  'AffectiveAndPsychomotorAssessments',
  'ExpenditureWeeks',
  'ExpensesCategories',
  'IncomeCategorys',
  'IncomeWeeks',
  'Expenses',
  'Incomes',
  'OPtionalFeesCategories',
  'OptionalFeesItems',
  'StudentAccessments',
  'StudentsDetails',
];

const c = new pg.Client({ connectionString: DEST });
await c.connect();
for (const t of tables) {
  const r = await c.query(`SELECT COUNT(*)::bigint AS c FROM "${t}"`);
  console.log(`${t}: ${r.rows[0].c}`);
}
await c.end();
