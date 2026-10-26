import { ArmName } from '@shared/interfaces';
import { CurrentClass } from '@shared/services/generic.service';

export type ResultTypeMode = '0' | '1';
export type ResultCategoryMode = '0' | '1';
export type CollectionScopeMode = '0' | '1' | '2';
export type ThirdTermResultMode = '0' | '1';

export interface SelectOption {
  text: string;
  value: string;
}

/** Picks the option with the highest numeric value (e.g. Third = 2), matching legacy MVC dropdowns. */
export function selectLatestSelectOption(options: SelectOption[]): SelectOption | undefined {
  const valid = options.filter(option => option.value !== '-1' && option.value !== '');
  if (valid.length === 0) {
    return undefined;
  }

  return valid.reduce((latest, option) => {
    const latestNum = Number(latest.value);
    const optionNum = Number(option.value);
    if (Number.isFinite(optionNum) && Number.isFinite(latestNum)) {
      return optionNum >= latestNum ? option : latest;
    }
    return option;
  });
}

/** Parse the starting year from labels like "2025/2026". */
export function parseSessionStartYear(text: string): number {
  const match = String(text ?? '').trim().match(/^(\d{4})/);
  return match ? Number(match[1]) : Number.NaN;
}

/** Order sessions most-recent first (2025/2026 before 2024/2025), keeping placeholders first. */
export function sortSessionOptionsDescending(options: SelectOption[]): SelectOption[] {
  const placeholders = options.filter(option => option.value === '-1');
  const valid = options.filter(option => option.value !== '-1');
  const sorted = [...valid].sort((left, right) => {
    const leftYear = parseSessionStartYear(left.text);
    const rightYear = parseSessionStartYear(right.text);
    if (Number.isFinite(leftYear) && Number.isFinite(rightYear) && leftYear !== rightYear) {
      return rightYear - leftYear;
    }

    return right.text.localeCompare(left.text);
  });

  return placeholders.length > 0 ? [...placeholders, ...sorted] : sorted;
}

export interface ClassOption extends SelectOption {
  classId: string;
  classNameEnum: number;
}

export interface CasSettingRecord {
  casCategory?: string | number | null;
  perCasValue?: number | null;
}

export interface AssessmentRecord {
  id?: string;
  studentId?: string;
  subjectId?: string;
  classArmId?: string;
  yearlyAndTermlyStatusId?: string | number | null;
  ca1?: number;
  ca2?: number;
  ca3?: number;
  exams?: number;
  subject?: { subjectName?: string | null; id?: string | null };
  student?: {
    id?: string | null;
    fullName?: string | null;
    firstName?: string | null;
    surName?: string | null;
    idNo?: string | null;
    passport?: string | null;
    user?: { fullName?: string | null; firstName?: string | null; surName?: string | null };
  };
  classArm?: {
    armName?: string | number | null;
    classs?: { classLabel?: string | null; className?: string | number | null };
  };
  yearlyAndTermlyStatus?: {
    id?: string | number | null;
    termStatus?: number | null;
    casCategory?: string | number | null;
    perCasValue?: number | null;
    session?: { session?: string | null };
    totalAttendance?: number | null;
    nextTermBeginsDate?: string | null;
  };
}

export interface AttendanceRecord {
  totalPresent?: number;
  totalAbsent?: number;
  teacherComment?: string | null;
  headComment?: string | null;
  studentId?: string | null;
}

export interface AffectiveRecord {
  studentId?: string | null;
  classArmId?: string | null;
  yearlyAndTermlyAssessmentStatusId?: string | number | null;
  student?: {
    fullName?: string | null;
    id?: string | null;
    currentClassLabel?: string | null;
    armName?: string | number | null;
  };
  affectiveAndPsychomotor?: Record<string, unknown>;
  score?: number | null;
  itemId?: string | number | null;
  formMaster?: {
    formMastersName?: string | null;
    signatureString?: string | null;
  };
  yearlyAndTermlyAssessmentStatus?: {
    id?: string | number | null;
    nextTermBeginsDate?: string | null;
    totalAttendance?: number | null;
    session?: { session?: string | null };
    termStatus?: number | null;
  };
}

