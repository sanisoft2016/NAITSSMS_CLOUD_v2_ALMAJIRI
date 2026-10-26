import { ArmName } from '@shared/interfaces';

import {
  AssessmentRecord,
  AttendanceRecord,
  AffectiveRecord,
  CasSettingRecord,
  ReportPayload,
  termLabel,
} from './student-results.models';

export interface SchoolInfoVm {
  schoolName?: string | null;
  schoolAddress?: string | null;
  motto?: string | null;
  schoolLogoString?: string | null;
}

export interface ReportBuildContext {
  sessionLabel: string;
  termLabel: string;
  termStatus: string;
  schoolInfo: SchoolInfoVm | null;
  logoUrl: string;
  apiOrigin: string;
  /** Bust prior immutable 404 caches for Passport/Signatures/logos. */
  mediaCacheBust?: number;
  filterStudentId?: string;
}

export interface CasColumnConfig {
  key: 'ca1' | 'ca2' | 'ca3';
  label: string;
  percent: number;
}

export interface CasLayout {
  columns: CasColumnConfig[];
  examPercent: number;
  colSpan: number;
}

export interface SubjectReportRow {
  subjectName: string;
  ca1: number;
  ca2: number;
  ca3: number;
  exam: number;
  total: number;
  grade: string;
  position: number;
  outOf: number;
  minScore: number;
  maxScore: number;
  classAvg: number;
  remarks: string;
}

export interface AffectiveItemRow {
  itemName: string;
  score: number | string;
}

export type ReportLayoutDensity = 'legacy';

export interface ReportSheetViewModel {
  studentId: string;
  studentName: string;
  admissionNo: string;
  classLabel: string;
  armLabel: string;
  passportUrl: string;
  schoolName: string;
  schoolAddress: string;
  schoolMotto: string;
  schoolLogoUrl: string;
  academicSession: string;
  termLabel: string;
  reportTitle: string;
  fontSize: string;
  layoutDensity: ReportLayoutDensity;
  layoutVars: Record<string, string>;
  colSpan: number;
  casLayout: CasLayout;
  subjects: SubjectReportRow[];
  subjectCount: number;
  totalScore: number;
  finalAverage: number;
  finalGrade: string;
  classPosition: string;
  classHighestAverage: number;
  classLowestAverage: number;
  studentsInClass: number;
  daysOpen: number | string;
  daysPresent: number | string;
  daysAbsent: number | string;
  affectiveItems: AffectiveItemRow[];
  psychomotorItems: AffectiveItemRow[];
  teacherComment: string;
  headComment: string;
  formMasterName: string;
  formMasterSignatureUrl: string;
  schoolHeadName: string;
  schoolHeadTitle: string;
  schoolHeadSignatureUrl: string;
  nextTermBegins: string;
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

function readString(raw: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = raw[key];
    if (value !== null && value !== undefined && String(value).trim()) {
      return String(value).trim();
    }
  }
  return '';
}

function assessmentTotal(row: AssessmentRecord): number {
  return Number(row.ca1 ?? 0) + Number(row.ca2 ?? 0) + Number(row.ca3 ?? 0) + Number(row.exams ?? 0);
}

function groupBy<T>(items: T[], keyFn: (item: T) => string): Map<string, T[]> {
  const map = new Map<string, T[]>();
  for (const item of items) {
    const key = keyFn(item);
    const bucket = map.get(key) ?? [];
    bucket.push(item);
    map.set(key, bucket);
  }
  return map;
}

/**
 * Legacy PerArmAndThirdTermCumulativeResultProcessing.cshtml lines 172–197.
 * Font size is driven only by distinct SubjectId count.
 */
export function resolveLegacyFontSize(distinctSubjectCount: number): string {
  if (distinctSubjectCount <= 7) {
    return '15px';
  }
  if (distinctSubjectCount <= 10) {
    return '15px';
  }
  if (distinctSubjectCount <= 12) {
    return '14px';
  }
  if (distinctSubjectCount <= 15) {
    return '13px';
  }
  if (distinctSubjectCount <= 18) {
    return '12.75px';
  }
  if (distinctSubjectCount <= 21) {
    return '10px';
  }
  return '9px';
}

