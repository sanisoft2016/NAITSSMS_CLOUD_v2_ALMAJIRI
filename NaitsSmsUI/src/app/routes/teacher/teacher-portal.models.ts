export type EntityIdentifier = number | string;

export interface TeacherPortalSubject {
  id?: EntityIdentifier;
  subjectName?: string | null;
  SubjectName?: string | null;
}

export interface TeacherPortalClass {
  classLabel?: string | null;
  ClassLabel?: string | null;
  className?: string | number | null;
  ClassName?: string | number | null;
}

export interface TeacherPortalArm {
  id?: EntityIdentifier;
  armName?: EntityIdentifier | string | null;
  ArmName?: EntityIdentifier | string | null;
  classs?: TeacherPortalClass | null;
  Classs?: TeacherPortalClass | null;
}

export interface TeacherPortalUser {
  id?: EntityIdentifier;
  Id?: EntityIdentifier;
  firstName?: string | null;
  FirstName?: string | null;
  middleName?: string | null;
  MiddleName?: string | null;
  surName?: string | null;
  SurName?: string | null;
  fullName?: string | null;
  FullName?: string | null;
  gender?: number | string | null;
  Gender?: number | string | null;
}

export interface TeacherPortalEmployee {
  id?: EntityIdentifier;
  Id?: EntityIdentifier;
  user?: TeacherPortalUser | null;
  User?: TeacherPortalUser | null;
}

export interface TeacherSubjectArmAssignment {
  id?: EntityIdentifier;
  subjectId?: EntityIdentifier;
  SubjectId?: EntityIdentifier;
  armId?: EntityIdentifier;
  ArmId?: EntityIdentifier;
  classArmLabel?: string | null;
  ClassArmLabel?: string | null;
  subject?: TeacherPortalSubject | null;
  Subject?: TeacherPortalSubject | null;
  arm?: TeacherPortalArm | null;
  Arm?: TeacherPortalArm | null;
}

export interface TeacherClassArmAssignment {
  id?: EntityIdentifier;
  Id?: EntityIdentifier;
  armId?: EntityIdentifier;
  ArmId?: EntityIdentifier;
  classArmLabel?: string | null;
  ClassArmLabel?: string | null;
  armTermlyAssessmentStatus?: number | string | null;
  ArmTermlyAssessmentStatus?: number | string | null;
  employee?: TeacherPortalEmployee | null;
  Employee?: TeacherPortalEmployee | null;
  arm?: TeacherPortalArm | null;
  Arm?: TeacherPortalArm | null;
}

export interface CasSetting {
  casCategory?: number | string | null;
  CasCategory?: number | string | null;
  perCasValue?: number | null;
  PerCasValue?: number | null;
}

export interface StudentUser {
  firstName?: string | null;
  FirstName?: string | null;
  middleName?: string | null;
  MiddleName?: string | null;
  surName?: string | null;
  SurName?: string | null;
  fullName?: string | null;
  FullName?: string | null;
  gender?: number | string | null;
  Gender?: number | string | null;
}

export interface StudentDetails {
  id?: EntityIdentifier;
  Id?: EntityIdentifier;
  sNo?: number | null;
  SNo?: number | null;
  idNo?: string | null;
  IdNo?: string | null;
  firstName?: string | null;
  FirstName?: string | null;
  middleName?: string | null;
  MiddleName?: string | null;
  surName?: string | null;
  SurName?: string | null;
  fullName?: string | null;
  FullName?: string | null;
  gender?: number | string | null;
  Gender?: number | string | null;
  currentClass?: number | string | null;
  CurrentClass?: number | string | null;
  classArmId?: EntityIdentifier;
  ClassArmId?: EntityIdentifier;
  classArm?: TeacherPortalArm | null;
  ClassArm?: TeacherPortalArm | null;
  user?: StudentUser | null;
  User?: StudentUser | null;
}

export interface StudentsAccessment {
  id?: EntityIdentifier;
  Id?: EntityIdentifier;
  yearlyAndTermlyStatusId?: EntityIdentifier;
  YearlyAndTermlyStatusId?: EntityIdentifier;
  subjectId?: EntityIdentifier;
  SubjectId?: EntityIdentifier;
  studentId?: EntityIdentifier;
  StudentId?: EntityIdentifier;
  classArmId?: EntityIdentifier;
  ClassArmId?: EntityIdentifier;
  ca1?: number | null;
  Ca1?: number | null;
  ca2?: number | null;
  Ca2?: number | null;
  ca3?: number | null;
  Ca3?: number | null;
  exams?: number | null;
  Exams?: number | null;
  student?: StudentDetails | null;
  Student?: StudentDetails | null;
}

export interface StudentsAccessmentCasSetting {
  casSetting?: CasSetting | null;
  CasSetting?: CasSetting | null;
  studentsAccessment?: StudentsAccessment[] | null;
  StudentsAccessment?: StudentsAccessment[] | null;
}

export interface AssignedArmsStudentsResponse {
  classLabel?: string | null;
  armTermlyAssessmentStatus?: number | string | null;
  studentDetails?: StudentDetails[] | null;
  StudentDetails?: StudentDetails[] | null;
}

export interface FormMasterReportResponse {
  classLabel?: string | null;
  message?: string | null;
  studentsAccessment?: unknown;
  StudentsAccessment?: unknown;
  statusCode?: number | null;
}

export type ScoreModalContext = {
  armId: string;
  subjectId: string;
  subjectName: string;
  classArm: string;
};

export type AssignmentListMode = 'all' | 'uncompleted-ca-exams' | 'uncompleted-ca';

export type FormMasterPanelMode =
  | 'none'
  | 'students'
  | 'attendance'
  | 'ca-report'
  | 'termly-report'
  | 'class-position'
  | 'third-term';
