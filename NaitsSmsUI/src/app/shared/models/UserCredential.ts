export interface UserCredential {
  token: string;
  expiration: string;
  /** Login roles returned by the API (may contain more than one). */
  role: string[];
  userFullName: string;
  userName?: string;
}

/** Accepts a string (legacy) or string[] and returns a cleaned role list. */
export function normalizeCredentialRoles(role: string | string[] | null | undefined): string[] {
  if (Array.isArray(role)) {
    return role.map(entry => String(entry ?? '').trim()).filter(Boolean);
  }

  if (typeof role === 'string' && role.trim()) {
    return [role.trim()];
  }

  return [];
}

export function credentialHasRole(
  credential: { role?: string | string[] } | null | undefined,
  expected: string,
): boolean {
  const needle = expected.trim().toLowerCase();
  if (!needle) {
    return false;
  }

  return normalizeCredentialRoles(credential?.role).some(
    entry => entry.toLowerCase() === needle,
  );
}

/** Teacher portal roles (standard Teacher + CBT teacher). */
export function isTeacherLikeRole(role: string | null | undefined): boolean {
  const value = String(role ?? '').trim().toLowerCase();
  return value === 'teacher' || value === 'cbtteacher';
}

/** Student / Parent portal role from Identity seeding. */
export function isStudentOrParentRole(role: string | null | undefined): boolean {
  const value = String(role ?? '').trim().toLowerCase();
  return value === 'studentorparent';
}

export function credentialHasTeacherAccess(
  credential: { role?: string | string[] } | null | undefined,
): boolean {
  return normalizeCredentialRoles(credential?.role).some(isTeacherLikeRole);
}

export function credentialHasStudentOrParentAccess(
  credential: { role?: string | string[] } | null | undefined,
): boolean {
  return normalizeCredentialRoles(credential?.role).some(isStudentOrParentRole);
}

/** Role-based landing route after sign-in (or return from the public front pages). */
export function resolvePortalHomeUrl(roles: string | string[] | null | undefined): string {
  const list = normalizeCredentialRoles(roles);
  const hasRole = (name: string) =>
    list.some(role => role.toLowerCase() === name.toLowerCase());

  if (hasRole('Commission')) {
    return '/commission';
  }
  if (hasRole('SAOfficer')) {
    return '/supper-admin';
  }
  if (hasRole('Registry')) {
    return '/dashboard';
  }
  if (hasRole('Accounting')) {
    return '/accounting/income-statement';
  }
  if (list.some(isTeacherLikeRole)) {
    return '/teacher';
  }
  if (list.some(isStudentOrParentRole)) {
    return '/student';
  }

  return '/dashboard';
}