export function resolveCasLayout(casSetting?: CasSettingRecord | null): CasLayout {
  const category = String(casSetting?.casCategory ?? 'twocas').toLowerCase();
  const perCas = Number(casSetting?.perCasValue ?? 0);

  // Legacy CasCategory enum: OneCA=0, TwoCAs=1, ThreeCAs=2
  if (category === 'threecas' || category === '2') {
    const caValue = perCas > 0 ? perCas : 10;
    return {
      examPercent: 100 - caValue * 3,
      colSpan: 12,
      columns: [
        { key: 'ca1', label: `CA1 (${caValue}%)`, percent: caValue },
        { key: 'ca2', label: `CA2 (${caValue}%)`, percent: caValue },
        { key: 'ca3', label: `CA3 (${caValue}%)`, percent: caValue },
      ],
    };
  }

  if (category === 'oneca' || category === '0') {
    const caValue = perCas > 0 ? perCas : 40;
    return {
      examPercent: 100 - caValue,
      colSpan: 10,
      columns: [{ key: 'ca1', label: `CA (${caValue}%)`, percent: caValue }],
    };
  }

  const caValue = perCas > 0 ? perCas : 20;
  return {
    examPercent: 100 - caValue * 2,
    colSpan: 11,
    columns: [
      { key: 'ca1', label: `CA1 (${caValue}%)`, percent: caValue },
      { key: 'ca2', label: `CA2 (${caValue}%)`, percent: caValue },
    ],
  };
}

export function getGrade(totalScore: number): { grade: string; remarks: string } {
  if (totalScore < 40) return { grade: 'F', remarks: 'Fail' };
  if (totalScore < 45) return { grade: 'E', remarks: 'Fair' };
  if (totalScore < 50) return { grade: 'D', remarks: 'Pass' };
  if (totalScore < 60) return { grade: 'C', remarks: 'Good' };
  if (totalScore < 70) return { grade: 'B', remarks: 'V.Good' };
  return { grade: 'A', remarks: 'Excellent' };
}

function defaultComments(grade: string): { teacher: string; head: string } {
  switch (grade) {
    case 'F':
      return { teacher: 'Very Poor result, buckle up next term please.', head: 'You need to wake up, please.' };
    case 'E':
      return { teacher: 'Poor Performance', head: 'A fair performance, wake up please.' };
    case 'D':
      return { teacher: 'You need to put more effort.', head: 'Put more effort in your weak subject, please.' };
    case 'C':
      return {
        teacher: 'Its a good result, but you can strive to do better.',
        head: 'Good, but put more effort next term, please.',
      };
    case 'B':
      return { teacher: 'Good Performance', head: 'A very good result, keep it up.' };
    case 'A':
      return { teacher: 'A brilliant work, keep it up.', head: 'An excellent performance, keep it up.' };
    default:
      return { teacher: 'A brilliant work, keep it up.', head: 'An excellent performance, keep it up.' };
  }
}

/**
 * Legacy ordinal formatting for class position (lines 382–421).
 */
function formatClassPosition(classPositionCounter: number): string {
  let classPosition = String(classPositionCounter);
  if (classPositionCounter > 0 && classPositionCounter < 21) {
    if (classPosition === '1') return '1st';
    if (classPosition === '2') return '2nd';
    if (classPosition === '3') return '3rd';
    return `${classPosition}th`;
  }
  if (classPositionCounter > 20) {
    const lastDigit = classPosition[classPosition.length - 1];
    if (lastDigit === '1') return `${classPosition}st`;
    if (lastDigit === '2') return `${classPosition}nd`;
    if (lastDigit === '3') return `${classPosition}rd`;
    return `${classPosition}th`;
  }
  return classPosition;
}

function resolveArmLabel(row: AssessmentRecord): string {
  const armName = row.classArm?.armName;
  const numeric = Number(armName);
  if (Number.isInteger(numeric) && ArmName[numeric] !== undefined) {
    return ArmName[numeric];
  }
  return String(armName ?? '').trim();
}

