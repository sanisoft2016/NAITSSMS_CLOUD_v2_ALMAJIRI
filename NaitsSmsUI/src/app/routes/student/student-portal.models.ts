import { AccessStatus, ClassCategoryStatus, CurrentClass } from '@shared';

export interface SelectOption {
  text: string;
  value: string;
}

export interface StudentSchoolInfo {
  ssNo?: number;
  schoolName?: string | null;
  schoolAddress?: string | null;
  telephone?: string | null;
  poAddress?: string | null;
  emailAddress?: string | null;
  website?: string | null;
  schoolLogoString?: string | null;
  motto?: string | null;
}

export interface StudentSubjectCategory {
  subjectStatus?: number | string | null;
  subjectStatusLabel?: string | null;
}

export interface StudentSubjectRecord {
  id: string;
  subjectName: string;
  timeStatus?: number | string | null;
  subjectCategory?: StudentSubjectCategory | null;
}

export interface ElectiveStatusRecord {
  electiveId: string;
  electiveStatusCode: number;
}

export interface SubjectsAndClassNameRecord {
  className?: number | CurrentClass | null;
  studentStatus?: number | ClassCategoryStatus | null;
  accessStatu?: number | AccessStatus | null;
  subjectDtos: StudentSubjectRecord[];
  electiveStatus: ElectiveStatusRecord[];
  emptyMessage?: string | null;
}

export interface StudentPortalSibling {
  id: string;
  idNo: string;
  displayName: string;
  email: string;
  classLabel: string;
  armLabel: string;
  isCurrentLogin: boolean;
}

export interface StudentProfileRecord {
  id?: string | null;
  idNo?: string | null;
  firstName?: string | null;
  middleName?: string | null;
  surName?: string | null;
  dateOfBirth?: string | null;
  placeOfBirth?: string | null;
  bloodGroup?: string | null;
  dateOfAdmsn?: string | null;
  transferedFromWhere?: string | null;
  guardiansName?: string | null;
  relationship?: string | null;
  guardianAddress?: string | null;
  occupation?: string | null;
  mobileNumber?: string | null;
  homeNumber?: string | null;
  startClass?: string | null;
  religion?: number | string | null;
  emailAddress?: string | null;
  studentMobileNo?: string | null;
  country?: string | null;
  passport?: string | null;
  status?: number | string | null;
  classArm?: {
    armName?: number | string | null;
    classs?: { classLabel?: string | null } | null;
  } | null;
  user?: {
    firstName?: string | null;
    middleName?: string | null;
    surName?: string | null;
    accessStatus?: number | string | null;
  } | null;
}

export function isStudentAccountDisabled(accessStatus: number | string | null | undefined): boolean {
  if (accessStatus === null || accessStatus === undefined || accessStatus === '') {
    return false;
  }

  if (typeof accessStatus === 'number') {
    return accessStatus === AccessStatus.Disable;
  }

  const normalized = String(accessStatus).trim().toLowerCase();
  return normalized === 'disable' || normalized === 'disabled' || normalized === String(AccessStatus.Disable);
}

export enum ElectiveStatusCode {
  OptionalToAllStudents = 0,
  CompulsaryToSomeStudents = 1,
}

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

export function normalizeSelectOptions(items: unknown): SelectOption[] {
  if (!Array.isArray(items)) {
    return [];
  }

  return items.map(item => {
    const raw = (item ?? {}) as Record<string, unknown>;
    const value = String(raw['value'] ?? raw['Value'] ?? '').trim();
    const text = String(raw['text'] ?? raw['Text'] ?? '').trim();
    return {
      value,
      text: text || defaultSubjectCategoryOptionText(value),
    };
  });
}

function defaultSubjectCategoryOptionText(value: string): string {
  switch (value) {
    case 'PlayClass':
      return 'Play Class Subjects';
    case 'PreNursery':
      return 'Pre Nursery Subjects';
    case 'Nursery':
      return 'Nursery Subjects';
    case 'Primary':
      return 'Primary Subjects';
    case 'Jss':
      return 'JSS Subjects';
    case 'General':
      return 'General Subjects';
    case 'AllSeniors':
      return 'All Senior Subjects';
    case 'Elective':
      return 'Elective Subjects';
    case 'Science':
      return 'Science Subjects';
    case 'Art':
      return 'Art Subjects';
    case 'Commercial':
      return 'Commercial Subjects';
    case 'AllSubjects':
      return 'All-subjects';
    case 'My-subject-list':
      return 'My-subject-list';
    default:
      return value || 'Untitled category';
  }
}

