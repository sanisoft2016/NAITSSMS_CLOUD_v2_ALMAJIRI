export interface CommissionCentreDirectoryRow {
  centreId: string;
  centreName: string;
  registrationNumber: string;
  location: string;
  state: string;
  lga: string;
  zone: string;
  centreType: string;
  registrarUserId: string;
  learners: number;
  femaleLearners: number;
  maleLearners: number;
  facilitators: number;
}

export interface CommissionCentresDirectory {
  totalCentres: number;
  totalLearners: number;
  totalFemaleLearners: number;
  totalFacilitators: number;
  states: number;
  zones: number;
  centres: CommissionCentreDirectoryRow[];
}

export interface CommissionCentreRow {
  centreId: string;
  centreName: string;
  registrationNumber: string;
  location: string;
  state: string;
  zone: string;
  centreType: string;
  registrarUserId: string;
  learners: number;
  femaleLearners: number;
  facilitators: number;
  daysRegistered: number;
  learnerAttendanceRate: number;
  facilitatorAttendanceRate: number;
  atRiskLearners: number;
  lastRegisterDate?: string | null;
  daysSinceLastRegister: number;
  lastStaffRegisterDate?: string | null;
  staffDaysSinceLastRegister: number;
  classRegisterOverdue: boolean;
  staffRegisterOverdue: boolean;
  registerOverdue: boolean;
  neverSubmitted: boolean;
}

export interface CommissionDayPoint {
  date: string;
  learnersPresent: number;
  learnersAbsent: number;
  learnerAttendanceRate: number;
  facilitatorAttendanceRate: number;
}

export interface CommissionEnrolmentPoint {
  centreId: string;
  centreName: string;
  learners: number;
  facilitators: number;
}

export interface CommissionAtRiskLearner {
  centreId: string;
  centreName: string;
  classArmLabel: string;
  studentId: string;
  fullName: string;
  identificationNo: string;
  attendanceRate: number;
  currentAbsenceStreak: number;
  longestAbsenceStreak: number;
  lastPresentDate?: string | null;
  reason: string;
}

export interface CommissionClassArmAttendanceStatus {
  /** The centre's facilitator register, kept by the centre administrator. */
  isStaffRegister: boolean;
  centreId: string;
  centreName: string;
  classArmId: string;
  className: string;
  classArmName: string;
  formMasterId: string;
  formMasterName: string;
  enrolledLearners: number;
  expectedSubmissions: number;
  submittedDates: number;
  neverSubmitted: boolean;
  missingDates: string[];
}

export interface CommissionDefaultedCentre {
  centreId: string;
  centreName: string;
  missingSubmissionCount: number;
  affectedClassArms: number;
  classArms: CommissionClassArmAttendanceStatus[];
}

export interface CommissionAreaRow {
  name: string;
  zone: string;
  centres: number;
  learners: number;
  femaleLearners: number;
  facilitators: number;
  learnerAttendanceRate: number;
  facilitatorAttendanceRate: number;
  atRiskLearners: number;
  centresOverdue: number;
}

export interface CommissionTransitionSummary {
  leavers: number;
  toFormalSchool: number;
  toVocational: number;
  withdrawn: number;
  transitionRate: number;
}

export interface CommissionOversight {
  fromDate: string;
  toDate: string;
  asAtDate: string;
  /** The school day the "today" figures describe (the last school day at weekends and holidays). */
  reportDate: string;
  totalCentres: number;
  totalLearners: number;
  femaleLearners: number;
  maleLearners: number;
  totalFacilitators: number;
  activeTerms: number;
  learnersPresentToday: number;
  learnersAbsentToday: number;
  learnerAttendanceRateToday: number;
  facilitatorsPresentToday: number;
  facilitatorsAbsentToday: number;
  facilitatorAttendanceRateToday: number;
  centresReportingToday: number;
  learnerAttendanceRate: number;
  femaleAttendanceRate: number;
  maleAttendanceRate: number;
  facilitatorAttendanceRate: number;
  totalAtRiskLearners: number;
  centresWithOverdueRegister: number;
  transitions: CommissionTransitionSummary;
  centres: CommissionCentreRow[];
  statesSummary: CommissionAreaRow[];
  zonesSummary: CommissionAreaRow[];
  dailyTrend: CommissionDayPoint[];
  enrolmentByCentre: CommissionEnrolmentPoint[];
  atRiskLearners: CommissionAtRiskLearner[];
  defaultedCentres: CommissionDefaultedCentre[];
  classArmStatuses: CommissionClassArmAttendanceStatus[];
}

// --- Learning outcomes -----------------------------------------------------------------------

export interface CommissionTermOption {
  session: string;
  term: number;
  label: string;
  complete: boolean;
}

export interface CommissionOutcomeRow {
  key: string;
  name: string;
  detail: string;
  learners: number;
  averageScore: number;
  passRate: number;
  strongestSubject: string;
  weakestSubject: string;
}

export interface CommissionGradeBand {
  grade: string;
  range: string;
  count: number;
}

