import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ClassRegIndex } from './class-reg-index';

describe('ClassRegIndex', () => {
  let component: ClassRegIndex;
  let fixture: ComponentFixture<ClassRegIndex>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClassRegIndex]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ClassRegIndex);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
