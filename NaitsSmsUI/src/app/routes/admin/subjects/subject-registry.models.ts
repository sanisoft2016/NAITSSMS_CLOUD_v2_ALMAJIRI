import { ClassArmDto } from '@shared/interfaces';

export enum SubjectStatusCode {
  PlayClass = 0,
  PreNursery = 1,
  General = 2,
  Nursery = 3,
  Primary = 4,
  Jss = 5,
  AllSeniors = 6,
  Science = 7,
  Art = 8,
  Commercial = 9,
  Elective = 10,
}

export enum TimeStatusCode {
  Current = 0,
  Previous = 1,
}

export enum ClassCategoryStatusCode {
  PlayClass = 0,
  PreNursery = 1,
  Nursery = 2,
  Primary = 3,
  Jss = 4,
  Science = 5,
  Art = 6,
  Commercial = 7,
  None = 8,
}

export type SubjectStatusName =
  | 'PlayClass'
  | 'PreNursery'
  | 'General'
  | 'Nursery'
  | 'Primary'
  | 'Jss'
  | 'AllSeniors'
  | 'Science'
  | 'Art'
  | 'Commercial'
  | 'Elective';

export interface SelectOptionRecord {
  text: string;
  value: string;
  selected?: boolean;
  disabled?: boolean;
}

export function normalizeSelectOption(item: unknown): SelectOptionRecord {
  const raw = (item ?? {}) as Record<string, unknown>;
  return {
    value: String(raw['value'] ?? raw['Value'] ?? '').trim(),
    text: String(raw['text'] ?? raw['Text'] ?? '').trim(),
    selected: Boolean(raw['selected'] ?? raw['Selected'] ?? false),
    disabled: Boolean(raw['disabled'] ?? raw['Disabled'] ?? false),
  };
}

export function normalizeSelectOptions(items: unknown): SelectOptionRecord[] {
  if (!Array.isArray(items)) {
    return [];
  }

  return items.map(item => normalizeSelectOption(item));
}

export interface SubjectCategoryRecord {
  id?: number;
  subjectStatus?: SubjectStatusCode | number | null;
  subjectStatusLabel?: string | null;
  schoolId?: string | null;
}

export interface SubjectRecord {
  id: string;
  subjectName?: string | null;
  timeStatus?: TimeStatusCode | number | string | null;
  subjectCategoryId?: string | null;
  subjectCategory?: SubjectCategoryRecord | null;
}

export interface SubjectListResponse {
  subjectLevelStatus: string;
  subjectStatus: SubjectStatusCode | number;
  totalNoOfSubjects: number;
  title: string;
  shouldCreate: boolean;
  subjects: SubjectRecord[];
}

export interface SubjectSavePayload {
  subjectName: string;
  timeStatus: TimeStatusCode;
  subjectCategoryId?: string;
}

export interface SubjectAssignmentContextRecord {
  electiveSubjectsDropdown: SelectOptionRecord[];
  classLabelDropdown: SelectOptionRecord[];
}

export interface SubjectArmsResponse {
  arms: SelectOptionRecord[];
  subjectId?: string;
  message?: string | null;
}

export interface SubjectStudentRecord {
  id: string;
  idNo?: string | null;
  fullName?: string | null;
  firstName?: string | null;
  middleName?: string | null;
  surName?: string | null;
  classCategory?: ClassCategoryStatusCode | number | string | null;
  currentClass?: number | string | null;
}

export interface SubjectStudentsResponse {
  studentsList: SubjectStudentRecord[];
  assignedStudentIds: string[];
}

export interface SubjectAssignmentSubmitResponse {
  data?: string | null;
  message?: string | null;
}

export interface SubjectSelectionRecord {
  Id: string;
}

export interface LabelEditorRecord {
  generalSujectLabel?: string;
  electiveSubjectLabel?: string;
  playClassLabel?: string;
  preNurseryLabel?: string;
  nurseryLabel?: string;
  primaryLabel?: string;
  jssLabel?: string;
  ssLabel?: string;
}

