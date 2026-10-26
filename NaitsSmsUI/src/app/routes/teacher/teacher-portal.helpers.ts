import { ArmName } from '@shared/interfaces';

import {
  CasSetting,
  StudentDetails,
  StudentsAccessment,
  StudentsAccessmentCasSetting,
  TeacherClassArmAssignment,
  TeacherSubjectArmAssignment,
} from './teacher-portal.models';

export type CasCategoryName = 'OneCA' | 'TwoCAs' | 'ThreeCAs';

export function readSubjectId(item: TeacherSubjectArmAssignment): string {
  return String(item.subjectId ?? item.SubjectId ?? item.subject?.id ?? item.Subject?.id ?? '').trim();
}

export function readArmId(item: TeacherSubjectArmAssignment): string {
  return String(item.armId ?? item.ArmId ?? item.arm?.id ?? item.Arm?.id ?? '').trim();
}

export function readSubjectName(item: TeacherSubjectArmAssignment): string {
  return String(item.subject?.subjectName ?? item.Subject?.SubjectName ?? item.subject?.SubjectName ?? '').trim();
}

export function formatArmName(value: unknown): string {
  if (value === undefined || value === null || value === '') {
    return '';
  }

  if (typeof value === 'number' || (typeof value === 'string' && /^\d+$/.test(value.trim()))) {
    const numericValue = Number(value);
    if (Number.isFinite(numericValue) && ArmName[numericValue] !== undefined) {
      const label = ArmName[numericValue];
      return label === 'NoArm' ? '' : label;
    }
  }

  const text = String(value).trim();
  return text === 'NoArm' ? '' : text;
}

export function readClassArmLabel(item: TeacherSubjectArmAssignment | TeacherClassArmAssignment): string {
  const preset = String(item.classArmLabel ?? item.ClassArmLabel ?? '').trim();
  if (preset) {
    return preset;
  }

  const arm = item.arm ?? item.Arm;
  const classLabel = String(arm?.classs?.classLabel ?? arm?.Classs?.ClassLabel ?? '').trim();
  const armName = formatArmName(arm?.armName ?? arm?.ArmName);
  return `${classLabel} ${armName}`.trim();
}

export function readFormMasterFullName(item: TeacherClassArmAssignment): string {
  const user = item.employee?.user ?? item.Employee?.User;
  const explicit = user?.fullName ?? user?.FullName;
  if (explicit) {
    return String(explicit).trim();
  }

  return [user?.firstName ?? user?.FirstName, user?.middleName ?? user?.MiddleName, user?.surName ?? user?.SurName]
    .map(part => String(part ?? '').trim())
    .filter(Boolean)
    .join(' ');
}

export function readClassNameEnum(item: TeacherClassArmAssignment): string {
  const arm = item.arm ?? item.Arm;
  const value = arm?.classs?.className ?? arm?.Classs?.ClassName;
  if (typeof value === 'number') {
    // Keep numeric enum name if available via ArmName-style reverse map is not used for class;
    // API/JSON typically already serializes as string enum names.
    return String(value);
  }
  return String(value ?? '').trim();
}

export function readArmNameEnum(item: TeacherClassArmAssignment): string {
  const arm = item.arm ?? item.Arm;
  const value = arm?.armName ?? arm?.ArmName;
  if (typeof value === 'number' || (typeof value === 'string' && /^\d+$/.test(value.trim()))) {
    const numericValue = Number(value);
    if (Number.isFinite(numericValue) && ArmName[numericValue] !== undefined) {
      return String(ArmName[numericValue]);
    }
  }
  return String(value ?? '').trim();
}

export function readClassLabelOnly(item: TeacherClassArmAssignment): string {
  const arm = item.arm ?? item.Arm;
  return String(arm?.classs?.classLabel ?? arm?.Classs?.ClassLabel ?? '').trim();
}

export function buildFormMasterConcatParameters(item: TeacherClassArmAssignment): string {
  // Legacy: arm + "," + classs + "," + classLabel  (classLabel escaped in JS)
  return `${readArmNameEnum(item)},${readClassNameEnum(item)},${readClassLabelOnly(item)}`;
}

export function readStudentId(student: StudentDetails | null | undefined): string {
  return String(student?.id ?? student?.Id ?? '').trim();
}

