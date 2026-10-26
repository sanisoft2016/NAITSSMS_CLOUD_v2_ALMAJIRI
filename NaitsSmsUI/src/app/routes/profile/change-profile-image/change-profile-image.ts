import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { AuthService } from '@core';
import { environment } from '@env/environment';
import { BaseComponent, GenericService, ImageCompressionService } from '@shared';

type ProfileImageResponse = {
  status?: string;
  Status?: string;
  data?: { avatar?: string; Avatar?: string; message?: string };
  Data?: { avatar?: string; Avatar?: string; message?: string };
  message?: string;
};

@Component({
  selector: 'app-change-profile-image',
  standalone: true,
  templateUrl: './change-profile-image.html',
  styleUrl: './change-profile-image.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule],
})
export class ChangeProfileImage extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<unknown>);
  private readonly auth = inject(AuthService);
  private readonly imageCompression = inject(ImageCompressionService);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;
  private readonly avatarMaxKb = 100;

  readonly previewUrl = signal('images/avatar-default.jpg');
  readonly isCompressing = signal(false);
  private pendingFile: File | null = null;

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.auth.refreshUser().subscribe({
      next: user => {
        this.previewUrl.set(this.resolveAvatarUrl(user?.avatar));
        this.cdr.markForCheck();
      },
    });
  }

  async onImageSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const looksLikeImage =
      file.type.startsWith('image/') ||
      ['image/jpeg', 'image/png', 'image/jpg'].includes(file.type) ||
      /\.(jpe?g|png)$/i.test(file.name);
    if (!looksLikeImage) {
      this.showAlert('Only JPEG and PNG images are allowed.', { type: 'error' });
      input.value = '';
      return;
    }

    this.isCompressing.set(true);
    this.cdr.markForCheck();

    try {
      const compressed = await this.imageCompression.compressImageFile(file, {
        maxKB: this.avatarMaxKb,
        maxDimension: 640,
        fileName: 'profile-avatar.jpg',
      });

      this.pendingFile = compressed;
      const reader = new FileReader();
      reader.onload = () => {
        this.previewUrl.set(String(reader.result ?? ''));
        this.cdr.markForCheck();
      };
      reader.readAsDataURL(compressed);
    } catch (error) {
      this.pendingFile = null;
      input.value = '';
      this.showAlert(
        error instanceof Error
          ? error.message
          : `Unable to reduce profile image below ${this.avatarMaxKb}KB. Please use a smaller image.`,
        { type: 'warning' },
      );
    } finally {
      this.isCompressing.set(false);
      this.cdr.markForCheck();
    }
  }

  save(): void {
    if (this.isCompressing()) {
      this.showAlert('Please wait for image compression to finish.', { type: 'warning' });
      return;
    }

    if (!this.pendingFile) {
      this.showAlert('Please choose a profile image first.', { type: 'warning' });
      return;
    }

    if (!this.startSubmitting()) {
      return;
    }

    const formData = new FormData();
    formData.append('profileImage', this.pendingFile, this.pendingFile.name);

    this.withRequestState(this.api.createWithFile('/user/profile-image', formData), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Failed to update profile image.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: (response: ProfileImageResponse) => {
        this.stopSubmitting();
        const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
        const payload = response?.data ?? response?.Data ?? {};
        const avatar = String(payload.avatar ?? payload.Avatar ?? '').trim();
        const message = String(payload.message ?? response?.message ?? '').trim();

        if (status !== 'success') {
          this.showAlert(message || 'Failed to update profile image.', { type: 'error' });
          this.cdr.markForCheck();
          return;
        }

        if (avatar) {
          this.previewUrl.set(this.resolveAvatarUrl(avatar));
        }

        this.pendingFile = null;
        this.auth.refreshUser().subscribe();
        this.showAlert(message || 'Profile image updated successfully.', { type: 'success' });
        this.cdr.markForCheck();
      },
      error: error => {
        this.stopSubmitting();
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to update profile image.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  private resolveAvatarUrl(avatar?: string | null): string {
    const value = String(avatar ?? '').trim();
    if (!value) {
      return 'images/avatar-default.jpg';
    }

    if (value.startsWith('http') || value.startsWith('data:') || value.startsWith('images/')) {
      return value;
    }

    return `${this.apiOrigin}${value.startsWith('/') ? value : `/${value}`}`;
  }
}