export function normalizeSchoolInfo(raw: unknown): StudentSchoolInfo | null {
  if (!raw || typeof raw !== 'object') {
    return null;
  }

  const item = raw as Record<string, unknown>;
  return {
    ssNo: Number(item['ssNo'] ?? item['SsNo'] ?? NaN) || undefined,
    schoolName: String(item['schoolName'] ?? item['SchoolName'] ?? '').trim() || null,
    schoolAddress: String(item['schoolAddress'] ?? item['SchoolAddress'] ?? '').trim() || null,
    telephone: String(item['telephone'] ?? item['Telephone'] ?? '').trim() || null,
    poAddress: String(item['poAddress'] ?? item['PoAddress'] ?? '').trim() || null,
    emailAddress: String(item['emailAddress'] ?? item['EmailAddress'] ?? '').trim() || null,
    website: String(item['website'] ?? item['Website'] ?? '').trim() || null,
    schoolLogoString: String(item['schoolLogoString'] ?? item['SchoolLogoString'] ?? '').trim() || null,
    motto: String(item['motto'] ?? item['Motto'] ?? '').trim() || null,
  };
}

function asNumber(value: unknown): number | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function asScalar(value: unknown): string | number | null {
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value === 'string' || typeof value === 'number') {
    return value;
  }
  if (typeof value === 'boolean') {
    return value ? 1 : 0;
  }
  return String(value);
}

function normalizeSubject(item: unknown): StudentSubjectRecord | null {
  if (!item || typeof item !== 'object') {
    return null;
  }

  const raw = item as Record<string, unknown>;
  const id = String(raw['id'] ?? raw['Id'] ?? '').trim();
  if (!id) {
    return null;
  }

  const categoryRaw = (raw['subjectCategory'] ?? raw['SubjectCategory'] ?? null) as Record<string, unknown> | null;
  return {
    id,
    subjectName: String(raw['subjectName'] ?? raw['SubjectName'] ?? '').trim(),
    timeStatus: asScalar(raw['timeStatus'] ?? raw['TimeStatus']),
    subjectCategory: categoryRaw
      ? {
          subjectStatus: asScalar(categoryRaw['subjectStatus'] ?? categoryRaw['SubjectStatus']),
          subjectStatusLabel: String(
            categoryRaw['subjectStatusLabel'] ?? categoryRaw['SubjectStatusLabel'] ?? '',
          ).trim() || null,
        }
      : null,
  };
}

export function normalizeSubjectsAndClassName(raw: unknown): SubjectsAndClassNameRecord {
  const item = (raw ?? {}) as Record<string, unknown>;
  const subjectsRaw = item['subjectDtos'] ?? item['SubjectDtos'] ?? [];
  const electivesRaw = item['electiveStatus'] ?? item['ElectiveStatus'] ?? [];

  return {
    className: asNumber(item['className'] ?? item['ClassName']),
    studentStatus: asNumber(item['studentStatus'] ?? item['StudentStatus']),
    accessStatu: asNumber(item['accessStatu'] ?? item['AccessStatu'] ?? item['accessStatus'] ?? item['AccessStatus']),
    subjectDtos: Array.isArray(subjectsRaw)
      ? subjectsRaw.map(normalizeSubject).filter((row): row is StudentSubjectRecord => row !== null)
      : [],
    electiveStatus: Array.isArray(electivesRaw)
      ? electivesRaw
          .map(entry => {
            const e = (entry ?? {}) as Record<string, unknown>;
            const electiveId = String(e['electiveId'] ?? e['ElectiveId'] ?? '').trim();
            const electiveStatusCode = asNumber(e['electiveStatusCode'] ?? e['ElectiveStatusCode']);
            if (!electiveId || electiveStatusCode === null) {
              return null;
            }
            return { electiveId, electiveStatusCode };
          })
          .filter((row): row is ElectiveStatusRecord => row !== null)
      : [],
    emptyMessage: String(item['msg'] ?? item['Msg'] ?? item['message'] ?? item['Message'] ?? '').trim() || null,
  };
}