export function readStudentIdNo(student: StudentDetails | null | undefined): string {
  return String(student?.idNo ?? student?.IdNo ?? '').trim();
}

export function readStudentFullName(student: StudentDetails | null | undefined): string {
  // API often flattens names onto StudentDetailsDto (User is ignored by AutoMapper).
  const explicit =
    student?.fullName ??
    student?.FullName ??
    student?.user?.fullName ??
    student?.user?.FullName ??
    student?.User?.fullName ??
    student?.User?.FullName;
  if (explicit && String(explicit).trim()) {
    return String(explicit).trim();
  }

  const user = student?.user ?? student?.User;
  const parts = [
    student?.firstName ?? student?.FirstName ?? user?.firstName ?? user?.FirstName,
    student?.middleName ?? student?.MiddleName ?? user?.middleName ?? user?.MiddleName,
    student?.surName ?? student?.SurName ?? user?.surName ?? user?.SurName,
  ]
    .map(part => String(part ?? '').trim())
    .filter(Boolean);

  return parts.join(' ');
}

export function readStudentGender(student: StudentDetails | null | undefined): string {
  const user = student?.user ?? student?.User;
  return String(
    student?.gender ?? student?.Gender ?? user?.gender ?? user?.Gender ?? '',
  ).trim();
}

export function readAssessmentId(row: StudentsAccessment): string {
  return String(row.id ?? row.Id ?? '').trim();
}

export function readAssessmentYearlyStatusId(row: StudentsAccessment): string {
  return String(row.yearlyAndTermlyStatusId ?? row.YearlyAndTermlyStatusId ?? '').trim();
}

export function readAssessmentStudentId(row: StudentsAccessment): string {
  return String(row.studentId ?? row.StudentId ?? readStudentId(row.student ?? row.Student)).trim();
}

export function readAssessmentClassArmId(row: StudentsAccessment): string {
  return String(row.classArmId ?? row.ClassArmId ?? '').trim();
}

export function readAssessmentSubjectId(row: StudentsAccessment): string {
  return String(row.subjectId ?? row.SubjectId ?? '').trim();
}

export function readScoreValue(row: StudentsAccessment, key: 'ca1' | 'ca2' | 'ca3' | 'exams'): number {
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  const camel = key as keyof StudentsAccessment;
  const value = row[camel] ?? row[pascal as keyof StudentsAccessment];
  return Number(value ?? 0);
}

export function normalizeCasCategory(value: unknown): CasCategoryName {
  if (value === 'OneCA' || value === 0 || value === '0') {
    return 'OneCA';
  }

  if (value === 'TwoCAs' || value === 1 || value === '1') {
    return 'TwoCAs';
  }

  return 'ThreeCAs';
}

export function readCasSetting(source: StudentsAccessmentCasSetting | null | undefined): CasSetting {
  return source?.casSetting ?? source?.CasSetting ?? {};
}

export function readPerCasValue(casSetting: CasSetting): number {
  return Number(casSetting.perCasValue ?? casSetting.PerCasValue ?? 0);
}

export function computeExamsMax(casSetting: CasSetting): number {
  const perCas = readPerCasValue(casSetting);
  const category = normalizeCasCategory(casSetting.casCategory ?? casSetting.CasCategory);

  if (category === 'ThreeCAs') {
    return 100 - perCas * 3;
  }

  if (category === 'TwoCAs') {
    return 100 - perCas * 2;
  }

  return 100 - perCas;
}

export function readStudentsAccessment(source: StudentsAccessmentCasSetting | null | undefined): StudentsAccessment[] {
  return source?.studentsAccessment ?? source?.StudentsAccessment ?? [];
}

export function sortAssignments(items: TeacherSubjectArmAssignment[]): TeacherSubjectArmAssignment[] {
  return [...items].sort((left, right) => readSubjectId(left).localeCompare(readSubjectId(right)));
}

