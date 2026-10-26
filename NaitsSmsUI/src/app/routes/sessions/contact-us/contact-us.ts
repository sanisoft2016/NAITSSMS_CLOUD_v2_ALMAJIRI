import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { SeoService } from '@core';
import { BaseComponent } from '@shared';
import { GenericService } from '@shared/services/generic.service';

@Component({
  selector: 'app-contact-us',
  standalone: true,
  templateUrl: './contact-us.html',
  styleUrl: './contact-us.scss',
  imports: [ReactiveFormsModule],
})
export class ContactUs extends BaseComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(GenericService<unknown>);

  readonly supportEmail = 'info@naits.com.ng';
  readonly phones = ['+2348035550938', '+2348055068619'];
  readonly website = 'https://ourschoolonline.ng';
  readonly offices = [
    {
      city: 'Abuja',
      address: 'Qubeesh Integrated Services, Plot 1259, Hanan Plaza, 2nd Floor Suite 9, Aminu Kano Crescent, Wuse II, Abuja.',
    }
  ];
  readonly submitted = signal(false);
  readonly submitError = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(160)]],
    school: ['', [Validators.maxLength(160)]],
    message: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(2000)]],
  });

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.seo.apply({
      title: 'Contact NAITS-ASMS',
      description:
        'Contact New Age Solutions Limited for NAITS-ASMS demos, subscriptions and support. Call +2348035550938 or email info@naits.com.ng.',
      path: '/auth/contact',
      keywords:
        'NAITS-ASMS contact, New Age Solutions Limited, ourschoolonline.ng, school software support, info@naits.com.ng, +2348035550938',
    });
  }

  sendMessage(): void {
    this.submitError.set(null);
    this.submitted.set(false);

    if (this.form.invalid || !this.startSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, email, school, message } = this.form.getRawValue();
    const payload = {
      fullName: name.trim(),
      email: email.trim(),
      school: school.trim(),
      subject: school.trim()
        ? `Enquiry from ${school.trim()}`
        : 'NAITS-ASMS website enquiry',
      message: message.trim(),
    };

    this.api
      .create('/Email/contact-enquiry', payload)
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: (response: any) => {
          const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
          const msg =
            response?.message ??
            response?.Message ??
            'Your message has successfully been sent.';
          if (status === 'success') {
            this.submitted.set(true);
            this.form.reset({ name: '', email: '', school: '', message: '' });
            this.showAlert(msg, { type: 'success' });
            return;
          }
          this.submitError.set(msg);
          this.showAlert(msg, { type: 'error' });
        },
        error: error => {
          const msg = this.getHttpErrorMessage(
            error,
            'Unable to send your message. Kindly mail info@naits.com.ng or call 08035550938.'
          );
          this.submitError.set(msg);
          this.showAlert(msg, { type: 'error' });
        },
      });
  }
}
