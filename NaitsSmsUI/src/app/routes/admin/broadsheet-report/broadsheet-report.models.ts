import {
  ClassOption,
  SelectOption,
  asArray,
  normalizeArms,
  normalizeClasses,
  normalizeSessions,
  normalizeTerms,
  parseClassEnum,
} from '../student-results/student-results.models';

export type BroadSheetCollectionMode = '1' | '2';

export interface BroadSheetSubjectScore {
  displayValue: string;
  isFail: boolean;
}

export interface BroadSheetStudentRow {
  serialNumber: number;
  admissionNo: string;
  fullName: string;
  scores: BroadSheetSubjectScore[];
  subjectCount: number;
  total: number;
  average: number;
  position: string;
}

export interface BroadSheetArmSection {
  armLabel: string;
  subjects: string[];
  studentCount: number;
  classHighestAverage: number;
  classLowestAverage: number;
  subjectAverageScore: number;
  students: BroadSheetStudentRow[];
}

export interface BroadSheetSchoolInfo {
  schoolName: string;
  schoolAddress: string;
  motto: string;
  schoolLogoString: string;
}

export interface BroadSheetReport {
  message: string;
  schoolInfo: BroadSheetSchoolInfo | null;
  sessionLabel: string;
  termLabel: string;
  classLabel: string;
  arms: BroadSheetArmSection[];
}

function readString(record: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = record[key];
    if (value !== undefined && value !== null && String(value).trim()) {
      return String(value).trim();
    }
  }
  return '';
}

function readNumber(record: Record<string, unknown>, ...keys: string[]): number {
  for (const key of keys) {
    const value = record[key];
    if (value !== undefined && value !== null && value !== '') {
      const parsed = Number(value);
      if (Number.isFinite(parsed)) {
        return parsed;
      }
    }
  }
  return 0;
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' ? value as Record<string, unknown> : null;
}

function normalizeSubjectScore(value: unknown): BroadSheetSubjectScore {
  const record = asRecord(value) ?? {};
  return {
    displayValue: readString(record, 'displayValue', 'DisplayValue') || '-',
    isFail: Boolean(record['isFail'] ?? record['IsFail']),
  };
}

function normalizeStudentRow(value: unknown): BroadSheetStudentRow | null {
  const record = asRecord(value);
  if (!record) {
    return null;
  }

  const rawScores = record['scores'] ?? record['Scores'];
  const scores = Array.isArray(rawScores)
    ? rawScores.map(normalizeSubjectScore)
    : [];

  return {
    serialNumber: readNumber(record, 'serialNumber', 'SerialNumber'),
    admissionNo: readString(record, 'admissionNo', 'AdmissionNo'),
    fullName: readString(record, 'fullName', 'FullName'),
    scores,
    subjectCount: readNumber(record, 'subjectCount', 'SubjectCount'),
    total: readNumber(record, 'total', 'Total'),
    average: readNumber(record, 'average', 'Average'),
    position: readString(record, 'position', 'Position'),
  };
}

function normalizeArmSection(value: unknown): BroadSheetArmSection | null {
  const record = asRecord(value);
  if (!record) {
    return null;
  }

  const rawSubjects = record['subjects'] ?? record['Subjects'];
  const subjects = Array.isArray(rawSubjects)
    ? rawSubjects.map(item => String(item ?? '').trim()).filter(Boolean)
    : [];

  const rawStudents = record['students'] ?? record['Students'];
  const students = Array.isArray(rawStudents)
    ? rawStudents
      .map(normalizeStudentRow)
      .filter((item): item is BroadSheetStudentRow => item !== null)
    : [];

  return {
    armLabel: readString(record, 'armLabel', 'ArmLabel'),
    subjects,
    studentCount: readNumber(record, 'studentCount', 'StudentCount'),
    classHighestAverage: readNumber(record, 'classHighestAverage', 'ClassHighestAverage'),
    classLowestAverage: readNumber(record, 'classLowestAverage', 'ClassLowestAverage'),
    subjectAverageScore: readNumber(record, 'subjectAverageScore', 'SubjectAverageScore'),
    students,
  };
}

export function normalizeBroadSheetReport(response: unknown): BroadSheetReport {
  const record = asRecord(response) ?? {};
  const schoolRaw = asRecord(record['schoolInfo'] ?? record['SchoolInfo']);
  const rawArms = record['arms'] ?? record['Arms'];
  const arms = Array.isArray(rawArms)
    ? rawArms
      .map(normalizeArmSection)
      .filter((item): item is BroadSheetArmSection => item !== null)
    : [];

  return {
    message: readString(record, 'message', 'Message'),
    sessionLabel: readString(record, 'sessionLabel', 'SessionLabel'),
    termLabel: readString(record, 'termLabel', 'TermLabel'),
    classLabel: readString(record, 'classLabel', 'ClassLabel'),
    schoolInfo: schoolRaw
      ? {
        schoolName: readString(schoolRaw, 'schoolName', 'SchoolName'),
        schoolAddress: readString(schoolRaw, 'schoolAddress', 'SchoolAddress'),
        motto: readString(schoolRaw, 'motto', 'Motto'),
        schoolLogoString: readString(schoolRaw, 'schoolLogoString', 'SchoolLogoString'),
      }
      : null,
    arms,
  };
}

export function normalizeSelectListItems(response: unknown): SelectOption[] {
  const rows = asArray<Record<string, unknown>>(response);
  const options = rows
    .map(row => ({
      text: String(row['text'] ?? row['Text'] ?? '').trim(),
      value: String(row['value'] ?? row['Value'] ?? '').trim(),
    }))
    // The API may prepend a legacy "Select" (-1) row; the template already renders its own placeholder.
    .filter(option => option.value && option.text && option.value !== '-1');

  return options;
}

export {
  ClassOption,
  SelectOption,
  normalizeArms,
  normalizeClasses,
  normalizeSessions,
  normalizeTerms,
  parseClassEnum,
};

export function hasBroadSheetContent(report: BroadSheetReport | null): boolean {
  return !!report && report.arms.some(arm => arm.students.length > 0);
}
