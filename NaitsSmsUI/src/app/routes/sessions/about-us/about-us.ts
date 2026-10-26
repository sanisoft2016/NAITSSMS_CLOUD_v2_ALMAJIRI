import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SeoService } from '@core';

@Component({
  selector: 'app-about-us',
  standalone: true,
  templateUrl: './about-us.html',
  styleUrl: './about-us.scss',
  imports: [RouterLink],
})
export class AboutUs implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.apply({
      title: 'About Us | NAITS-ASMS by New Age Solutions Limited',
      description:
        'Meet New Age Solutions Limited, the Nigerian team behind NAITS-ASMS, a secure school management system for nursery, primary and secondary schools since 2017.',
      path: '/auth/about',
      keywords:
        'About NAITS-ASMS, About Us, About NAITS-SMS, SMS, school management system, New Age Solutions Limited, Engr. Bashir Sani, ourschoolonline.ng, Nigeria school software, school result processing, student attendance, hard copy register, school fees, payroll',
    });
  }
}