export function sortAssessments(items: StudentsAccessment[], orderBy: '0' | '1' | '2'): StudentsAccessment[] {
  const rows = [...items];

  if (orderBy === '1') {
    return rows.sort((left, right) => {
      const leftUser = left.student?.user ?? left.student?.User ?? left.Student?.user ?? left.Student?.User;
      const rightUser = right.student?.user ?? right.student?.User ?? right.Student?.user ?? right.Student?.User;
      const leftSur = String(leftUser?.surName ?? leftUser?.SurName ?? '');
      const rightSur = String(rightUser?.surName ?? rightUser?.SurName ?? '');
      return leftSur.localeCompare(rightSur, undefined, { sensitivity: 'base' });
    });
  }

  if (orderBy === '2') {
    return rows.sort((left, right) => {
      const genderCompare = readStudentGender(left.student ?? left.Student).localeCompare(
        readStudentGender(right.student ?? right.Student),
        undefined,
        { sensitivity: 'base' },
      );

      if (genderCompare !== 0) {
        return genderCompare;
      }

      const leftUser = left.student?.user ?? left.student?.User ?? left.Student?.user ?? left.Student?.User;
      const rightUser = right.student?.user ?? right.student?.User ?? right.Student?.user ?? right.Student?.User;
      const leftFirst = String(leftUser?.firstName ?? leftUser?.FirstName ?? '');
      const rightFirst = String(rightUser?.firstName ?? rightUser?.FirstName ?? '');
      return leftFirst.localeCompare(rightFirst, undefined, { sensitivity: 'base' });
    });
  }

  return rows.sort((left, right) => {
    const leftUser = left.student?.user ?? left.student?.User ?? left.Student?.user ?? left.Student?.User;
    const rightUser = right.student?.user ?? right.student?.User ?? right.Student?.user ?? right.Student?.User;
    const leftFirst = String(leftUser?.firstName ?? leftUser?.FirstName ?? '');
    const rightFirst = String(rightUser?.firstName ?? rightUser?.FirstName ?? '');
    return leftFirst.localeCompare(rightFirst, undefined, { sensitivity: 'base' });
  });
}

export function computeAssessmentTotal(row: ScoreRowState): number {
  return Number(row.ca1 || 0) + Number(row.ca2 || 0) + Number(row.ca3 || 0) + Number(row.exams || 0);
}

export type ScoreRowState = {
  assessmentId: string;
  studentId: string;
  yearlyAndTermlyStatusId: string;
  classArmId: string;
  subjectId: string;
  idNo: string;
  fullName: string;
  firstName: string;
  surName: string;
  gender: string;
  ca1: number | null;
  ca2: number | null;
  ca3: number | null;
  exams: number | null;
};

export function mapAssessmentToScoreRow(row: StudentsAccessment, subjectId: string, classArmId: string): ScoreRowState {
  const student = row.student ?? row.Student;
  const user = student?.user ?? student?.User;

  return {
    assessmentId: readAssessmentId(row),
    studentId: readAssessmentStudentId(row),
    yearlyAndTermlyStatusId: readAssessmentYearlyStatusId(row),
    classArmId: readAssessmentClassArmId(row) || classArmId,
    subjectId: readAssessmentSubjectId(row) || subjectId,
    idNo: readStudentIdNo(student),
    fullName: readStudentFullName(student),
    firstName: String(user?.firstName ?? user?.FirstName ?? ''),
    surName: String(user?.surName ?? user?.SurName ?? ''),
    gender: readStudentGender(student),
    ca1: readScoreValue(row, 'ca1'),
    ca2: readScoreValue(row, 'ca2'),
    ca3: readScoreValue(row, 'ca3'),
    exams: readScoreValue(row, 'exams'),
  };
}

export function sortScoreRows(items: ScoreRowState[], orderBy: '0' | '1' | '2'): ScoreRowState[] {
  const rows = [...items];

  if (orderBy === '1') {
    return rows.sort((left, right) => left.surName.localeCompare(right.surName, undefined, { sensitivity: 'base' }));
  }

  if (orderBy === '2') {
    return rows.sort((left, right) => {
      const genderCompare = left.gender.localeCompare(right.gender, undefined, { sensitivity: 'base' });
      if (genderCompare !== 0) {
        return genderCompare;
      }

      return left.firstName.localeCompare(right.firstName, undefined, { sensitivity: 'base' });
    });
  }

  return rows.sort((left, right) => left.firstName.localeCompare(right.firstName, undefined, { sensitivity: 'base' }));
}
