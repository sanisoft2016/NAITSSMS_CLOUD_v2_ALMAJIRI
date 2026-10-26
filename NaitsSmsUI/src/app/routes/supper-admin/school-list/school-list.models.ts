import { AccessStatus } from '@shared';

export type SchoolSearchStatus = '0' | '1' | '2' | '3' | '4' | '5' | '6';

export interface SchoolListUser {
  id?: string;
  userName?: string;
  phoneNumber?: string;
  firstName?: string;
  middleName?: string;
  surName?: string;
  fullName?: string;
  accessStatus?: AccessStatus | number | string;
}

export interface SchoolListAgent {
  id?: string;
  userId?: string;
}

export interface SchoolRegistrationOfficer {
  id?: string;
  userId?: string;
  user?: SchoolListUser;
  agentUser?: SchoolListAgent;
}

export interface SchoolInfoRecord {
  schoolName?: string;
  schoolAddress?: string;
  telephone?: string;
  poAddress?: string;
  emailAddress?: string;
  website?: string;
  motto?: string;
  schoolLogoString?: string;
  schoolRegistrationOfficerId?: string;
  schoolAdminAccountantStatus?: number;
  systemImplementationType?: string;
  /** 0 = Per Term, 1 = Per Student Per Term */
  systemImplementationTypeCode?: number | null;
  schoolRegistrationOfficer?: SchoolRegistrationOfficer;
}

export interface SchoolListForSystemAdmin {
  schoolInfo?: SchoolInfoRecord[];
  noOfSchools?: number;
}

export interface AgentInfoRecord {
  userName?: string;
  email?: string;
  phoneNumber?: string;
  firstName?: string;
  middleName?: string;
  surName?: string;
  fullName?: string;
  fullAddress?: string;
  userId?: string;
}

export interface AgentDetailsResponse {
  schoolName?: string;
  agent?: AgentInfoRecord;
}

export interface SchoolRegistrarDetailsResponse {
  fullName?: string;
  school?: SchoolInfoRecord;
}

/** Legacy SchoolList.cshtml option values are strings "0"…"6". */
export const SCHOOL_SEARCH_OPTIONS: { value: SchoolSearchStatus; label: string }[] = [
  { value: '0', label: 'User name' },
  { value: '1', label: 'First name' },
  { value: '2', label: 'Sur name' },
  { value: '3', label: 'Phone number' },
  { value: '4', label: 'School Name' },
  { value: '5', label: 'System token' },
  { value: '6', label: 'System Id (Key)' },
];