function resolveStudentName(row: AssessmentRecord): string {
  return (
    row.student?.fullName ??
    row.student?.user?.fullName ??
    [row.student?.user?.firstName ?? row.student?.firstName, row.student?.user?.surName ?? row.student?.surName]
      .filter(Boolean)
      .join(' ')
      .trim()
  );
}

function mediaBaseName(raw: string): string {
  const trimmed = String(raw ?? '')
    .trim()
    .replace(/\\/g, '/');
  if (!trimmed) {
    return '';
  }
  const parts = trimmed.split('/').filter(Boolean);
  return parts[parts.length - 1] || trimmed;
}

function withMediaCacheBust(url: string, bust?: number): string {
  if (!url || url.startsWith('data:') || bust == null) {
    return url;
  }
  const sep = url.includes('?') ? '&' : '?';
  return `${url}${sep}v=${bust}`;
}

function resolvePassportUrl(apiOrigin: string, passport?: string | null, bust?: number): string {
  const fileName = mediaBaseName(String(passport ?? ''));
  if (!fileName) {
    return withMediaCacheBust(`${apiOrigin}/Passport/no-passport.jpg`, bust);
  }
  if (/^https?:\/\//i.test(fileName)) {
    return withMediaCacheBust(fileName, bust);
  }
  return withMediaCacheBust(`${apiOrigin}/Passport/${encodeURIComponent(fileName)}`, bust);
}

function resolveSignatureUrl(apiOrigin: string, signature?: string | null, bust?: number): string {
  const raw = String(signature ?? '').trim();
  if (!raw) {
    return '';
  }
  if (raw.startsWith('data:image')) {
    return raw;
  }
  if (/^https?:\/\//i.test(raw)) {
    return withMediaCacheBust(raw, bust);
  }
  // Already a site-relative media path (/Signatures/..., /Content/Signatures/..., etc.)
  if (raw.startsWith('/')) {
    return withMediaCacheBust(`${apiOrigin}${raw}`, bust);
  }
  const fileName = mediaBaseName(raw);
  if (!fileName) {
    return '';
  }
  return withMediaCacheBust(`${apiOrigin}/Signatures/${encodeURIComponent(fileName)}`, bust);
}

function resolveSchoolHeadTitle(
  categoryId: number | null | undefined,
  gender: number | string | null | undefined,
): string {
  if (categoryId === 2) {
    return 'Principal';
  }
  if (gender === 1 || gender === 'Female' || String(gender).toLowerCase() === 'female') {
    return 'Headmistress';
  }
  if (categoryId === 1) {
    return 'Headmaster';
  }
  return '';
}

function rowAverage(rows: AssessmentRecord[]): number {
  if (rows.length === 0) {
    return 0;
  }
  return rows.reduce((sum, row) => sum + assessmentTotal(row), 0) / rows.length;
}

function subjectAverageRows(rows: AssessmentRecord[]): {
  ca1: number;
  ca2: number;
  ca3: number;
  exams: number;
  total: number;
  subjectName: string;
  subjectId: string;
  studentId: string;
  classArmId: string;
} {
  const count = rows.length || 1;
  return {
    ca1: rows.reduce((sum, row) => sum + Number(row.ca1 ?? 0), 0) / count,
    ca2: rows.reduce((sum, row) => sum + Number(row.ca2 ?? 0), 0) / count,
    ca3: rows.reduce((sum, row) => sum + Number(row.ca3 ?? 0), 0) / count,
    exams: rows.reduce((sum, row) => sum + Number(row.exams ?? 0), 0) / count,
    total: rows.reduce((sum, row) => sum + assessmentTotal(row), 0) / count,
    subjectName: String(rows[0]?.subject?.subjectName ?? '-'),
    subjectId: String(rows[0]?.subjectId ?? ''),
    studentId: String(rows[0]?.studentId ?? ''),
    classArmId: String(rows[0]?.classArmId ?? ''),
  };
}

