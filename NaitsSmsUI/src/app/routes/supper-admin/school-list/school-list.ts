import { HttpParams } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { environment } from '@env/environment';

import { AccessStatus, BaseComponent, GenericService } from '@shared';

import {
  AgentDetailsResponse,
  AgentInfoRecord,
  SCHOOL_SEARCH_OPTIONS,
  SchoolInfoRecord,
  SchoolListForSystemAdmin,
  SchoolRegistrarDetailsResponse,
  SchoolSearchStatus,
} from './school-list.models';

type AdminAccountantDetails = {
  id?: string;
  userId?: string;
  employeeStatus?: string;
  schoolInfo?: { schoolName?: string };
  user?: {
    id?: string;
    userName?: string;
    fullName?: string;
    firstName?: string;
    middleName?: string;
    surName?: string;
    accessStatus?: AccessStatus | number | string;
  };
};

@Component({
  selector: 'app-school-list',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './school-list.html',
  styleUrl: './school-list.scss',
})
export class SchoolList extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<any>);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;

  readonly searchOptions = SCHOOL_SEARCH_OPTIONS;
  readonly AccessStatus = AccessStatus;

  schools: SchoolInfoRecord[] = [];
  noOfSchools = 0;
  searchParameter = '';
  /** Bound as string like legacy `#SearchStatus` option values ("0"…"6"). */
  searchStatus: SchoolSearchStatus = '0';
  hasLoaded = false;

  showAgentModal = false;
  agentSchoolName = '';
  agentDetails: AgentInfoRecord | null = null;

  showSchoolModal = false;
  schoolDetailsFullName = '';
  schoolDetails: SchoolInfoRecord | null = null;

  showAccountantModal = false;
  showAccountantEditForm = false;
  accountantSchoolId = '';
  accountantDetails: AdminAccountantDetails | null = null;
  accountantEdit = {
    firstName: '',
    middleName: '',
    surName: '',
    password: '',
    confirmPassword: '',
  };

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadSchoolList();
  }

  loadSchoolList(): void {
    this.withRequestState(this.api.get<SchoolListForSystemAdmin>('/SuperAdmin/get-school-list-for-system-admin'), {
      errorMessage: 'Failed to load school list.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.hasLoaded = true;
      this.noOfSchools = result?.noOfSchools ?? 0;
      this.schools = result?.schoolInfo ?? [];
    });
  }

  /**
   * Legacy SchoolList.cshtml: $(document).on("change", "#SearchStatus", …)
   * searchStatus: 0 UserName … 5 System token, 6 System Id (Key).
   */
  onSearchStatusChange(event?: Event): void {
    if (event?.target instanceof HTMLSelectElement) {
      // Native option values are strings "0"…"6" (legacy SchoolList.cshtml).
      const selected = String(event.target.value ?? '').trim();
      if (selected === '0' || selected === '1' || selected === '2' || selected === '3'
          || selected === '4' || selected === '5' || selected === '6') {
        this.searchStatus = selected;
      }
    }

    const searchParameter = (this.searchParameter ?? '').trim();
    if (searchParameter === '') {
      this.showAlert('Search Parameter must be provided to continue.', { type: 'warning' });
      queueMicrotask(() => document.getElementById('parameter')?.focus());
      return;
    }

    // Token / Key fingerprints: strip whitespace so "^^" is not searched as "^ ^".
    const status = this.searchStatus;
    const parameterForSearch =
      status === '5' || status === '6'
        ? searchParameter.replace(/\s+/g, '')
        : searchParameter;

    const params = new HttpParams()
      .set('searchParameter', parameterForSearch)
      .set('searchStatus', status);

    this.withRequestState(this.api.get<SchoolInfoRecord[]>('/SuperAdmin/search-school-info', { params }), {
      errorMessage: 'Failed to search schools.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.schools = result ?? [];
    });
  }

  officerFullName(school: SchoolInfoRecord): string {
    const user = school.schoolRegistrationOfficer?.user;
    if (!user) return '-';
    if (user.fullName?.trim()) return user.fullName.trim();
    return [user.firstName, user.middleName, user.surName].filter(Boolean).join(' ').trim() || '-';
  }

  officerUserName(school: SchoolInfoRecord): string {
    return school.schoolRegistrationOfficer?.user?.userName || '-';
  }

  officerPhone(school: SchoolInfoRecord): string {
    return school.schoolRegistrationOfficer?.user?.phoneNumber || '-';
  }

  officerUserId(school: SchoolInfoRecord): string {
    return school.schoolRegistrationOfficer?.userId
      || school.schoolRegistrationOfficer?.user?.id
      || '';
  }

  officerId(school: SchoolInfoRecord): string {
    return school.schoolRegistrationOfficer?.id
      || school.schoolRegistrationOfficerId
      || '';
  }

  agentUserId(school: SchoolInfoRecord): string {
    return school.schoolRegistrationOfficer?.agentUser?.userId
      || school.schoolRegistrationOfficer?.agentUser?.id
      || '';
  }

  isAccountDisabled(school: SchoolInfoRecord): boolean {
    const status = school.schoolRegistrationOfficer?.user?.accessStatus;
    return status === AccessStatus.Disable || status === 1 || status === 'Disable' || status === '1';
  }

  implementationTypeCode(school: SchoolInfoRecord): number | null {
    const code = school.systemImplementationTypeCode;
    if (code === 0 || code === 1) return code;
    return null;
  }

  onImplementationTypeChange(school: SchoolInfoRecord, value: string): void {
    if (!value) return;
    const parts = value.split(',');
    if (parts.length < 2) return;

    const currentCode = this.implementationTypeCode(school);
    if (currentCode !== null && `${this.officerId(school)},${currentCode}` === value) {
      return;
    }

    const params = new HttpParams().set('conCatParameters', value);
    this.withRequestState(this.api.get<boolean>('/SuperAdmin/change-system-implementation-type', { params }), {
      errorMessage: 'Failed! Unable to complete the process.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(data => {
      if (data === true) {
        school.systemImplementationTypeCode = Number(parts[1]) as 0 | 1;
        this.showAlert('The status has successfully been updated.', { type: 'success' });
      } else {
        this.showAlert('Failed! Unable to complete the process.', { type: 'warning' });
      }
    });
  }

  openAgentInfo(school: SchoolInfoRecord): void {
    const id = this.agentUserId(school);
    if (!id) {
      this.showAlert('Agent information is not available for this school.', { type: 'warning' });
      return;
    }

    const schoolName = school.schoolName ?? '';
    const params = new HttpParams().set('schoolName', schoolName);
    this.withRequestState(
      this.api.get<AgentDetailsResponse>(`/SuperAdmin/get-agent-info-byId/${encodeURIComponent(id)}`, { params }),
      {
        errorMessage: 'Failed to load agent details.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(result => {
      this.agentSchoolName = result?.schoolName || schoolName;
      this.agentDetails = result?.agent ?? null;
      this.showAgentModal = true;
    });
  }

  closeAgentModal(): void {
    this.showAgentModal = false;
    this.agentDetails = null;
    this.agentSchoolName = '';
  }

  openSchoolDetails(school: SchoolInfoRecord): void {
    const userId = this.officerUserId(school);
    if (!userId) {
      this.showAlert('School registrar id is missing.', { type: 'warning' });
      return;
    }

    const fullName = this.officerFullName(school);
    const params = new HttpParams().set('fullName', fullName);
    this.withRequestState(
      this.api.get<SchoolRegistrarDetailsResponse>(
        `/SuperAdmin/get-school-details-byId/${encodeURIComponent(userId)}`,
        { params },
      ),
      {
        errorMessage: 'Failed to load school details.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(result => {
      this.schoolDetailsFullName = result?.fullName || fullName;
      this.schoolDetails = result?.school ?? school;
      this.showSchoolModal = true;
    });
  }

  closeSchoolModal(): void {
    this.showSchoolModal = false;
    this.schoolDetails = null;
    this.schoolDetailsFullName = '';
  }

  enableRegistrarAccount(school: SchoolInfoRecord): void {
    const userId = this.officerUserId(school);
    if (!userId) return;

    this.withRequestState(
      this.api.get<SchoolInfoRecord[]>(`/SuperAdmin/enable-school-users-by-registrar-id/${encodeURIComponent(userId)}`),
      {
        errorMessage: 'Failed! We were unable to complete the request.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(list => {
      this.schools = list ?? [];
      this.showAlert('The user has successfully been Enabled.', { type: 'success' });
    });
  }

  disableRegistrarAccount(school: SchoolInfoRecord): void {
    const userId = this.officerUserId(school);
    if (!userId) return;

    this.withRequestState(
      this.api.get<SchoolInfoRecord[]>(`/SuperAdmin/disable-school-users-by-registrar-id/${encodeURIComponent(userId)}`),
      {
        errorMessage: 'Failed! We were unable to complete the request.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(list => {
      this.schools = list ?? [];
      this.showAlert('The user has successfully been disabled.', { type: 'success' });
    });
  }

  deleteRecord(school: SchoolInfoRecord): void {
    const userId = this.officerUserId(school);
    const fullName = this.officerFullName(school);
    if (!userId) return;

    if (!confirm(`Are you sure you want to DELETE  "${fullName}"? If 'Yes',  click 'Ok' to continue.`)) {
      return;
    }

    this.withRequestState(
      this.api.create<number>(`/SuperAdmin/delete-school-registrar-byId/${encodeURIComponent(userId)}`, {}),
      {
        errorMessage: 'Failed! Unable to complete the process.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(data => {
      if (data === 0) {
        this.showAlert('Record successfully Deleted.', { type: 'success' });
        this.loadSchoolList();
      } else if (data === 1) {
        this.showAlert(
          'Failed! The Registrar has registered student(s) and therefore we cannot DELETE the Registrar.',
          { type: 'warning' },
        );
      } else if (data === 2) {
        this.showAlert(
          'Failed! The Registrar has registered Class(es) and therefore we cannot DELETE the Registrar.',
          { type: 'warning' },
        );
      } else if (data === 3) {
        this.showAlert(
          'Failed! The Registrar has registered Subject(s) and therefore we cannot DELETE the Registrar.',
          { type: 'warning' },
        );
      } else {
        this.showAlert('Failed! Unable to complete the process.', { type: 'warning' });
      }
    });
  }

  editSchoolRegistrar(school: SchoolInfoRecord): void {
    const userId = this.officerUserId(school);
    if (!userId) return;
    this.router.navigate(['/supper-admin/edit-school-registrar'], { queryParams: { userId } });
  }

  openSmsAndMore(school: SchoolInfoRecord): void {
    const id = this.officerId(school);
    if (!id) return;
    this.router.navigate(['/supper-admin/school-sms-index', id]);
  }

  openLicensing(school: SchoolInfoRecord): void {
    const id = this.officerId(school);
    if (!id) return;
    this.router.navigate(['/supper-admin/licensing-index', id]);
  }

  createAdminAccountant(schoolId: string | undefined): void {
    if (!schoolId) return;
    this.closeSchoolModal();
    this.router.navigate(['/supper-admin/create-admin-accountant'], {
      queryParams: { schoolId },
    });
  }

  createSubordinateAccountant(schoolId: string | undefined): void {
    if (!schoolId) return;
    this.closeSchoolModal();
    this.router.navigate(['/supper-admin/create-subordinate-accountant'], {
      queryParams: { schoolId },
    });
  }

  viewAdminAccountant(schoolId: string | undefined): void {
    if (!schoolId) return;

    this.withRequestState(
      this.api.getAnObj<AdminAccountantDetails>(
        `/SuperAdmin/get-admin-accountant-details-by-schoolId/${encodeURIComponent(schoolId)}`,
      ),
      {
        errorMessage: 'Failed to load admin accountant details.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(details => {
      if (!details) {
        this.showAlert('Admin accountant was not found for this school.', { type: 'warning' });
        return;
      }

      this.accountantSchoolId = schoolId;
      this.accountantDetails = details;
      this.showAccountantModal = true;
      this.showAccountantEditForm = false;
    });
  }

  closeAccountantModal(): void {
    this.showAccountantModal = false;
    this.showAccountantEditForm = false;
    this.accountantDetails = null;
    this.accountantSchoolId = '';
    this.accountantEdit = { firstName: '', middleName: '', surName: '', password: '', confirmPassword: '' };
  }

  accountantFullName(details: AdminAccountantDetails | null): string {
    const user = details?.user;
    if (!user) return '-';
    if (user.fullName?.trim()) return user.fullName.trim();
    return [user.firstName, user.middleName, user.surName].filter(Boolean).join(' ').trim() || '-';
  }

  isAccountantDisabled(details: AdminAccountantDetails | null): boolean {
    const status = details?.user?.accessStatus;
    return status === AccessStatus.Disable || status === 1 || status === 'Disable' || status === '1';
  }

  enableAdminAccountantAccount(): void {
    const userId = this.accountantDetails?.user?.id || this.accountantDetails?.userId;
    if (!userId) return;

    this.withRequestState(
      this.api.get<boolean>(`/SuperAdmin/enable-accountant-user-byId/${encodeURIComponent(userId)}`),
      {
        errorMessage: 'Failed! We were unable to complete the process.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(ok => {
      if (ok === true) {
        this.showAlert('The user has successfully been enabled.', { type: 'success' });
        this.closeAccountantModal();
        this.closeSchoolModal();
        this.loadSchoolList();
      } else {
        this.showAlert('Failed! We were unable to complete the process.', { type: 'warning' });
      }
    });
  }

  disableAdminAccountantAccount(): void {
    const userId = this.accountantDetails?.user?.id || this.accountantDetails?.userId;
    if (!userId) return;

    this.withRequestState(
      this.api.get<boolean>(`/SuperAdmin/disable-accountant-user-byId/${encodeURIComponent(userId)}`),
      {
        errorMessage: 'Failed! We were unable to complete the process.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(ok => {
      if (ok === true) {
        this.showAlert('The user has successfully been disabled.', { type: 'success' });
        this.closeAccountantModal();
        this.closeSchoolModal();
        this.loadSchoolList();
      } else {
        this.showAlert('Failed! We were unable to complete the process.', { type: 'warning' });
      }
    });
  }

  openEditAdminAccountant(): void {
    const name = this.accountantFullName(this.accountantDetails);
    const parts = name.split(/\s+/).filter(Boolean);
    this.accountantEdit = {
      firstName: parts[0] || '',
      middleName: parts.length === 3 ? parts[1] : '',
      surName: parts.length >= 2 ? parts[parts.length - 1] : '',
      password: '',
      confirmPassword: '',
    };
    this.showAccountantEditForm = true;
  }

  submitAccountantChanges(): void {
    if (!this.accountantSchoolId) return;
    if (!this.accountantEdit.firstName.trim()) {
      this.showAlert('First Name must be entered to continue.', { type: 'warning' });
      return;
    }
    if (!this.accountantEdit.surName.trim()) {
      this.showAlert('Sur Name must be entered to continue.', { type: 'warning' });
      return;
    }
    const password = this.accountantEdit.password.trim();
    const confirm = this.accountantEdit.confirmPassword.trim();
    if ((password && password !== confirm) || (confirm && confirm !== password)) {
      this.showAlert('Password mis-matched.', { type: 'warning' });
      return;
    }

    this.withRequestState(
      this.api.create<boolean>(
        `/user-registration/update-admin-accountant-by-schoolId/${encodeURIComponent(this.accountantSchoolId)}`,
        {
          schoolRegId: this.accountantSchoolId,
          firstName: this.accountantEdit.firstName.trim(),
          middleName: this.accountantEdit.middleName.trim(),
          surName: this.accountantEdit.surName.trim(),
          password: password || null,
          confirmPassword: confirm || null,
        },
      ),
      {
        errorMessage: 'Failed! Unable to complete the process.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(ok => {
      if (ok === true) {
        this.showAlert('The changes has been succcessfully saved.', { type: 'success' });
        this.closeAccountantModal();
        this.closeSchoolModal();
        this.loadSchoolList();
      } else {
        this.showAlert('Failed! Unable to complete the process.', { type: 'warning' });
      }
    });
  }

  deleteAdminAccountantInfo(): void {
    const id = this.accountantDetails?.id;
    const userId = this.accountantDetails?.user?.id || this.accountantDetails?.userId;
    const fullName = this.accountantFullName(this.accountantDetails);
    if (!id || !userId) return;

    if (!confirm(`Are you sure you want to DELETE  "${fullName}"? If 'Yes',  click 'Ok' to continue.`)) {
      return;
    }

    this.withRequestState(
      this.api.create<boolean>(`/SuperAdmin/delete-all-accountants-byId/${encodeURIComponent(id)}`, {}),
      {
        errorMessage: 'Failed! Unable to complete the process.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(ok => {
      if (ok === true) {
        this.showAlert('Record successfully Deleted.', { type: 'success' });
        this.closeAccountantModal();
        this.closeSchoolModal();
        this.loadSchoolList();
      } else {
        this.showAlert('Failed! Unable to complete the process.', { type: 'warning' });
      }
    });
  }

  resolveLogoUrl(fileName?: string): string {
    if (!fileName) return '';
    if (fileName.startsWith('http')) return fileName;
    return `${this.apiOrigin}/school-logos/${fileName}`;
  }

  agentFullName(agent: AgentInfoRecord | null): string {
    if (!agent) return '-';
    if (agent.fullName?.trim()) return agent.fullName.trim();
    return [agent.firstName, agent.middleName, agent.surName].filter(Boolean).join(' ').trim() || '-';
  }

  trackBySchool(index: number, school: SchoolInfoRecord): string {
    return `${index}:${school.schoolRegistrationOfficerId ?? ''}:${school.schoolName ?? ''}`;
  }

  printSchoolDetails(): void {
    window.print();
  }
}