export interface SchoolHeadInfoRecord {
  headsName?: string | null;
  signatureImageString?: string | null;
  schoolHeadCategoryId?: number | null;
  gender?: number | string | null;
}

export interface SchoolInfoRecord {
  schoolName?: string | null;
  schoolAddress?: string | null;
  motto?: string | null;
  schoolLogoString?: string | null;
}

export function normalizeSchoolInfo(raw: unknown): SchoolInfoRecord | null {
  if (!raw || typeof raw !== 'object') {
    return null;
  }

  const item = raw as Record<string, unknown>;
  const schoolName = String(item['schoolName'] ?? item['SchoolName'] ?? '').trim();
  const schoolAddress = String(item['schoolAddress'] ?? item['SchoolAddress'] ?? '').trim();
  const motto = String(item['motto'] ?? item['Motto'] ?? '').trim();
  const schoolLogoString = String(item['schoolLogoString'] ?? item['SchoolLogoString'] ?? '').trim();

  if (!schoolName && !schoolAddress && !motto && !schoolLogoString) {
    return null;
  }

  return { schoolName, schoolAddress, motto, schoolLogoString };
}

export interface ReportPayload {
  studentsAccessment: AssessmentRecord[];
  studentsAttendance: AttendanceRecord[];
  affectiveAndPsychomotorAssessment: AffectiveRecord[];
  casSetting?: CasSettingRecord | null;
  schoolHeadInfo?: SchoolHeadInfoRecord | null;
  schoolInfo?: SchoolInfoRecord | null;
  armFormMaster?: { formMastersName?: string | null; signatureString?: string | null } | null;
  totalAttendance?: number | null;
  statusCode?: number | null;
  message?: string | null;
}

export interface StudentReportGroup {
  studentId: string;
  studentName: string;
  admissionNo: string;
  classLabel: string;
  assessments: AssessmentRecord[];
  attendance?: AttendanceRecord;
  affective?: AffectiveRecord;
}

export function asArray<T>(value: unknown): T[] {
  if (Array.isArray(value)) {
    return value as T[];
  }

  const wrapped = value as { data?: unknown } | null;
  if (Array.isArray(wrapped?.data)) {
    return wrapped.data as T[];
  }

  return [];
}

export function normalizeSelectOption(item: unknown): SelectOption {
  const raw = (item ?? {}) as Record<string, unknown>;
  return {
    value: String(raw['value'] ?? raw['Value'] ?? '').trim(),
    text: String(raw['text'] ?? raw['Text'] ?? '').trim(),
  };
}

export function normalizeSelectOptions(response: unknown): SelectOption[] {
  return asArray<unknown>(response)
    .map(normalizeSelectOption)
    .filter(option => option.value && option.text);
}

function unwrapSessionsPayload(response: unknown): unknown {
  if (!response || typeof response !== 'object' || Array.isArray(response)) {
    return response;
  }

  const wrapped = response as Record<string, unknown>;
  return wrapped['sessions'] ?? wrapped['Sessions'] ?? wrapped['data'] ?? wrapped['Data'] ?? response;
}

export function normalizeSessions(response: unknown): SelectOption[] {
  const rows = asArray<Record<string, unknown>>(unwrapSessionsPayload(response));
  const options = rows
    .map(row => ({
      text: String(
        row['text'] ??
        row['Text'] ??
        row['session'] ??
        row['Session'] ??
        ''
      ).trim(),
      value: String(
        row['value'] ??
        row['Value'] ??
        row['id'] ??
        row['Id'] ??
        ''
      ).trim(),
    }))
    .filter(option => option.value && option.text);

  if (options.length === 1) {
    const only = options[0];
    return [
      { text: 'Select', value: '-1' },
      only,
    ];
  }

  return options;
}

function resolveTermStatusValue(row: Record<string, unknown>): string {
  const raw = row['value'] ?? row['Value'] ?? row['termStatus'] ?? row['TermStatus'] ?? row['term'] ?? row['Term'];
  if (raw === null || raw === undefined || raw === '') {
    return '';
  }

  if (typeof raw === 'string') {
    const normalized = raw.trim().toLowerCase();
    if (normalized === 'first') return '0';
    if (normalized === 'second') return '1';
    if (normalized === 'third') return '2';
    return raw.trim();
  }

  return String(raw).trim();
}

