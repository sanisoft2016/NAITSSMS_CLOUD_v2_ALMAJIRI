import { Routes } from '@angular/router';
import { ClassRegIndex } from './class-reg-index/class-reg-index';
import { ClassArmIndex } from './class-arm-index/class-arm-index';
import { ClassReg } from './class-reg/class-reg';
import { ClassArmReg } from './class-arm-reg/class-arm-reg';
import { Index } from './index/index';
import { SchoolRegistration } from './school-registration/school-registration';
import { SmsIndex } from './sms-index/sms-index';
import { StudentResults } from './student-results/student-results';
import { StudentRegIndex } from './student-reg-index/student-reg-index';
import { StudentHomePage } from './student-home-page/student-home-page';
import { EditStudent } from './edit-student/edit-student';
import { AllSubjectIndex } from './all-subject-index/all-subject-index';
import { SubjectIndex } from './subject-index/subject-index';
import { SubjectReg } from './subject-reg/subject-reg';
import { SubjectRegIndex } from './subject-reg-index/subject-reg-index';
import { SubjectStudentAssignment } from './subject-student-assignment/subject-student-assignment';
import { TransferSubjectAssignment } from './teachers/transfer-subject-assignment';
import { TransferClassArmAssignment } from './teachers/transfer-class-arm-assignment';
import { AssignClassArmIndex } from './teachers/assign-class-arm-index';
import { AssignSubjectIndex } from './teachers/assign-subject-index';
import { CbtUserHandler } from './teachers/cbt-user-handler';
import { Teachers } from './teachers/teachers';
import { FacilitatorRegister } from './daily-attendance/facilitator-register/facilitator-register';
import { AttendanceSummary } from './daily-attendance/attendance-summary/attendance-summary';

export const routes: Routes = [
  { path: '', component: Index },
  { path: 'school-registration', component: SchoolRegistration },
  { path: 'student-results', component: StudentResults },
  { path: 'sms-index', component: SmsIndex },
  { path: 'class-reg-index',     component: ClassRegIndex },
  { path: 'class-reg',           component: ClassReg },
  { path: 'class-arm-index',     component: ClassArmIndex },
  { path: 'class-arm-reg',       component: ClassArmReg },
  { path: 'student-reg-index',   component: StudentRegIndex },
  { path: 'student-home-page',   component: StudentHomePage },
  { path: 'edit-student/:id',    component: EditStudent },
  { path: 'edit-student',        component: EditStudent }, // old query-string links
  { path: 'subjects',            component: SubjectIndex },
  { path: 'subject-index',       component: SubjectIndex },
  { path: 'subject-reg-index',   component: SubjectRegIndex },
  { path: 'subject-reg',         component: SubjectReg },
  { path: 'all-subject-index',   component: AllSubjectIndex },
  { path: 'subject-student-assignment', component: SubjectStudentAssignment },

  { path: 'daily-attendance', redirectTo: 'daily-attendance/summary', pathMatch: 'full' },
  { path: 'daily-attendance/facilitators', component: FacilitatorRegister },
  { path: 'daily-attendance/summary',      component: AttendanceSummary },

  { path: 'teachers',            component: Teachers },
  { path: 'teachers/assign-subject-index', component: AssignSubjectIndex },
  { path: 'teachers/transfer-subject-assignment', component: TransferSubjectAssignment },
  { path: 'teachers/assign-class-arm-index', component: AssignClassArmIndex },
  { path: 'teachers/transfer-class-arm-assignment', component: TransferClassArmAssignment },
  { path: 'teachers/cbt-user-handler', component: CbtUserHandler },
];