export function normalizeStudentPortalSiblings(raw: unknown): StudentPortalSibling[] {
  if (!Array.isArray(raw)) {
    return [];
  }

  return raw
    .map(entry => {
      const item = (entry ?? {}) as Record<string, unknown>;
      const id = String(item['id'] ?? item['Id'] ?? '').trim();
      if (!id) {
        return null;
      }

      const displayName =
        String(item['displayName'] ?? item['DisplayName'] ?? '').trim() ||
        String(item['idNo'] ?? item['IdNo'] ?? id).trim();

      return {
        id,
        idNo: String(item['idNo'] ?? item['IdNo'] ?? '').trim(),
        displayName,
        email: String(item['email'] ?? item['Email'] ?? item['emailAddress'] ?? item['EmailAddress'] ?? '').trim(),
        classLabel: String(item['classLabel'] ?? item['ClassLabel'] ?? '').trim(),
        armLabel: String(item['armLabel'] ?? item['ArmLabel'] ?? '').trim(),
        isCurrentLogin: Boolean(item['isCurrentLogin'] ?? item['IsCurrentLogin'] ?? false),
      } satisfies StudentPortalSibling;
    })
    .filter((row): row is StudentPortalSibling => row !== null);
}

export function normalizeStudentProfile(raw: unknown): StudentProfileRecord {
  const item = (raw ?? {}) as Record<string, unknown>;
  const user = (item['user'] ?? item['User'] ?? null) as Record<string, unknown> | null;
  const classArm = (item['classArm'] ?? item['ClassArm'] ?? null) as Record<string, unknown> | null;
  const classs = (classArm?.['classs'] ?? classArm?.['Classs'] ?? null) as Record<string, unknown> | null;

  return {
    id: String(item['id'] ?? item['Id'] ?? '').trim() || null,
    idNo: String(item['idNo'] ?? item['IdNo'] ?? '').trim() || null,
    firstName: String(item['firstName'] ?? item['FirstName'] ?? user?.['firstName'] ?? user?.['FirstName'] ?? '').trim() || null,
    middleName:
      String(item['middleName'] ?? item['MiddleName'] ?? user?.['middleName'] ?? user?.['MiddleName'] ?? '').trim() ||
      null,
    surName: String(item['surName'] ?? item['SurName'] ?? user?.['surName'] ?? user?.['SurName'] ?? '').trim() || null,
    dateOfBirth: String(item['dateOfBirth'] ?? item['DateOfBirth'] ?? '').trim() || null,
    placeOfBirth: String(item['placeOfBirth'] ?? item['PlaceOfBirth'] ?? '').trim() || null,
    bloodGroup: String(item['bloodGroup'] ?? item['BloodGroup'] ?? '').trim() || null,
    dateOfAdmsn: String(item['dateOfAdmsn'] ?? item['DateOfAdmsn'] ?? '').trim() || null,
    transferedFromWhere: String(item['transferedFromWhere'] ?? item['TransferedFromWhere'] ?? '').trim() || null,
    guardiansName: String(item['guardiansName'] ?? item['GuardiansName'] ?? '').trim() || null,
    relationship: String(item['relationship'] ?? item['Relationship'] ?? '').trim() || null,
    guardianAddress: String(item['guardianAddress'] ?? item['GuardianAddress'] ?? '').trim() || null,
    occupation: String(item['occupation'] ?? item['Occupation'] ?? '').trim() || null,
    mobileNumber: String(item['mobileNumber'] ?? item['MobileNumber'] ?? '').trim() || null,
    homeNumber: String(item['homeNumber'] ?? item['HomeNumber'] ?? '').trim() || null,
    startClass: String(item['startClass'] ?? item['StartClass'] ?? '').trim() || null,
    religion: asScalar(item['religion'] ?? item['Religion']),
    emailAddress: String(item['emailAddress'] ?? item['EmailAddress'] ?? '').trim() || null,
    studentMobileNo: String(item['studentMobileNo'] ?? item['StudentMobileNo'] ?? '').trim() || null,
    country: String(item['country'] ?? item['Country'] ?? '').trim() || null,
    passport: String(item['passport'] ?? item['Passport'] ?? '').trim() || null,
    status: asScalar(item['status'] ?? item['Status']),
    classArm: classArm
      ? {
          armName: asScalar(classArm['armName'] ?? classArm['ArmName']),
          classs: classs ? { classLabel: String(classs['classLabel'] ?? classs['ClassLabel'] ?? '').trim() || null } : null,
        }
      : null,
    user: user
      ? {
          firstName: String(user['firstName'] ?? user['FirstName'] ?? '').trim() || null,
          middleName: String(user['middleName'] ?? user['MiddleName'] ?? '').trim() || null,
          surName: String(user['surName'] ?? user['SurName'] ?? '').trim() || null,
          accessStatus: asScalar(user['accessStatus'] ?? user['AccessStatus']),
        }
      : null,
  };
}