function resolveTermStatusLabel(row: Record<string, unknown>, value: string): string {
  const explicit = String(row['text'] ?? row['Text'] ?? '').trim();
  if (explicit) {
    return explicit;
  }

  const numeric = Number(value);
  return termLabel(Number.isFinite(numeric) ? numeric : 0);
}

export function normalizeTerms(response: unknown): SelectOption[] {
  const rows = asArray<Record<string, unknown>>(response);
  const options = rows
    .map(row => {
      const value = resolveTermStatusValue(row);
      return {
        text: resolveTermStatusLabel(row, value),
        value,
      };
    })
    .filter(option => option.value !== '');

  if (options.length === 1) {
    const only = options[0];
    return [
      { text: 'Select', value: '-1' },
      only,
    ];
  }

  return options;
}

export function normalizeClasses(response: unknown): ClassOption[] {
  return asArray<Record<string, unknown>>(response)
    .map(row => {
      const classNameEnum = Number(row['className'] ?? row['ClassName'] ?? 0);
      const classLabel = String(row['classLabel'] ?? row['ClassLabel'] ?? '').trim();
      const classId = String(row['id'] ?? row['Id'] ?? '').trim();
      return {
        classId,
        classNameEnum,
        text: classLabel || String(CurrentClass[classNameEnum] ?? classNameEnum),
        value: `${classNameEnum},${classLabel}`,
      };
    })
    .sort((left, right) => left.classNameEnum - right.classNameEnum);
}

export function normalizeArms(response: unknown): SelectOption[] {
  const rows = asArray<Record<string, unknown>>(response);
  const options = rows.map(row => {
    const armName = row['armName'] ?? row['ArmName'] ?? '';
    const numeric = Number(armName);
    const text = Number.isInteger(numeric) && ArmName[numeric] !== undefined ? ArmName[numeric] : String(armName);
    return {
      value: String(Number.isInteger(numeric) ? numeric : armName),
      text,
    };
  });

  if (options.length === 1) {
    const only = options[0];
    return [
      { text: 'Select', value: '-1' },
      only,
    ];
  }

  return options;
}

export function termLabel(termStatus: number): string {
  switch (termStatus) {
    case 1:
      return 'Second';
    case 2:
      return 'Third';
    default:
      return 'First';
  }
}

export function parseClassEnum(classValue: string): string {
  return String(classValue.split(',')[0] ?? '').trim();
}

export function readAssessmentRows(source: Record<string, unknown> | null | undefined): AssessmentRecord[] {
  const nested = source?.['generalResultProcessingsToReportSheetXtractionForFormTeacherCasSetting'] as
    | Record<string, unknown>
    | undefined;

  return asArray<AssessmentRecord>(
    source?.['studentsAccessment'] ??
      source?.['StudentsAccessment'] ??
      nested?.['studentsAccessment'] ??
      nested?.['StudentsAccessment'],
  );
}

function readAffectiveRows(source: Record<string, unknown>, nested?: Record<string, unknown>): AffectiveRecord[] {
  return asArray<unknown>(
    nested?.['affectiveAndPsychomotorAssessment'] ??
      nested?.['AffectiveAndPsychomotorAssessment'] ??
      source['affectiveAndPsychomotorAssessment'] ??
      source['AffectiveAndPsychomotorAssessment'],
  ).map(normalizeAffectiveRecord);
}

