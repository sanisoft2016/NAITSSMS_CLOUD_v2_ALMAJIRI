import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';

import { BaseComponent, GenericService } from '@shared';

type SchoolOverview = {
  schoolId: string;
  registrationNumber: number;
  schoolName: string;
  registrarUserId: string;
  registrarName: string;
  registrarUserName: string;
  accessStatus: number;
  accountantStatus: number;
  subscriptionExpiryDate?: string | null;
  createdDate?: string | null;
  subscriptionHealth: 'Active' | 'Expiring soon' | 'Expired' | 'No subscription';
  attentionPriority: number;
};

type SuperAdminSummary = {
  totalSchools: number;
  activeSchools: number;
  disabledSchools: number;
  schoolsWithAccountant: number;
  schoolsWithoutAccountant: number;
  activeSubscriptions: number;
  expiringWithin30Days: number;
  expiredSubscriptions: number;
  noSubscription: number;
  totalAgents: number;
  schoolsNeedingAttention: SchoolOverview[];
  recentSchools: SchoolOverview[];
};

@Component({
  selector: 'app-supper-admin-dashboard',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './supper-admin-dashboard.html',
  styleUrl: './supper-admin-dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupperAdminDashboard extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<SuperAdminSummary>);

  constructor() {
    super();
  }

  readonly summary = signal<SuperAdminSummary | null>(null);
  readonly loadFailed = signal(false);

  readonly subscriptionSegments = computed(() => {
    const summary = this.summary();
    if (!summary) return [];

    const total = Math.max(summary.totalSchools, 1);
    return [
      {
        label: 'Healthy',
        value: Math.max(summary.activeSubscriptions - summary.expiringWithin30Days, 0),
        color: '#138a62',
      },
      { label: 'Expiring soon', value: summary.expiringWithin30Days, color: '#e39b25' },
      { label: 'Expired', value: summary.expiredSubscriptions, color: '#d34b5d' },
      { label: 'No subscription', value: summary.noSubscription, color: '#8b93a7' },
    ].map(segment => ({
      ...segment,
      percentage: Math.round((segment.value / total) * 100),
    }));
  });

  readonly subscriptionRing = computed(() => {
    const segments = this.subscriptionSegments();
    let cursor = 0;
    const stops = segments.map(segment => {
      const start = cursor;
      cursor += segment.percentage;
      return `${segment.color} ${start}% ${cursor}%`;
    });
    return `conic-gradient(${stops.join(', ') || '#e7eaf0 0 100%'})`;
  });

  readonly quickActions = [
    {
      label: 'Commission oversight',
      description: 'Attendance accountability across all centres',
      icon: 'insights',
      route: '/supper-admin/commission-dashboard',
      tone: 'indigo',
    },
    {
      label: 'Onboard new school',
      description: 'Create the school and registrar account',
      icon: 'add_business',
      route: '/auth/register-school-admin',
      tone: 'primary',
    },
    {
      label: 'Manage schools',
      description: 'Access, profiles, passwords and accountants',
      icon: 'domain',
      route: '/supper-admin/school-list',
      tone: 'indigo',
    },
    {
      label: 'Register agent',
      description: 'Add a school onboarding agent',
      icon: 'person_add',
      route: '/supper-admin/register-agents',
      tone: 'teal',
    },
    {
      label: 'View agents',
      description: 'Review the agent network',
      icon: 'groups',
      route: '/supper-admin/agent-list',
      tone: 'amber',
    },
  ] as const;

  ngOnInit(): void {
    this.loadSummary();
  }

  refresh(): void {
    this.loadSummary();
  }

  accessLabel(row: SchoolOverview): string {
    return row.accessStatus === 0 ? 'Active' : 'Revoked';
  }

  private loadSummary(): void {
    this.loadFailed.set(false);
    this.withRequestState(
      this.api.getAnObj<SuperAdminSummary>('/SuperAdmin/dashboard/summary'),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'Unable to load the system overview.',
        notifyOnError: 'alert',
        rethrow: false,
        onError: () => {
          this.loadFailed.set(true);
          this.summary.set(null);
          this.cdr.markForCheck();
        },
      },
    ).subscribe(response => {
      this.summary.set(response);
      this.cdr.markForCheck();
    });
  }
}
