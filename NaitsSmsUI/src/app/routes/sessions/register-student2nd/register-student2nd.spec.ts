import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RegisterStudent2nd } from './register-student2nd';

describe('RegisterStudent2nd', () => {
  let component: RegisterStudent2nd;
  let fixture: ComponentFixture<RegisterStudent2nd>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterStudent2nd]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RegisterStudent2nd);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