export function normalizeAffectiveRecord(raw: unknown): AffectiveRecord {
  const item = (raw ?? {}) as Record<string, unknown>;
  const studentRaw = (item['student'] ?? item['Student'] ?? {}) as Record<string, unknown>;
  const studentId = String(
    item['studentId'] ?? item['StudentId'] ?? studentRaw['id'] ?? studentRaw['Id'] ?? '',
  ).trim();
  const yearlyStatusRaw = (item['yearlyAndTermlyAssessmentStatus'] ??
    item['YearlyAndTermlyAssessmentStatus'] ??
    {}) as Record<string, unknown>;
  const yearlyAndTermlyAssessmentStatusId = String(
    item['yearlyAndTermlyAssessmentStatusId'] ??
      item['YearlyAndTermlyAssessmentStatusId'] ??
      yearlyStatusRaw['id'] ??
      yearlyStatusRaw['Id'] ??
      '',
  ).trim();

  return {
    studentId,
    classArmId: String(item['classArmId'] ?? item['ClassArmId'] ?? '').trim() || null,
    yearlyAndTermlyAssessmentStatusId: yearlyAndTermlyAssessmentStatusId || null,
    student: {
      id: studentId || String(studentRaw['id'] ?? studentRaw['Id'] ?? '').trim() || undefined,
      fullName: String(studentRaw['fullName'] ?? studentRaw['FullName'] ?? '').trim() || undefined,
      currentClassLabel:
        String(studentRaw['currentClassLabel'] ?? studentRaw['CurrentClassLabel'] ?? '').trim() || undefined,
      armName: (studentRaw['armName'] ?? studentRaw['ArmName']) as string | number | null | undefined,
    },
    affectiveAndPsychomotor: ((item['affectiveAndPsychomotor'] ??
      item['AffectiveAndPsychomotor']) ??
      undefined) as Record<string, unknown> | undefined,
    score: Number(item['score'] ?? item['Score'] ?? 0),
    itemId: Number(item['itemId'] ?? item['ItemId'] ?? 0),
    formMaster: (item['formMaster'] ?? item['FormMaster'] ?? undefined) as AffectiveRecord['formMaster'],
    yearlyAndTermlyAssessmentStatus: Object.keys(yearlyStatusRaw).length
      ? {
          id: yearlyAndTermlyAssessmentStatusId || undefined,
          nextTermBeginsDate: (yearlyStatusRaw['nextTermBeginsDate'] ??
            yearlyStatusRaw['NextTermBeginsDate']) as string | null | undefined,
          totalAttendance: (yearlyStatusRaw['totalAttendance'] ??
            yearlyStatusRaw['TotalAttendance']) as number | null | undefined,
          session: (yearlyStatusRaw['session'] ?? yearlyStatusRaw['Session']) as
            | { session?: string | null }
            | undefined,
          termStatus: (yearlyStatusRaw['termStatus'] ?? yearlyStatusRaw['TermStatus']) as
            | number
            | null
            | undefined,
        }
      : undefined,
  };
}

export function unwrapReportPayload(response: Record<string, unknown> | null | undefined): ReportPayload {
  if (!response) {
    return emptyReportPayload('No response received.');
  }

  // Some callers wrap the DTO as { data: ... } / { body: ... }.
  const unwrapped =
    response['data'] && typeof response['data'] === 'object' && !Array.isArray(response['data'])
      ? (response['data'] as Record<string, unknown>)
      : response['body'] && typeof response['body'] === 'object' && !Array.isArray(response['body'])
        ? (response['body'] as Record<string, unknown>)
        : response;

  const nested = (unwrapped['generalResultProcessingsToReportSheetXtractionForFormTeacherCasSetting'] ??
    unwrapped['GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting']) as
    | Record<string, unknown>
    | undefined;

  const statusCode = Number(
    unwrapped['statusCode'] ??
      unwrapped['StatusCode'] ??
      nested?.['statusCode'] ??
      nested?.['StatusCode'] ??
      NaN,
  );

  const message =
    String(
      unwrapped['errorMsg'] ??
        unwrapped['ErrorMsg'] ??
        unwrapped['message'] ??
        unwrapped['Message'] ??
        '',
    ).trim() || null;

  return {
    studentsAccessment: readAssessmentRows(unwrapped),
    studentsAttendance: asArray<AttendanceRecord>(
      nested?.['studentsAttendance'] ??
        nested?.['StudentsAttendance'] ??
        unwrapped['studentsAttendance'] ??
        unwrapped['StudentsAttendance'],
    ),
    affectiveAndPsychomotorAssessment: readAffectiveRows(unwrapped, nested),
    // The API serialises this key as camelCase `caSetting`; without it every sheet fell back to two CAs.
    casSetting: (nested?.['casSetting'] ??
      nested?.['CasSetting'] ??
      nested?.['caSetting'] ??
      unwrapped['casSetting'] ??
      unwrapped['caSetting'] ??
      unwrapped['CaSetting'] ??
      unwrapped['CasSetting'] ??
      null) as CasSettingRecord | null,
    schoolHeadInfo: (nested?.['schoolHeadInfo'] ??
      nested?.['SchoolHeadInfo'] ??
      unwrapped['schoolHeadInfo'] ??
      unwrapped['SchoolHeadInfo'] ??
      unwrapped['schoolHead'] ??
      unwrapped['SchoolHead'] ??
      null) as ReportPayload['schoolHeadInfo'],
    schoolInfo: normalizeSchoolInfo(
      nested?.['schoolInfo'] ??
        nested?.['SchoolInfo'] ??
        unwrapped['schoolInfo'] ??
        unwrapped['SchoolInfo'],
    ),
    armFormMaster: (nested?.['armFormMaster'] ??
      nested?.['ArmFormMaster'] ??
      unwrapped['armFormMaster'] ??
      unwrapped['ArmFormMaster'] ??
      null) as ReportPayload['armFormMaster'],
    totalAttendance: Number(
      nested?.['totalAttendance'] ??
        nested?.['TotalAttendance'] ??
        unwrapped['totalAttendance'] ??
        unwrapped['TotalAttendance'] ??
        NaN,
    ) || null,
    statusCode: Number.isFinite(statusCode) ? statusCode : null,
    message,
  };
}