export function subjectStatusLabel(subject: StudentSubjectRecord): string {
  const label = subject.subjectCategory?.subjectStatusLabel?.trim();
  if (label) {
    return label;
  }

  switch (Number(subject.subjectCategory?.subjectStatus)) {
    case SubjectStatusCode.PreNursery:
      return 'Pre Nursery Subjects';
    case SubjectStatusCode.Nursery:
      return 'Nursery Subjects';
    case SubjectStatusCode.Primary:
      return 'Primary Subjects';
    case SubjectStatusCode.Jss:
      return 'JSS Subjects';
    case SubjectStatusCode.General:
      return 'General Subjects';
    case SubjectStatusCode.AllSeniors:
      return 'All Senior Subjects';
    case SubjectStatusCode.Elective:
      return 'Elective Subjects';
    case SubjectStatusCode.Science:
      return 'Science Subjects';
    case SubjectStatusCode.Art:
      return 'Art Subjects';
    case SubjectStatusCode.Commercial:
      return 'Commercial Subjects';
    default:
      return String(subject.subjectCategory?.subjectStatus ?? '');
  }
}

/**
 * Legacy RenderListOfSujectsUnderTheSelectedCategory selection column rules.
 * Returns 'compulsory' | 'checkbox' | 'none'.
 */
export function resolveSelectionMode(
  subjectStatusFilter: string,
  subject: StudentSubjectRecord,
  studentStatus: number | null | undefined,
  className: number | null | undefined,
  electiveStatuses: ElectiveStatusRecord[],
): 'compulsory' | 'checkbox' {
  const status = Number(subject.subjectCategory?.subjectStatus);
  const student = Number(studentStatus);
  const cls = Number(className);

  if (subjectStatusFilter !== 'My-subject-list') {
    return 'checkbox';
  }

  if (
    student === ClassCategoryStatus.Science &&
    (status === SubjectStatusCode.Science ||
      status === SubjectStatusCode.General ||
      status === SubjectStatusCode.AllSeniors)
  ) {
    return 'compulsory';
  }

  if (
    student === ClassCategoryStatus.Science &&
    (status === SubjectStatusCode.Elective ||
      status === SubjectStatusCode.Art ||
      status === SubjectStatusCode.Commercial)
  ) {
    return 'checkbox';
  }

  if (
    student === ClassCategoryStatus.Art &&
    (status === SubjectStatusCode.Art ||
      status === SubjectStatusCode.General ||
      status === SubjectStatusCode.AllSeniors)
  ) {
    return 'compulsory';
  }

  if (
    student === ClassCategoryStatus.Art &&
    (status === SubjectStatusCode.Elective ||
      status === SubjectStatusCode.Science ||
      status === SubjectStatusCode.Commercial)
  ) {
    return 'checkbox';
  }

  if (
    student === ClassCategoryStatus.Commercial &&
    (status === SubjectStatusCode.Commercial ||
      status === SubjectStatusCode.General ||
      status === SubjectStatusCode.AllSeniors)
  ) {
    return 'compulsory';
  }

  if (
    student === ClassCategoryStatus.Commercial &&
    (status === SubjectStatusCode.Elective ||
      status === SubjectStatusCode.Art ||
      status === SubjectStatusCode.Science)
  ) {
    return 'checkbox';
  }

  const elective = electiveStatuses.find(item => item.electiveId === subject.id);
  const electiveCode = elective?.electiveStatusCode ?? null;

  if (status === SubjectStatusCode.PreNursery && cls === CurrentClass.PreNursery) {
    return 'compulsory';
  }

  if (
    status === SubjectStatusCode.Nursery &&
    (cls === CurrentClass.Nursery1 || cls === CurrentClass.Nursery2)
  ) {
    // Legacy had a buggy && between Nursery1 and Nursery2; treat as either class.
    return 'compulsory';
  }

  if (
    (cls === CurrentClass.Nursery1 || cls === CurrentClass.Nursery2) &&
    status === SubjectStatusCode.Elective
  ) {
    return 'checkbox';
  }

  if (
    status === SubjectStatusCode.Primary &&
    (cls === CurrentClass.Primary1 ||
      cls === CurrentClass.Primary2 ||
      cls === CurrentClass.Primary3 ||
      cls === CurrentClass.Primary4 ||
      cls === CurrentClass.Primary5 ||
      cls === CurrentClass.Primary6)
  ) {
    return 'compulsory';
  }

  if (status === SubjectStatusCode.Jss && (cls === CurrentClass.Jss1 || cls === CurrentClass.Jss2 || cls === CurrentClass.Jss3)) {
    return 'compulsory';
  }

  if (electiveCode === ElectiveStatusCode.CompulsaryToSomeStudents) {
    return 'compulsory';
  }

  if (electiveCode === ElectiveStatusCode.OptionalToAllStudents) {
    return 'checkbox';
  }

  return 'compulsory';
}
