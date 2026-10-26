export enum DailyAttendanceStatus {
  Present = 0,
  Absent = 1,
  Late = 2,
  Excused = 3,
}

export interface DailyAttendanceRosterEntry {
  personId: string;
  fullName: string;
  identificationNo: string;
  classArmId: string;
  classArmLabel: string;
  status: DailyAttendanceStatus;
  remark: string;
  alreadyMarked: boolean;
}

export interface DailyAttendanceRoster {
  attendanceDate: string;
  classArmId: string;
  classArmLabel: string;
  registerAlreadyTaken: boolean;
  totalPeople: number;
  presentCount: number;
  absentCount: number;
  lateCount: number;
  excusedCount: number;
  entries: DailyAttendanceRosterEntry[];
}

export interface DailyAttendanceMark {
  personId: string;
  status: DailyAttendanceStatus;
  remark?: string;
}

export interface DailyAttendanceSubmit {
  attendanceDate: string;
  classArmId?: string;
  marks: DailyAttendanceMark[];
}

export interface DailyAttendanceSaveResult {
  success: boolean;
  message: string;
  created: number;
  updated: number;
}

export interface AttendanceArmSummary {
  classArmId: string;
  classArmLabel: string;
  learners: number;
  daysRegistered: number;
  presentMarks: number;
  absentMarks: number;
  lateMarks: number;
  excusedMarks: number;
  attendanceRate: number;
}

export interface AttendanceAtRiskLearner {
  studentId: string;
  fullName: string;
  identificationNo: string;
  classArmLabel: string;
  daysRegistered: number;
  daysPresent: number;
  daysAbsent: number;
  attendanceRate: number;
  longestAbsenceStreak: number;
  lastPresentDate?: string | null;
  riskReason: string;
}

export interface AttendanceDayPoint {
  date: string;
  present: number;
  absent: number;
  attendanceRate: number;
}

export interface AttendanceFacilitatorSummary {
  employeeId: string;
  fullName: string;
  staffNo: string;
  daysRegistered: number;
  daysPresent: number;
  daysAbsent: number;
  attendanceRate: number;
}

export interface DailyAttendanceSummary {
  fromDate: string;
  toDate: string;
  schoolName: string;
  totalLearners: number;
  totalFacilitators: number;
  daysRegistered: number;
  learnerAttendanceRate: number;
  facilitatorAttendanceRate: number;
  termTotalAttendanceDays?: number | null;
  arms: AttendanceArmSummary[];
  atRiskLearners: AttendanceAtRiskLearner[];
  dailyTrend: AttendanceDayPoint[];
  facilitators: AttendanceFacilitatorSummary[];
}

export interface SyncTermAttendanceResult {
  success: boolean;
  message: string;
  learnersUpdated: number;
  rowsCreated: number;
  schoolDaysCounted: number;
}

export interface AttendanceStatusOption {
  value: DailyAttendanceStatus;
  label: string;
  cssClass: string;
}

export const ATTENDANCE_STATUS_OPTIONS: AttendanceStatusOption[] = [
  { value: DailyAttendanceStatus.Present, label: 'Present', cssClass: 'status-present' },
  { value: DailyAttendanceStatus.Absent, label: 'Absent', cssClass: 'status-absent' },
  { value: DailyAttendanceStatus.Late, label: 'Late', cssClass: 'status-late' },
  { value: DailyAttendanceStatus.Excused, label: 'Excused', cssClass: 'status-excused' },
];

/** Local date as YYYY-MM-DD; never use toISOString here or the date shifts by a timezone. */
export function toDateOnly(value: Date | string | null | undefined): string {
  if (!value) {
    return '';
  }

  if (typeof value === 'string') {
    const isoMatch = /^(\d{4}-\d{2}-\d{2})/.exec(value.trim());
    if (isoMatch) {
      return isoMatch[1];
    }
  }

  const date = value instanceof Date ? value : new Date(String(value));
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function parseDateOnly(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec((value ?? '').trim());
  if (!match) {
    return null;
  }

  const date = new Date(+match[1], +match[2] - 1, +match[3]);
  return Number.isNaN(date.getTime()) ? null : date;
}
