export type TeacherRegistrationGender = 'Male' | 'Female';

export type TeacherRegistrationEmployeeStatus =
  | 'Teacher'
  | 'Accountant'
  | 'Security'
  | 'Principal'
  | 'Headmaster'
  | 'Others';

export interface TeacherCreateRequest {
  employeeIdNo?: string;
  firstName: string;
  middleName?: string;
  surName?: string;
  userName: string;
  email?: string;
  phoneNumber?: string;
  dateOfBirth?: string | null;
  gender: TeacherRegistrationGender;
  employeeStatus: TeacherRegistrationEmployeeStatus;
  signatureImageString?: string;
}

export interface TeacherCreateResponse {
  message?: string | null;
  userId?: string | null;
}