import { ClassArmDto } from '@shared/interfaces';

export type EntityIdentifier = number | string;

export type AccessStatusValue = number | string | null | undefined;
export type TimeStatusValue = number | string | null | undefined;

export interface TeacherUserRecord {
  id?: string;
  userName?: string;
  firstName?: string;
  middleName?: string;
  surName?: string;
  lastName?: string;
  fullName?: string;
  phoneNumber?: string;
  accessStatus?: AccessStatusValue;
}

export interface TeacherEmployeeRecord {
  id: EntityIdentifier;
  userId?: string;
  employeeIdNo?: string;
  employeeStatus?: string | null;
  timeStatus?: TimeStatusValue;
  user?: TeacherUserRecord | null;
}

export interface TeacherClassRecord {
  className?: number | string | null;
  classLabel?: string | null;
}

export interface TeacherArmRecord {
  armName?: number | string | null;
  classs?: TeacherClassRecord | null;
}

export interface TeacherSubjectRecord {
  subjectName?: string | null;
}

export interface TeacherSubjectAssignmentRecord {
  id: EntityIdentifier;
  classArmLabel?: string | null;
  employee?: TeacherEmployeeRecord | null;
  subject?: TeacherSubjectRecord | null;
  arm?: TeacherArmRecord | null;
}

export interface TeacherClassAssignmentRecord {
  id: EntityIdentifier;
  employee?: TeacherEmployeeRecord | null;
  arm?: TeacherArmRecord | null;
}

export interface TeacherSelectOptionRecord {
  text: string;
  value: string;
  selected?: boolean;
  disabled?: boolean;
}

export interface TeacherAssignmentOptionsRecord {
  teachers: TeacherSelectOptionRecord[];
  classes: TeacherSelectOptionRecord[];
}

export interface TeacherAssignmentArmResultRecord {
  arms: ClassArmDto[];
  message?: string | null;
}

export interface TeacherSubjectAssignmentRequest {
  teacherId: string;
  subjectId: string;
  armId: string;
}

export interface TeacherFormMasterAssignmentRequest {
  teacherId: string;
  armId: string;
}

export interface TeacherUnassignedSubjectRecord {
  subjectName?: string | null;
  className?: number | string | null;
  classLabel?: string | null;
  armName?: number | string | null;
}

export interface TeacherCbtUserRecord {
  id: string;
  employeeIdNo?: string | null;
  employeeStatus?: string | null;
  timeStatus?: TimeStatusValue;
  userId: string;
  userName?: string | null;
  fullName?: string | null;
  phoneNumber?: string | null;
}

export interface TeacherCbtUsersResponse {
  employees: TeacherCbtUserRecord[];
  userIds: string[];
}

export interface TeacherUnassignRequest {
  fullName: string;
  assignmentId: EntityIdentifier;
}

export interface TeacherTransferSubjectRequest {
  assignmentIds: string[];
  proposedTeacherId: string;
}

export type TeacherTransferArmRequest = TeacherTransferSubjectRequest;

export type TeacherViewMode = 'registered' | 'subject' | 'form';