export const SUBJECT_STATUS_QUERY_NAMES: Record<SubjectStatusCode, SubjectStatusName> = {
  [SubjectStatusCode.PlayClass]: 'PlayClass',
  [SubjectStatusCode.PreNursery]: 'PreNursery',
  [SubjectStatusCode.General]: 'General',
  [SubjectStatusCode.Nursery]: 'Nursery',
  [SubjectStatusCode.Primary]: 'Primary',
  [SubjectStatusCode.Jss]: 'Jss',
  [SubjectStatusCode.AllSeniors]: 'AllSeniors',
  [SubjectStatusCode.Science]: 'Science',
  [SubjectStatusCode.Art]: 'Art',
  [SubjectStatusCode.Commercial]: 'Commercial',
  [SubjectStatusCode.Elective]: 'Elective',
};

export const DEFAULT_SUBJECT_LABELS: Record<SubjectStatusName, string> = {
  PlayClass: 'Play Class Subjects',
  PreNursery: 'Pre Nursery Subjects',
  General: 'General Subjects',
  Nursery: 'Nursery Subjects',
  Primary: 'Primary Subjects',
  Jss: 'Junior Secondary Subjects',
  AllSeniors: 'Senior Secondary Subjects',
  Science: 'Science Subjects',
  Art: 'Art Subjects',
  Commercial: 'Commercial Subjects',
  Elective: 'Elective Subjects',
};

export function subjectStatusToQueryName(subjectStatus: SubjectStatusCode | number | string | null | undefined): SubjectStatusName {
  if (typeof subjectStatus === 'string') {
    const trimmed = subjectStatus.trim();
    if (!trimmed) {
      return 'PlayClass';
    }

    // Production proxies / typed URLs sometimes change casing (JSS, jss).
    const matchedByName = Object.values(SUBJECT_STATUS_QUERY_NAMES).find(
      name => name.toLowerCase() === trimmed.toLowerCase(),
    );
    if (matchedByName) {
      return matchedByName;
    }
  }

  const numericValue = Number(subjectStatus);
  return SUBJECT_STATUS_QUERY_NAMES[numericValue as SubjectStatusCode] ?? 'PlayClass';
}

export function subjectStatusToCode(subjectStatus: SubjectStatusCode | number | string | null | undefined): SubjectStatusCode {
  const name = subjectStatusToQueryName(subjectStatus);
  const matched = (Object.entries(SUBJECT_STATUS_QUERY_NAMES) as Array<[string, SubjectStatusName]>).find(
    ([, statusName]) => statusName === name,
  );
  return matched ? (Number(matched[0]) as SubjectStatusCode) : SubjectStatusCode.PlayClass;
}

export function subjectStatusDisplayLabel(subjectStatus: SubjectStatusCode | number | string | null | undefined): string {
  return DEFAULT_SUBJECT_LABELS[subjectStatusToQueryName(subjectStatus)];
}

export function subjectCategoryDisplayLabel(subject: SubjectRecord): string {
  const explicitLabel = String(subject.subjectCategory?.subjectStatusLabel ?? '').trim();
  if (explicitLabel) {
    return explicitLabel;
  }

  return subjectStatusDisplayLabel(subject.subjectCategory?.subjectStatus ?? null);
}

export function timeStatusLabel(timeStatus: TimeStatusCode | number | string | null | undefined): string {
  return Number(timeStatus) === TimeStatusCode.Previous ? 'Previous' : 'Current';
}

export function classCategoryLabel(classCategory: ClassCategoryStatusCode | number | string | null | undefined): string {
  switch (Number(classCategory)) {
    case ClassCategoryStatusCode.PlayClass:
      return 'PlayClass';
    case ClassCategoryStatusCode.PreNursery:
      return 'PreNursery';
    case ClassCategoryStatusCode.Nursery:
      return 'Nursery';
    case ClassCategoryStatusCode.Primary:
      return 'Primary';
    case ClassCategoryStatusCode.Jss:
      return 'Jss';
    case ClassCategoryStatusCode.Science:
      return 'Science';
    case ClassCategoryStatusCode.Art:
      return 'Art';
    case ClassCategoryStatusCode.Commercial:
      return 'Commercial';
    default:
      return '-';
  }
}

export function resolveSubjectLabel(labels: SubjectCategoryRecord[], subjectStatus: SubjectStatusCode): string {
  const matchedLabel = labels.find(label => Number(label.subjectStatus) === subjectStatus)?.subjectStatusLabel?.trim();
  if (matchedLabel) {
    return matchedLabel;
  }

  return DEFAULT_SUBJECT_LABELS[SUBJECT_STATUS_QUERY_NAMES[subjectStatus]];
}