function yearlyStatusId(row: AssessmentRecord): string {
  return String(row.yearlyAndTermlyStatusId ?? row.yearlyAndTermlyStatus?.id ?? '');
}

function termStatusOf(row: AssessmentRecord | undefined): number | null {
  if (!row) {
    return null;
  }
  const raw = row.yearlyAndTermlyStatus?.termStatus;
  if (raw === null || raw === undefined) {
    return null;
  }
  const numeric = Number(raw);
  return Number.isFinite(numeric) ? numeric : null;
}

/**
 * Prefer explicit TermStatus over YearlyAndTermlyStatusId GUID order.
 * GUID localeCompare is not chronological; schools like wingate can sort Third before First/Second
 * and then render cumulative payloads with the non-third (per-term-row) subject layout.
 */
function compareByTermThenStatusId(a: AssessmentRecord, b: AssessmentRecord): number {
  const termA = termStatusOf(a);
  const termB = termStatusOf(b);
  if (termA !== null && termB !== null && termA !== termB) {
    return termA - termB;
  }
  if (termA !== null && termB === null) {
    return 1;
  }
  if (termA === null && termB !== null) {
    return -1;
  }
  return yearlyStatusId(a).localeCompare(yearlyStatusId(b));
}

function pickLatestTermRow(rows: AssessmentRecord[]): AssessmentRecord | undefined {
  if (rows.length === 0) {
    return undefined;
  }
  return [...rows].sort(compareByTermThenStatusId)[rows.length - 1];
}

function isThirdTermStudentRows(rows: AssessmentRecord[], selectedTermStatus?: string): boolean {
  if (rows.length === 0) {
    return false;
  }
  if (selectedTermStatus !== undefined && selectedTermStatus !== '' && selectedTermStatus !== '-1') {
    const selected = Number(selectedTermStatus);
    if (Number.isFinite(selected)) {
      return selected === 2;
    }
  }
  const statuses = rows.map(termStatusOf).filter((value): value is number => value !== null);
  if (statuses.length > 0) {
    return Math.max(...statuses) === 2;
  }
  return termStatusOf(pickLatestTermRow(rows)!) === 2;
}

function buildSubjectRowsNonThird(
  studentRows: AssessmentRecord[],
  armStudentGroups: AssessmentRecord[][],
): SubjectReportRow[] {
  const orderStudent = [...studentRows].sort((a, b) =>
    String(a.subject?.subjectName ?? '').localeCompare(String(b.subject?.subjectName ?? '')),
  );
  const allArmRows = armStudentGroups.flat();

  return orderStudent.map(eachSubject => {
    const total = assessmentTotal(eachSubject);
    const gradeInfo = getGrade(total);
    const subjectId = String(eachSubject.subjectId ?? '');
    const classArmId = String(eachSubject.classArmId ?? '');

    const orderedCurrentSubjectClassArmStudentsScores = allArmRows
      .filter(
        r => String(r.subjectId ?? '') === subjectId && String(r.classArmId ?? '') === classArmId,
      )
      .sort((a, b) => assessmentTotal(b) - assessmentTotal(a));

    const outOf = orderedCurrentSubjectClassArmStudentsScores.length;
    const totals = orderedCurrentSubjectClassArmStudentsScores.map(assessmentTotal);
    let subjectScorePosition = 1;
    for (const eachItem of orderedCurrentSubjectClassArmStudentsScores) {
      if (String(eachItem.studentId ?? '') === String(eachSubject.studentId ?? '')) {
        break;
      }
      subjectScorePosition += 1;
    }

    return {
      subjectName: String(eachSubject.subject?.subjectName ?? '-'),
      ca1: round1(Number(eachSubject.ca1 ?? 0)),
      ca2: round1(Number(eachSubject.ca2 ?? 0)),
      ca3: round1(Number(eachSubject.ca3 ?? 0)),
      exam: round1(Number(eachSubject.exams ?? 0)),
      total: round1(total),
      grade: gradeInfo.grade,
      position: subjectScorePosition,
      outOf,
      minScore: round1(totals.length ? Math.min(...totals) : 0),
      maxScore: round1(totals.length ? Math.max(...totals) : 0),
      classAvg: round1(totals.length ? totals.reduce((s, v) => s + v, 0) / outOf : 0),
      remarks: gradeInfo.remarks,
    };
  });
}

