import {
  ClassOption,
  SelectOption,
  normalizeArms,
  normalizeClasses,
  normalizeSessions,
  normalizeTerms,
  parseClassEnum,
  selectLatestSelectOption,
} from '../student-results/student-results.models';

export type NewAssessmentCollectionMode = '0' | '1' | '2';

export interface UncompletedFormMasterRow {
  formMasterName: string;
  userName: string;
  classLabelAndArm: string;
}

/** Exact shape of PrepareEnvironmentForStudentAssessmentReturnDto from the API. */
export interface PrepareEnvironmentForStudentAssessmentReturn {
  yearlyAndTermlyStatusId?: string | null;
  YearlyAndTermlyStatusId?: string | null;
  currentTermStatus?: number | string | null;
  CurrentTermStatus?: number | string | null;
  message?: string | null;
  Message?: string | null;
  uncompletedFormMastersReport?: UncompletedFormMasterRow[] | null;
  UncompletedFormMastersReport?: UncompletedFormMasterRow[] | null;
  totalStudentListToPdf?: unknown[] | null;
  TotalStudentListToPdf?: unknown[] | null;
}

export interface NewAssessmentProcessRequest {
  collectionType: 0 | 1 | 2;
  className?: number;
  armName?: number;
  assessmentCategory: '0' | '1';
  actionType: '0' | '1';
  psychoAffectDefaultValue?: number;
}

export interface NewAssessmentProcessResponse {
  message?: string;
  Message?: string;
  data?: PrepareEnvironmentForStudentAssessmentReturn;
}

export {
  ClassOption,
  SelectOption,
  normalizeArms,
  normalizeClasses,
  normalizeSessions,
  normalizeTerms,
  parseClassEnum,
  selectLatestSelectOption,
};

export function normalizeSelectListItems(value: unknown): SelectOption[] {
  const items = Array.isArray(value) ? value : [];
  return items.map(item => {
    const record = item as Record<string, unknown>;
    return {
      text: String(record['text'] ?? record['Text'] ?? ''),
      value: String(record['value'] ?? record['Value'] ?? ''),
    };
  });
}

export function readResponseMessage(response: unknown): string {
  if (response == null) {
    return '';
  }

  if (typeof response === 'string') {
    return response.trim();
  }

  const record = response as Record<string, unknown>;
  const data = record['data'] as Record<string, unknown> | undefined;
  return String(
    record['message'] ??
      record['Message'] ??
      data?.['message'] ??
      data?.['Message'] ??
      ''
  ).trim();
}

/** True when the legacy/service message indicates a successful psycho or subject run. */
export function isAssessmentSuccessMessage(message: string): boolean {
  const normalized = message.trim().toLowerCase();
  if (!normalized) {
    return false;
  }

  return (
    normalized === 'success' ||
    normalized.startsWith('success') ||
    normalized.includes('successfully been generated') ||
    normalized.includes('successfully been deleted')
  );
}

/** Legacy shows status.Message when the defaulted-teachers list is empty. */
export function readDefaultedTeachersMessage(response: unknown, teacherCount: number): string {
  if (teacherCount > 0) {
    return '';
  }

  const record = response as Record<string, unknown> | null;
  if (!record) {
    return 'Success!';
  }

  const data = record['data'] as Record<string, unknown> | undefined;
  const dataMessage = String(data?.['message'] ?? data?.['Message'] ?? '').trim();
  if (dataMessage) {
    return dataMessage;
  }

  const topLevelMessage = String(record['message'] ?? record['Message'] ?? '').trim();
  if (topLevelMessage && !topLevelMessage.toLowerCase().includes('prepared successfully')) {
    return topLevelMessage;
  }

  return 'Success!';
}

function extractUncompletedFormMasterItems(value: unknown): Record<string, unknown>[] {
  const record = value && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null;
  const data = record?.['data'] as Record<string, unknown> | undefined;

  const candidates = [
    value,
    record?.['teachers'],
    record?.['Teachers'],
    record?.['uncompletedFormMastersReport'],
    record?.['UncompletedFormMastersReport'],
    data?.['uncompletedFormMastersReport'],
    data?.['UncompletedFormMastersReport'],
    data?.['teachers'],
    data?.['Teachers'],
  ];

  for (const candidate of candidates) {
    if (Array.isArray(candidate)) {
      return candidate as Record<string, unknown>[];
    }
  }

  return [];
}

export function normalizeUncompletedFormMasters(value: unknown): UncompletedFormMasterRow[] {
  return extractUncompletedFormMasterItems(value).map(item => ({
    formMasterName: String(item['formMasterName'] ?? item['FormMasterName'] ?? '').replace(/\s+/g, ' ').trim(),
    userName: String(item['userName'] ?? item['UserName'] ?? '').trim(),
    classLabelAndArm: String(item['classLabelAndArm'] ?? item['ClassLabelAndArm'] ?? '').trim(),
  }));
}
