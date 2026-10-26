import { FormControl, FormGroup } from '@angular/forms';
import { CurrentClass } from '@shared';
import { ClassCategoryStatus } from '@shared/interfaces';
import { categoryForClass, seniorCategoryValidator, syncClassCategory } from './class-category';

describe('class-category', () => {
  it('derives the category for every junior class and leaves SS to the user', () => {
    expect(categoryForClass(CurrentClass.PlayClass)).toBe(ClassCategoryStatus.PlayClass);
    expect(categoryForClass(CurrentClass.PreNursery)).toBe(ClassCategoryStatus.PreNursery);
    expect(categoryForClass(CurrentClass.Nursery2)).toBe(ClassCategoryStatus.Nursery);
    expect(categoryForClass(CurrentClass.Primary1)).toBe(ClassCategoryStatus.Primary);
    expect(categoryForClass('6')).toBe(ClassCategoryStatus.Primary);
    expect(categoryForClass(CurrentClass.Primary6)).toBe(ClassCategoryStatus.Primary);
    expect(categoryForClass('Jss2')).toBe(ClassCategoryStatus.Jss);
    expect(categoryForClass(CurrentClass.Ss1)).toBeNull();
    expect(categoryForClass('Ss2')).toBeNull();
  });

  function buildForm(currentClass: CurrentClass, category: ClassCategoryStatus) {
    return new FormGroup({
      currentClass: new FormControl(currentClass, { nonNullable: true }),
      classCategory: new FormControl(category, { nonNullable: true, validators: [seniorCategoryValidator] }),
    });
  }

  it('locks junior classes to their derived category', () => {
    const form = buildForm(CurrentClass.Jss2, ClassCategoryStatus.Primary);
    syncClassCategory(form.controls.currentClass, form.controls.classCategory);
    expect(form.controls.classCategory.disabled).toBeTrue();
    expect(form.getRawValue().classCategory).toBe(ClassCategoryStatus.Jss);
  });

  it('requires Science, Art or Commercial for SS and keeps an existing choice', () => {
    const fresh = buildForm(CurrentClass.Ss2, ClassCategoryStatus.Jss);
    syncClassCategory(fresh.controls.currentClass, fresh.controls.classCategory);
    expect(fresh.controls.classCategory.enabled).toBeTrue();
    expect(fresh.controls.classCategory.value).toBe(ClassCategoryStatus.None);
    expect(fresh.invalid).toBeTrue();

    const saved = buildForm(CurrentClass.Ss3, ClassCategoryStatus.Art);
    syncClassCategory(saved.controls.currentClass, saved.controls.classCategory);
    expect(saved.controls.classCategory.value).toBe(ClassCategoryStatus.Art);
    expect(saved.valid).toBeTrue();
  });

  it('unlocks again when moving from a junior class to SS', () => {
    const form = buildForm(CurrentClass.Jss3, ClassCategoryStatus.None);
    syncClassCategory(form.controls.currentClass, form.controls.classCategory);
    form.controls.currentClass.setValue(CurrentClass.Ss1);
    syncClassCategory(form.controls.currentClass, form.controls.classCategory);
    expect(form.controls.classCategory.enabled).toBeTrue();
    expect(form.controls.classCategory.value).toBe(ClassCategoryStatus.None);
  });
});
