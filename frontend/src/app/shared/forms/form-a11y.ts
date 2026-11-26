import { AbstractControl, FormGroup } from '@angular/forms';

/**
 * Form accessibility convention (tasks.md, "Acessibilidade de formulários"):
 * - each field error has a deterministic id, referenced by the control's `aria-describedby`;
 * - `aria-invalid="true"` while invalid and `aria-required="true"` on required controls;
 * - field errors live in `aria-live="polite"` regions (`<app-field-error>`), general errors in
 *   `role="alert"`;
 * - submitting an invalid form focuses the first invalid control in DOM order.
 */

/** Deterministic id of a field's error element, e.g. `login-email-error`. */
export function fieldErrorId(formId: string, field: string): string {
  return `${formId}-${field}-error`;
}

/** Value for `aria-describedby`, skipping empty entries (`null` when nothing remains). */
export function describedBy(...ids: (string | null | undefined | false)[]): string | null {
  const value = ids.filter(Boolean).join(' ');
  return value || null;
}

/**
 * Focuses the first control (DOM order) of `form` that is invalid or flagged by `extraInvalid`.
 * Controls are matched through their `formControlName` attribute inside `host`.
 * Returns whether a control was focused.
 */
export function focusFirstInvalid(
  form: FormGroup,
  host: HTMLElement,
  extraInvalid: (name: string) => boolean = () => false,
): boolean {
  const elements = host.querySelectorAll<HTMLElement>('[formcontrolname]');
  for (const element of Array.from(elements)) {
    const name = element.getAttribute('formcontrolname') ?? '';
    const control: AbstractControl | null = form.get(name);
    if (control?.invalid || extraInvalid(name)) {
      element.focus();
      return true;
    }
  }
  return false;
}
