import { CurrentClass } from "@shared/services";

export interface ClassDto {
  id: string;
  className: CurrentClass;
  classLabel: string;
  sNo: number;
  schoolInfoId: string;
}

export interface ClassReArrangementDto {
  id: string;
  schoolInfoId: string;
  sNo: number;
}

export enum ArmName {
  A     = 0,
  B     = 1,
  C     = 2,
  D     = 3,
  E     = 4,
  F     = 5,
  G     = 6,
  H     = 7,
  I     = 8,
  J     = 9,
  K     = 10,
  L     = 11,
  N     = 12,
  O     = 13,
  NoArm = 14,
}

export interface ClassArmDto {
  id: string;
  sNo: number;
  classId: string;
  armName: ArmName;
  formMaster?: string;
  classs?: { id: string; classLabel: string; };
}

// ── Students ─────────────────────────────────────────────────────────────────

export enum Gender {
  Male   = 0,
  Female = 1,
}

export enum StudentStatus {
  Fresher   = 0,
  Returning = 1,
  Previous  = 2,
}

export enum Religion {
  Islam        = 0,
  Christianity = 1,
}

export enum AccessStatus {
  Active  = 0,
  Disable = 1,
}

export enum ClassCategoryStatus {
  PlayClass  = 0,
  PreNursery = 1,
  Nursery    = 2,
  Primary    = 3,
  Jss        = 4,
  Science    = 5,
  Art        = 6,
  Commercial = 7,
  None       = 8,
}

export interface StudentRegistrationContext {
  classId: string;
  classs: string;
  classLabel: string;
  armId: string;
  armName: string;
  totalNoOfStudents: number;
}

export interface StudentRegistrationDraftDto {
  idNo: string;
  sNo: number;
  classArmId: string;
  currentClass: CurrentClass;
  dateOfAdmsn: string;
  dateOfBirth: string;
}

export interface StudentRegistrationDefaultsDto {
  classId: string;
  classLabel: string;
  armId: string;
  armName: string;
  currentClass: CurrentClass;
  totalNoOfStudents: number;
  student: StudentRegistrationDraftDto;
}

export interface StudentDto {
  id: string;
  sNo: number;
  idNo: string;
  firstName: string;
  middleName: string;
  surName: string;
  fullName: string;
  gender: Gender;
  currentClass: CurrentClass;
  classArmId: string;
  classArm?: ClassArmDto;
  status: StudentStatus;
  schoolInfoId: string;
}