export function resolveStatusMessage(statusCode: number | null | undefined): string | null {
  switch (statusCode) {
    case 0:
      return "Failed! Either wrong Student's Admission number or no result is available for the selected SESSION and TERM.";
    case 1:
      return 'Failed! Environment has not been prepared for result processing yet; contact Registration Officer.';
    case 2:
      return "Failed; because Affective and Psychomotor has not been entered for any Student in that 'class arm' during the selected session and term.";
    default:
      return null;
  }
}

export function groupReports(payload: ReportPayload): StudentReportGroup[] {
  const groups = new Map<string, StudentReportGroup>();

  for (const row of payload.studentsAccessment) {
    const studentId = String(
      row.studentId ?? row.student?.id ?? row.student?.idNo ?? row.id ?? '',
    ).trim();
    const key = studentId || `row-${groups.size}`;
    const existing = groups.get(key);
    const studentName =
      row.student?.fullName ??
      row.student?.user?.fullName ??
      [row.student?.user?.firstName ?? row.student?.firstName, row.student?.user?.surName ?? row.student?.surName]
        .filter(Boolean)
        .join(' ');

    if (existing) {
      existing.assessments.push(row);
      continue;
    }

    groups.set(key, {
      studentId: key,
      studentName: String(studentName ?? '').trim() || '-',
      admissionNo: String(row.student?.idNo ?? '').trim() || key,
      classLabel: String(row.classArm?.classs?.classLabel ?? '').trim(),
      assessments: [row],
    });
  }

  for (const attendance of payload.studentsAttendance) {
    const key = String(attendance.studentId ?? '').trim();
    if (!key) {
      continue;
    }
    const group = groups.get(key);
    if (group) {
      group.attendance = attendance;
    }
  }

  for (const affective of payload.affectiveAndPsychomotorAssessment) {
    const key = String(affective.student?.id ?? '').trim();
    if (!key) {
      continue;
    }
    const group = groups.get(key);
    if (group) {
      group.affective = affective;
    }
  }

  return [...groups.values()];
}

export function assessmentTotal(row: AssessmentRecord): number {
  return Number(row.ca1 ?? 0) + Number(row.ca2 ?? 0) + Number(row.ca3 ?? 0) + Number(row.exams ?? 0);
}

export function casColumns(casSetting?: CasSettingRecord | null): Array<'ca1' | 'ca2' | 'ca3'> {
  const category = String(casSetting?.casCategory ?? '').toLowerCase();
  if (category === 'oneca' || category === '0') {
    return ['ca1'];
  }
  if (category === 'twocas' || category === '1') {
    return ['ca1', 'ca2'];
  }
  return ['ca1', 'ca2', 'ca3'];
}

function emptyReportPayload(message: string): ReportPayload {
  return {
    studentsAccessment: [],
    studentsAttendance: [],
    affectiveAndPsychomotorAssessment: [],
    message,
  };
}