export interface CommissionOutcomes {
  availableTerms: CommissionTermOption[];
  term: CommissionTermOption | null;
  previousTerm: CommissionTermOption | null;
  centresReporting: number;
  learnersAssessed: number;
  scoresRecorded: number;
  averageScore: number;
  previousAverageScore: number | null;
  passRate: number;
  previousPassRate: number | null;
  passMark: number;
  learnersNeedingSupport: number;
  grades: CommissionGradeBand[];
  centres: CommissionOutcomeRow[];
  subjects: CommissionOutcomeRow[];
  genders: CommissionOutcomeRow[];
  levels: CommissionOutcomeRow[];
  states: CommissionOutcomeRow[];
}

// --- Centre drill-down -----------------------------------------------------------------------

export interface CommissionCentreLevel {
  classArmId: string;
  label: string;
  formMaster: string;
  learners: number;
  female: number;
  male: number;
  attendanceRate: number;
}

export interface CommissionCentreFacilitator {
  employeeId: string;
  staffNumber: string;
  fullName: string;
  gender: string;
  discipline: string;
  qualification: string;
  daysRegistered: number;
  daysAbsent: number;
  attendanceRate: number;
  lastAbsentDate?: string | null;
}

export interface CommissionCentreLearner {
  studentId: string;
  fullName: string;
  identificationNo: string;
  gender: string;
  level: string;
  category: string;
  attendanceRate: number;
  atRisk: boolean;
}

export interface CommissionCentreDetail {
  centreId: string;
  centreName: string;
  registrationNumber: string;
  centreType: string;
  address: string;
  community: string;
  lga: string;
  state: string;
  zone: string;
  head: string;
  telephone: string;
  yearEstablished: number | null;
  currentSession: string;
  currentTerm: string;
  fromDate: string;
  toDate: string;
  learners: number;
  femaleLearners: number;
  facilitators: number;
  daysRegistered: number;
  expectedDays: number;
  lastRegisterDate?: string | null;
  lastStaffRegisterDate?: string | null;
  classRegisterOverdue: boolean;
  staffRegisterOverdue: boolean;
  registerOverdue: boolean;
  learnerAttendanceRate: number;
  facilitatorAttendanceRate: number;
  transitions: CommissionTransitionSummary;
  lastTermOutcome: CommissionOutcomeRow | null;
  lastTermLabel: string;
  levels: CommissionCentreLevel[];
  facilitatorsList: CommissionCentreFacilitator[];
  atRiskLearners: CommissionAtRiskLearner[];
  roll: CommissionCentreLearner[];
  dailyTrend: CommissionDayPoint[];
  subjectOutcomes: CommissionOutcomeRow[];
}

// --- Learner records -------------------------------------------------------------------------

export interface CommissionLearnerSearchRow {
  studentId: string;
  fullName: string;
  identificationNo: string;
  gender: string;
  age: number;
  centreId: string;
  centreName: string;
  state: string;
  level: string;
  status: string;
}

export interface CommissionLearnerSubject {
  subject: string;
  ca1: number;
  ca2: number;
  ca3: number;
  exam: number;
  total: number;
  grade: string;
}

export interface CommissionLearnerTerm {
  session: string;
  term: string;
  level: string;
  complete: boolean;
  daysOpen: number;
  daysPresent: number;
  attendanceRate: number;
  totalScore: number;
  averageScore: number;
  grade: string;
  position: number;
  classSize: number;
  teacherComment: string;
  headComment: string;
  subjects: CommissionLearnerSubject[];
}

export interface CommissionRegisterMark {
  date: string;
  status: string;
  remark: string;
}

export interface CommissionLearnerRecord {
  studentId: string;
  fullName: string;
  identificationNo: string;
  gender: string;
  dateOfBirth: string;
  age: number;
  placeOfBirth: string;
  centreId: string;
  centreName: string;
  centreAddress: string;
  state: string;
  level: string;
  startLevel: string;
  admissionDate: string;
  dischargeDate?: string | null;
  status: string;
  category: string;
  stateOfOrigin: string;
  lgaOfOrigin: string;
  tsangaya: string;
  quranJuzMemorised: number | null;
  priorSchooling: string;
  transitionStatus: string;
  transitionDate?: string | null;
  transitionDestination: string;
  guardianName: string;
  guardianRelationship: string;
  guardianPhone: string;
  guardianAltPhone: string;
  guardianAddress: string;
  guardianOccupation: string;
  currentTermDaysRegistered: number;
  currentTermDaysPresent: number;
  currentTermDaysAbsent: number;
  currentTermDaysLate: number;
  currentAbsenceStreak: number;
  currentTermAttendanceRate: number;
  terms: CommissionLearnerTerm[];
  recentRegister: CommissionRegisterMark[];
}

/** A learner who has left the programme, with where they went. */
export interface CommissionLeaver {
  studentId: string;
  fullName: string;
  identificationNo: string;
  gender: string;
  centreId: string;
  centreName: string;
  state: string;
  lastLevel: string;
  category: string;
  outcome: LeaverOutcome;
  outcomeLabel: string;
  destination: string;
  leftOn?: string | null;
}

export type LeaverOutcome = 'formal' | 'vocational' | 'withdrawn';