function buildSubjectRowsThirdTerm(
  studentRows: AssessmentRecord[],
  armStudentGroups: AssessmentRecord[][],
): SubjectReportRow[] {
  const allArmRows = armStudentGroups.flat();
  const groupBySubject = [...groupBy(studentRows, r => String(r.subjectId ?? '')).entries()].sort((a, b) =>
    String(a[1][0]?.subject?.subjectName ?? '').localeCompare(String(b[1][0]?.subject?.subjectName ?? '')),
  );

  return groupBySubject.map(([, eachSubject]) => {
    const averaged = subjectAverageRows(eachSubject);
    const gradeInfo = getGrade(averaged.total);
    const subjectId = averaged.subjectId;
    const classArmId = averaged.classArmId;

    const subjectArmRows = allArmRows.filter(
      r => String(r.subjectId ?? '') === subjectId && String(r.classArmId ?? '') === classArmId,
    );

    const orderedByStudent = [...groupBy(subjectArmRows, r => String(r.studentId ?? '')).values()].sort(
      (a, b) => subjectAverageRows(b).total - subjectAverageRows(a).total,
    );

    // Legacy: GroupBy YearlyAndTermlyStatusId then Average(Count)
    const byTerm = groupBy(subjectArmRows, yearlyStatusId);
    const termCounts = [...byTerm.values()].map(g => g.length);
    const outOf = termCounts.length
      ? Math.round(termCounts.reduce((s, c) => s + c, 0) / termCounts.length)
      : orderedByStudent.length;

    const studentTotals = orderedByStudent.map(g => subjectAverageRows(g).total);
    let subjectScorePosition = 1;
    for (const eachItem of orderedByStudent) {
      if (String(eachItem[0]?.studentId ?? '') === averaged.studentId) {
        break;
      }
      subjectScorePosition += 1;
    }

    return {
      subjectName: averaged.subjectName,
      ca1: round1(averaged.ca1),
      ca2: round1(averaged.ca2),
      ca3: round1(averaged.ca3),
      exam: round1(averaged.exams),
      total: round1(averaged.total),
      grade: gradeInfo.grade,
      position: subjectScorePosition,
      outOf,
      minScore: round1(studentTotals.length ? Math.min(...studentTotals) : 0),
      maxScore: round1(studentTotals.length ? Math.max(...studentTotals) : 0),
      classAvg: round1(
        studentTotals.length && outOf > 0
          ? studentTotals.reduce((s, v) => s + v, 0) / outOf
          : 0,
      ),
      remarks: gradeInfo.remarks,
    };
  });
}

function normalizeAttendance(raw?: AttendanceRecord | Record<string, unknown> | null): AttendanceRecord {
  const source = (raw ?? {}) as Record<string, unknown>;
  return {
    studentId: readString(source, 'studentId', 'StudentId'),
    totalPresent: Number(source['attendance'] ?? source['Attendance'] ?? source['totalPresent'] ?? source['TotalPresent'] ?? 0),
    totalAbsent: Number(source['totalAbsent'] ?? source['TotalAbsent'] ?? 0),
    teacherComment: readString(source, 'teacherComments', 'TeacherComments', 'teacherComment', 'TeacherComment'),
    headComment: readString(source, 'schoolHeadComments', 'SchoolHeadComments', 'headComment', 'HeadComment'),
  };
}

function formatAffectiveScore(score: number): number | string {
  // Legacy: Score == 6 → ""; Score == 0 → "-"; else Score
  if (score === 6) {
    return '';
  }
  if (score === 0) {
    return '-';
  }
  return score;
}

