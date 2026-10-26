import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpParams } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { BaseComponent, GenericService } from '@shared';

type SessionStatusValue = 0 | 1;

type AcademicSessionVm = {
  id: string;
  session: string;
  status: SessionStatusValue;
};

type AcademicSessionPayload = {
  id?: string;
  session: string;
  status: SessionStatusValue;
};

type RequestMessage = {
  message?: string;
  data?: unknown;
};

@Component({
  selector: 'app-session-term-initialisation',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
  ],
  templateUrl: './session-term-initialisation.html',
  styleUrl: './session-term-initialisation.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionTermInitialisation extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<AcademicSessionPayload>);
  private readonly fb = inject(FormBuilder);

  readonly displayedColumns = ['sNo', 'session', 'status', 'actions'];
  readonly sessions = signal<AcademicSessionVm[]>([]);
  readonly editingSessionId = signal<string | null>(null);
  readonly selectedTerm = signal<0 | 1 | 2>(0);

  readonly sessionForm = this.fb.nonNullable.group({
    session: ['', [Validators.required, Validators.maxLength(9)]],
    status: [0 as SessionStatusValue, [Validators.required]],
  });

  readonly statusOptions = [
    { value: 0 as SessionStatusValue, label: 'Current' },
    { value: 1 as SessionStatusValue, label: 'Previous' },
  ];

  readonly termOptions = [
    { value: 0 as const, label: 'First Term' },
    { value: 1 as const, label: 'Second Term' },
    { value: 2 as const, label: 'Third Term' },
  ];

  readonly showEditor = signal(false);
  readonly isEditing = computed(() => this.editingSessionId() !== null);
  readonly hasSessions = computed(() => this.sessions().length > 0);
  readonly currentSession = computed(() => this.sessions().find(session => session.status === 0) ?? null);
  //readonly currentSessionLabel = computed(() => this.currentSession()?.session ?? 'Not yet created');

  constructor() {
    super();
  }
  readonly currentSessionLabel = signal('');

  ngOnInit(): void {
    this.loadSessions();
  }

  loadSessions(): void {
    this.withRequestState(this.api.getAnObj('/SessionAndTerm/get-sessions'), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load academic sessions.',
      rethrow: true,
      notifyOnError: 'none',
      onError: error => console.error('Error loading academic sessions:', error),
    }).subscribe({
      next: response => {
        this.sessions.set(this.normalizeSessions(response.sessions));
        this.currentSessionLabel.set(
          response.currentTerm === 0
            ? 'First Term'
            : response.currentTerm === 1
              ? 'Second Term'
              : response.currentTerm === 2
                ? 'Third Term'
                : 'Not yet created'
        );
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load academic sessions.'), { type: 'error' });
      },
    });
  }

  refreshPage(): void {
    this.loadSessions();
  }

  beginCreate(): void {
    if (this.hasSessions()) {
      return;
    }

    this.editingSessionId.set(null);
    this.showEditor.set(true);
    this.sessionForm.reset({
      session: '',
      status: this.currentSession() ? 1 : 0,
    });
    this.errorMessage.set(null);
  }

  beginEdit(session: AcademicSessionVm): void {
    this.editingSessionId.set(session.id);
    this.showEditor.set(true);
    this.sessionForm.reset({
      session: session.session,
      status: session.status,
    });
    this.errorMessage.set(null);
  }

  cancelEdit(): void {
    this.closeEditor();
  }

  private closeEditor(): void {
    this.showEditor.set(false);
    this.editingSessionId.set(null);
    this.sessionForm.reset({
      session: '',
      status: this.currentSession() ? 1 : 0,
    });
    this.errorMessage.set(null);
  }

  submitSession(): void {
    if (this.sessionForm.invalid) {
      this.validateAllFormFields(this.sessionForm);
      this.showAlert('Enter a valid academic session and status before saving.', { type: 'warning' });
      return;
    }

    const payload: AcademicSessionPayload = {
      session: this.sessionForm.controls.session.value.trim(),
      status: this.sessionForm.controls.status.value,
    };

    const editingId = this.editingSessionId();
    const request$ = editingId
      ? this.api.update(`/SessionAndTerm/update-session/${editingId}`, { ...payload, id: editingId })
      : this.api.create<RequestMessage>('/SessionAndTerm/create-session', payload);

    this.withRequestState(request$, {
      useLoading: true,
      clearError: true,
      errorMessage: editingId ? 'Failed to update academic session.' : 'Failed to create academic session.',
      rethrow: true,
      notifyOnError: 'none',
      onError: error => console.error('Error saving academic session:', error),
    }).subscribe({
      next: (response: AcademicSessionPayload | RequestMessage) => {
        const message = this.readResponseMessage(response)
          ?? (editingId ? 'Academic session updated successfully.' : 'Academic session created successfully.');

        this.showAlert(message, { type: 'success' });
        this.closeEditor();
        this.loadSessions();
      },
      error: error => {
        this.showAlert(
          this.getHttpErrorMessage(
            error,
            editingId ? 'Failed to update academic session.' : 'Failed to create academic session.'
          ),
          { type: 'error' }
        );
      },
    });
  }

  deleteSession(session: AcademicSessionVm): void {
    if (!window.confirm(`Are you sure you want to delete the academic session "${session.session}"?`)) {
      return;
    }

    const sessionId = session.id?.trim();
    if (!sessionId) {
      this.showAlert('This academic session cannot be deleted because its identifier is missing.', { type: 'warning' });
      return;
    }

    this.withRequestState(this.api.delete(`/SessionAndTerm/delete-session/${encodeURIComponent(sessionId)}`), {
      useLoading: true,
      clearError: false,
      errorMessage: 'Failed to delete academic session.',
      rethrow: true,
      notifyOnError: 'none',
      onError: error => console.error('Error deleting academic session:', error),
    }).subscribe({
      next: () => {
        this.showAlert('Academic session deleted successfully.', { type: 'success' });
        if (this.editingSessionId() === session.id) {
          this.closeEditor();
        }
        this.loadSessions();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to delete academic session.'), { type: 'error' });
      },
    });
  }

  prepareAssessmentEnvironment(): void {
    if (!this.currentSession()) {
      this.showAlert("Failed! You need to first set the 'Academic Session' before you can continue.", {
        type: 'error',
      });
      return;
    }

    const term = this.selectedTerm();
    const termLabel = this.termOptions.find(option => option.value === term)?.label ?? 'selected term';

    if (!window.confirm(`Prepare the assessment environment for ${termLabel}?`)) {
      return;
    }

    this.withRequestState(
      this.api.create<RequestMessage>('/SessionAndTerm/student-assessment-system-initial-preparation', {}, {
        params: new HttpParams().set('term', String(term)),
      }),
      {
        useLoading: true,
        clearError: false,
        errorMessage: 'Failed to prepare the assessment environment.',
        rethrow: true,
        notifyOnError: 'none',
        onError: error => console.error('Error preparing assessment environment:', error),
      }
    ).subscribe({
      next: response => {
        this.showAlert(
          this.readResponseMessage(response) ?? 'Assessment environment prepared successfully.',
          { type: 'success', duration: 5000 }
        );
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to prepare the assessment environment.'), {
          type: 'error',
        });
      },
    });
  }

  goBack(): void {
    this.router.navigate(['/SessionAndTerm/other-registrations']);
  }

  updateSelectedTerm(value: number | string): void {
    this.selectedTerm.set(Number(value) as 0 | 1 | 2);
  }

  getStatusLabel(status: SessionStatusValue | string | null | undefined): string {
    if (status === 0 || String(status).toLowerCase() === 'current') {
      return 'Current';
    }

    if (status === 1 || String(status).toLowerCase() === 'previous') {
      return 'Previous';
    }

    return String(status ?? 'Unknown');
  }

  trackBySessionId(_: number, session: AcademicSessionVm): string {
    return session.id;
  }

  private normalizeSessions(response: unknown): AcademicSessionVm[] {
    const collection = Array.isArray(response)
      ? response
      : Array.isArray((response as { data?: unknown[] } | null)?.data)
        ? ((response as { data: unknown[] }).data)
        : [];

    return collection
      .map(item => this.toAcademicSession(item))
      .filter((item): item is AcademicSessionVm => item !== null)
      .sort((left, right) => {
        if (left.status !== right.status) {
          return left.status - right.status;
        }

        return right.session.localeCompare(left.session);
      });
  }

  private toAcademicSession(value: unknown): AcademicSessionVm | null {
    if (!value || typeof value !== 'object') {
      return null;
    }

    const source = value as Record<string, unknown>;
    const id = source['id'];
    const session = source['session'];
    const status = source['status'];

    if (id == null || session == null) {
      return null;
    }

    return {
      id: String(id),
      session: String(session),
      status: this.normalizeStatus(status),
    };
  }

  private normalizeStatus(status: unknown): SessionStatusValue {
    if (status === 0 || String(status).toLowerCase() === 'current') {
      return 0;
    }

    return 1;
  }

  private readResponseMessage(response: unknown): string | null {
    if (!response || typeof response !== 'object') {
      return null;
    }

    const message = (response as RequestMessage).message;
    return typeof message === 'string' && message.trim().length > 0 ? message : null;
  }
}