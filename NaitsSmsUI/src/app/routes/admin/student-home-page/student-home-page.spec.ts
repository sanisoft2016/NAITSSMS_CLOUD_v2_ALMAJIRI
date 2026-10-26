import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StudentHomePage } from './student-home-page';

describe('StudentHomePage', () => {
  let component: StudentHomePage;
  let fixture: ComponentFixture<StudentHomePage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudentHomePage]
    })
    .compileComponents();

    fixture = TestBed.createComponent(StudentHomePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
