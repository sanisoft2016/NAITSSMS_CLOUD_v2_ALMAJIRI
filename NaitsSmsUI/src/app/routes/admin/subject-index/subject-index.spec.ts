import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubjectIndex } from './subject-index';

describe('SubjectIndex', () => {
  let component: SubjectIndex;
  let fixture: ComponentFixture<SubjectIndex>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubjectIndex]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubjectIndex);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