function buildAffectiveRows(
  payload: ReportPayload,
  studentId: string,
  yearlyAndTermlyStatusId: string,
): { affective: AffectiveItemRow[]; psychomotor: AffectiveItemRow[] } {
  const normalizedStudentId = studentId.trim().toLowerCase();
  const normalizedYearlyId = yearlyAndTermlyStatusId.trim().toLowerCase();

  const forStudent = payload.affectiveAndPsychomotorAssessment.filter(item => {
    const sid = String(item.studentId ?? item.student?.id ?? '')
      .trim()
      .toLowerCase();
    return !!sid && sid === normalizedStudentId;
  });

  // Prefer same YearlyAndTermlyStatusId as the subject scores; fall back to all student rows.
  let rows = forStudent.filter(item => {
    if (!normalizedYearlyId) {
      return true;
    }
    const yid = String(
      item.yearlyAndTermlyAssessmentStatusId ?? item.yearlyAndTermlyAssessmentStatus?.id ?? '',
    )
      .trim()
      .toLowerCase();
    return !yid || yid === normalizedYearlyId;
  });

  if (rows.length === 0 && forStudent.length > 0) {
    rows = forStudent;
  }

  rows = [...rows].sort((left, right) => Number(left.itemId ?? 0) - Number(right.itemId ?? 0));

  const toItem = (item: AffectiveRecord): AffectiveItemRow => {
    const raw = (item.affectiveAndPsychomotor ?? {}) as Record<string, unknown>;
    return {
      itemName: readString(raw, 'itemsName', 'ItemsName', 'itemName', 'ItemName') || '-',
      score: formatAffectiveScore(Number(item.score ?? 0)),
    };
  };

  return {
    affective: rows.slice(0, 6).map(toItem),
    psychomotor: rows.slice(6).map(toItem),
  };
}

function resolveFormMasterForArm(
  payload: ReportPayload,
  classArmId: string,
): { name: string; signature: string } {
  const match = payload.affectiveAndPsychomotorAssessment.find(
    item => String((item as AffectiveRecord & { classArmId?: string }).classArmId ?? '') === classArmId,
  );
  const formMaster = (match?.formMaster ?? payload.armFormMaster ?? {}) as Record<string, unknown>;
  return {
    name: readString(formMaster, 'formMastersName', 'FormMastersName'),
    signature: readString(formMaster, 'signatureString', 'SignatureString'),
  };
}

