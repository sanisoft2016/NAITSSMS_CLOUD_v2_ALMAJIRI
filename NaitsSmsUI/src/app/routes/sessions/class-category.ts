import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { CurrentClass } from '@shared';
import { ClassCategoryStatus } from '@shared/interfaces';

/** The only categories an SS learner can be registered under; staff must pick one. */
export const SENIOR_CATEGORY_OPTIONS = [
  { value: ClassCategoryStatus.Science, label: 'Science' },
  { value: ClassCategoryStatus.Art, label: 'Art' },
  { value: ClassCategoryStatus.Commercial, label: 'Commercial' },
];

/** Accepts the enum number, a numeric string ("6") or the enum name ("Ss2"). */
export function toCurrentClass(value: unknown): CurrentClass | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  const numericValue = Number(value);
  if (Number.isInteger(numericValue) && CurrentClass[numericValue as CurrentClass] !== undefined) {
    return numericValue as CurrentClass;
  }

  const name = Object.keys(CurrentClass).find(key => key.toLowerCase() === String(value).trim().toLowerCase());
  return name ? CurrentClass[name as keyof typeof CurrentClass] : null;
}

export function isSeniorClass(value: unknown): boolean {
  const currentClass = toCurrentClass(value);
  return currentClass !== null && currentClass >= CurrentClass.Ss1;
}

export function isSeniorCategory(value: unknown): boolean {
  return SENIOR_CATEGORY_OPTIONS.some(option => option.value === Number(value));
}

/** The category a class implies, or null for SS classes where staff choose Science, Art or Commercial. */
export function categoryForClass(value: unknown): ClassCategoryStatus | null {
  const currentClass = toCurrentClass(value);
  if (currentClass === null || currentClass >= CurrentClass.Ss1) {
    return null;
  }
  if (currentClass >= CurrentClass.Jss1) {
    return ClassCategoryStatus.Jss;
  }
  if (currentClass >= CurrentClass.Primary1) {
    return ClassCategoryStatus.Primary;
  }
  if (currentClass >= CurrentClass.Nursery1) {
    return ClassCategoryStatus.Nursery;
  }
  return currentClass === CurrentClass.PreNursery ? ClassCategoryStatus.PreNursery : ClassCategoryStatus.PlayClass;
}

/** Rejects anything other than Science, Art or Commercial while the form's class is an SS class. */
export const seniorCategoryValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const currentClass = control.parent?.get('currentClass')?.value;
  return isSeniorClass(currentClass) && !isSeniorCategory(control.value) ? { seniorCategoryRequired: true } : null;
};

/**
 * Sets the category from the class and locks it; for SS classes unlocks it and keeps an existing
 * Science/Art/Commercial choice, otherwise resets it so staff must choose.
 */
export function syncClassCategory(currentClassControl: AbstractControl, categoryControl: AbstractControl): void {
  const autoCategory = categoryForClass(currentClassControl.value);

  if (autoCategory !== null) {
    categoryControl.setValue(autoCategory, { emitEvent: false });
    categoryControl.disable({ emitEvent: false });
    return;
  }

  categoryControl.enable({ emitEvent: false });
  if (!isSeniorCategory(categoryControl.value)) {
    categoryControl.setValue(ClassCategoryStatus.None, { emitEvent: false });
  }
  categoryControl.updateValueAndValidity({ emitEvent: false });
}