function resolveNextTermBegins(rows: AssessmentRecord[]): string {
  const ordered = [...rows].sort((a, b) => yearlyStatusId(a).localeCompare(yearlyStatusId(b)));
  const last = ordered[ordered.length - 1];
  const termDate = last?.yearlyAndTermlyStatus?.nextTermBeginsDate;
  if (!termDate) {
    return 'No set date yet';
  }
  const parsed = new Date(String(termDate));
  if (Number.isNaN(parsed.getTime())) {
    return String(termDate);
  }
  // Legacy: ToLongDateString()
  return parsed.toLocaleDateString(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
}

/**
 * Strict port of the Razor flow in PerArmAndThirdTermCumulativeResultProcessing.cshtml.
 * DTO payload shape is unchanged; all presentation math stays on the client as in the legacy view.
 */
export function buildReportSheets(payload: ReportPayload, context: ReportBuildContext): ReportSheetViewModel[] {
  const casLayout = resolveCasLayout(payload.casSetting);
  const allRows = payload.studentsAccessment;
  if (allRows.length === 0) {
    return [];
  }

  const attendanceByStudent = new Map<string, AttendanceRecord>();
  for (const item of payload.studentsAttendance) {
    const normalized = normalizeAttendance(item);
    if (normalized.studentId) {
      attendanceByStudent.set(normalized.studentId, normalized);
    }
  }

  const schoolHeadRaw = (payload.schoolHeadInfo ?? {}) as Record<string, unknown>;
  const schoolHeadCategoryId = Number(
    schoolHeadRaw['schoolHeadCategoryId'] ?? schoolHeadRaw['SchoolHeadCategoryId'] ?? 0,
  );
  const schoolHeadName = readString(schoolHeadRaw, 'headsName', 'HeadsName');
  const schoolHeadTitle = resolveSchoolHeadTitle(
    schoolHeadCategoryId,
    (schoolHeadRaw['gender'] ?? schoolHeadRaw['Gender']) as string | number | null | undefined,
  );
  const schoolHeadSignature = readString(schoolHeadRaw, 'signatureImageString', 'SignatureImageString');

  const armGroups = groupBy(allRows, row => String(row.classArmId ?? ''));
  const sheets: ReportSheetViewModel[] = [];

  for (const [classArmId, armRows] of armGroups.entries()) {
    const formMaster = resolveFormMasterForArm(payload, classArmId);

    // Legacy: GroupBy StudentId, OrderByDescending Sum/Count (all assessment rows)
    const studentGroups = [...groupBy(armRows, row => String(row.studentId ?? row.student?.id ?? '')).entries()]
      .map(([studentId, rows]) => ({ studentId, rows, average: rowAverage(rows) }))
      .sort((left, right) => right.average - left.average);

    const classHighestAverage = studentGroups.length ? round1(Math.max(...studentGroups.map(s => s.average))) : 0;
    const classLowestAverage = studentGroups.length ? round1(Math.min(...studentGroups.map(s => s.average))) : 0;
    const studentsInClass = studentGroups.length;
    const armStudentRowGroups = studentGroups.map(s => s.rows);

    let classPositionCounter = 0;
    let lastOverallAverageScore = 0;
    let difference = 0;

    for (const entry of studentGroups) {
      const admissionNo = String(entry.rows[0]?.student?.idNo ?? entry.studentId).trim();
      const matchesFilter =
        !context.filterStudentId ||
        admissionNo.toLowerCase() === context.filterStudentId.toLowerCase() ||
        entry.studentId.toLowerCase() === context.filterStudentId.toLowerCase();

      const lastClassPosition = classPositionCounter;
      classPositionCounter = classPositionCounter + 1;

      const thirdTerm = isThirdTermStudentRows(entry.rows, context.termStatus);
      const distinctSubjectCount = groupBy(entry.rows, r => String(r.subjectId ?? '')).size;
      const fontSize = resolveLegacyFontSize(distinctSubjectCount);

      // Legacy overall totals always use all assessment rows (before third-term subject display override of count)
      const overallTotalScore = entry.rows.reduce((sum, row) => sum + assessmentTotal(row), 0);
      const noOfSubjectOfferedForAverage = entry.rows.length || 1;
      let overallAverageScore = overallTotalScore / noOfSubjectOfferedForAverage;

      // Legacy class-position bracketing (lines 343–377) — run for every arm student so
      // individual sheets keep the correct class position when filterStudentId is set.
      if (lastOverallAverageScore === overallAverageScore && overallAverageScore > 0) {
        classPositionCounter = lastClassPosition;
        difference = difference + 1;
      } else {
        if (difference === 1) {
          classPositionCounter = classPositionCounter + 1;
          difference = 0;
        } else if (difference === 2) {
          classPositionCounter = classPositionCounter + 2;
          difference = 0;
        } else if (difference === 3) {
          classPositionCounter = classPositionCounter + 3;
          difference = 0;
        } else if (difference === 4) {
          classPositionCounter = classPositionCounter + 4;
          difference = 0;
        } else if (difference === 5) {
          classPositionCounter = classPositionCounter + 5;
          difference = 0;
        }
      }
      lastOverallAverageScore = overallAverageScore;

      if (!matchesFilter) {
        continue;
      }

      const subjects = thirdTerm
        ? buildSubjectRowsThirdTerm(entry.rows, armStudentRowGroups)
        : buildSubjectRowsNonThird(entry.rows, armStudentRowGroups);

      // Legend "Number of Subject Offered": third term uses distinct subjects; else row count
      const subjectCount = thirdTerm ? distinctSubjectCount : entry.rows.length;

      const finalGrade = getGrade(overallAverageScore).grade;
      const defaults = defaultComments(finalGrade);
      const attendance = attendanceByStudent.get(entry.studentId);

      const lastStatusRow = pickLatestTermRow(entry.rows);
      const yearlyId = yearlyStatusId(lastStatusRow ?? entry.rows[0]);
      const attendanceTotal =
        payload.totalAttendance ??
        lastStatusRow?.yearlyAndTermlyStatus?.totalAttendance ??
        null;

      let studentAttendance = Number(attendance?.totalPresent ?? 0);
      let studentAbsent = 0;
      let teacherComments = attendance?.teacherComment ?? '';
      let schoolHeadComments = attendance?.headComment ?? '';

      if (attendance) {
        studentAbsent = attendanceTotal == null ? 0 : Number(attendanceTotal) - studentAttendance;
      } else {
        const absentValue =
          attendanceTotal == null ? 0 - studentAttendance : Number(attendanceTotal) - studentAttendance;
        studentAbsent = Number(absentValue);
      }

      if (!String(teacherComments).trim()) {
        teacherComments = defaults.teacher;
      }
      if (!String(schoolHeadComments).trim()) {
        schoolHeadComments = defaults.head;
      }

      const affectiveBundle = buildAffectiveRows(payload, entry.studentId, yearlyId);

      const sessionFromRow = lastStatusRow?.yearlyAndTermlyStatus?.session?.session;
      const academicSession = context.sessionLabel || String(sessionFromRow ?? '').trim() || 'Nil';
      const termRaw = termStatusOf(lastStatusRow);
      const resolvedTermLabel =
        context.termLabel ||
        (termRaw === null || termRaw === undefined ? 'First' : termLabel(termRaw));

      const daysOpen =
        attendanceTotal === 0 || attendanceTotal == null ? '__' : Number(attendanceTotal);
      const daysPresent = studentAttendance === 0 ? '__' : studentAttendance;
      const daysAbsent =
        studentAbsent === 0 || studentAbsent === attendanceTotal ? '__' : studentAbsent;

      sheets.push({
        studentId: entry.studentId,
        studentName: resolveStudentName(entry.rows[0]) || '-',
        admissionNo: admissionNo || entry.studentId,
        classLabel: String(entry.rows[0]?.classArm?.classs?.classLabel ?? '').trim(),
        armLabel: resolveArmLabel(entry.rows[0]),
        passportUrl: resolvePassportUrl(
          context.apiOrigin,
          entry.rows[0]?.student?.passport,
          context.mediaCacheBust,
        ),
        schoolName: String(context.schoolInfo?.schoolName ?? '').trim(),
        schoolAddress: String(context.schoolInfo?.schoolAddress ?? '').trim(),
        schoolMotto: String(context.schoolInfo?.motto ?? '').trim(),
        schoolLogoUrl: context.logoUrl,
        academicSession,
        termLabel: resolvedTermLabel,
        reportTitle: `REPORT SHEET FOR ${resolvedTermLabel.toUpperCase()} TERM ${academicSession} ACADEMIC SESSION`,
        fontSize,
        layoutDensity: 'legacy',
        layoutVars: {
          '--report-font-size': fontSize,
        },
        colSpan: casLayout.colSpan,
        casLayout,
        subjects,
        subjectCount,
        totalScore: round1(overallTotalScore),
        finalAverage: round1(overallAverageScore),
        finalGrade,
        classPosition: formatClassPosition(classPositionCounter),
        classHighestAverage,
        classLowestAverage,
        studentsInClass,
        daysOpen,
        daysPresent,
        daysAbsent,
        affectiveItems: affectiveBundle.affective,
        psychomotorItems: affectiveBundle.psychomotor,
        teacherComment: teacherComments || 'No Comment',
        headComment: schoolHeadComments || 'No Comment',
        formMasterName: formMaster.name,
        formMasterSignatureUrl: resolveSignatureUrl(
          context.apiOrigin,
          formMaster.signature,
          context.mediaCacheBust,
        ),
        schoolHeadName,
        schoolHeadTitle,
        schoolHeadSignatureUrl: resolveSignatureUrl(
          context.apiOrigin,
          schoolHeadSignature,
          context.mediaCacheBust,
        ),
        nextTermBegins: resolveNextTermBegins(entry.rows),
      });
    }
  }

  return sheets;
}
