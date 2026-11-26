/**
 * Small DOM helpers for component specs. They query the rendered component the way a user
 * perceives it: form controls by their <label> (or aria-label), buttons by their accessible name.
 *
 * Conventions the components must follow (agreed in the specs):
 * - every form control has a label (`<label for>`, a wrapping `<label>` or `aria-label`);
 * - every control sits inside a `.field` wrapper that also holds its validation message;
 * - invalid controls get `aria-invalid="true"` after a submit attempt (or once touched);
 * - form-level errors (not tied to a field) are rendered with `role="alert"`.
 */

type Control = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
const CONTROLS = 'input, select, textarea';

/** Collapses whitespace (including the NBSP that Intl puts after "R$"). */
export function normalize(text: string | null | undefined): string {
  return (text ?? '').replace(/\s+/g, ' ').trim();
}

export function textOf(el: Element | null | undefined): string {
  return normalize(el?.textContent);
}

function accessibleName(el: Element): string {
  return normalize(el.getAttribute('aria-label') ?? el.textContent);
}

function controlsLabelled(root: ParentNode, label: RegExp): Control[] {
  const found = new Set<Control>();
  for (const lbl of Array.from(root.querySelectorAll('label'))) {
    if (!label.test(textOf(lbl))) continue;
    const id = lbl.getAttribute('for');
    const control = id
      ? (lbl.ownerDocument.getElementById(id) as Control | null)
      : lbl.querySelector<Control>(CONTROLS);
    if (control) found.add(control);
  }
  for (const control of Array.from(root.querySelectorAll<Control>(CONTROLS))) {
    const aria = control.getAttribute('aria-label');
    if (aria && label.test(normalize(aria))) found.add(control);
  }
  return [...found];
}

/** The single form control whose label matches. Throws when missing or ambiguous. */
export function byLabel<T extends Control = HTMLInputElement>(root: ParentNode, label: RegExp): T {
  const matches = controlsLabelled(root, label);
  if (matches.length === 0) throw new Error(`No form control labelled ${label}`);
  if (matches.length > 1) throw new Error(`More than one form control labelled ${label}`);
  return matches[0] as T;
}

export function queryByLabel<T extends Control = HTMLInputElement>(
  root: ParentNode,
  label: RegExp,
): T | null {
  return (controlsLabelled(root, label)[0] as T | undefined) ?? null;
}

/** First button (or role=button) whose accessible name (aria-label or text) matches. */
export function queryButton(root: ParentNode, name: RegExp): HTMLButtonElement | null {
  const candidates = Array.from(
    root.querySelectorAll<HTMLButtonElement>('button, [role="button"], input[type="submit"]'),
  );
  return candidates.find((b) => name.test(accessibleName(b) || normalize(b.value))) ?? null;
}

export function button(root: ParentNode, name: RegExp): HTMLButtonElement {
  const found = queryButton(root, name);
  if (!found) throw new Error(`No button named ${name}`);
  return found;
}

export function link(root: ParentNode, name: RegExp): HTMLAnchorElement {
  const found = Array.from(root.querySelectorAll<HTMLAnchorElement>('a')).find((a) =>
    name.test(accessibleName(a)),
  );
  if (!found) throw new Error(`No link named ${name}`);
  return found;
}

/** Types into a text control the way a user would (input + change + blur). */
export function typeInto(control: Control, value: string): void {
  control.focus?.();
  control.value = value;
  control.dispatchEvent(new Event('input', { bubbles: true }));
  control.dispatchEvent(new Event('change', { bubbles: true }));
  control.dispatchEvent(new Event('blur'));
  control.dispatchEvent(new FocusEvent('focusout', { bubbles: true }));
}

/** Picks the <option> whose visible text matches (works with [value] and [ngValue]). */
export function chooseOption(select: HTMLSelectElement, optionText: RegExp): void {
  const index = Array.from(select.options).findIndex((o) => optionText.test(textOf(o)));
  if (index < 0) throw new Error(`No option ${optionText} in select`);
  select.selectedIndex = index;
  select.dispatchEvent(new Event('change', { bubbles: true }));
  select.dispatchEvent(new Event('blur'));
}

export function setFiles(input: HTMLInputElement, files: File[]): void {
  Object.defineProperty(input, 'files', { configurable: true, value: files });
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

/** The `.field` wrapper of a control (label + control + its messages). */
export function fieldOf(control: Element): HTMLElement {
  const field = control.closest<HTMLElement>('.field');
  if (!field) throw new Error('Form control is not wrapped in a .field element');
  return field;
}

export function isInvalid(control: Element): boolean {
  return control.getAttribute('aria-invalid') === 'true';
}

export function alertText(root: ParentNode): string {
  return Array.from(root.querySelectorAll('[role="alert"]'))
    .map((el) => textOf(el))
    .join(' ');
}

export function rows(root: ParentNode, testId: string): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(`[data-testid="${testId}"]`));
}

export function rowWith(root: ParentNode, testId: string, text: string): HTMLElement {
  const row = rows(root, testId).find((r) => textOf(r).includes(text));
  if (!row) throw new Error(`No [data-testid="${testId}"] containing "${text}"`);
  return row;
}

/**
 * The <section> whose accessible name (aria-label, or the text of the element referenced by
 * aria-labelledby) matches exactly. Mirrors Playwright's `getByRole('region', { name })`.
 */
export function region(root: ParentNode, name: string): HTMLElement {
  const sections = Array.from(root.querySelectorAll<HTMLElement>('section'));
  const found = sections.find((s) => {
    const label = s.getAttribute('aria-label');
    if (label !== null) return normalize(label) === name;
    const ids = (s.getAttribute('aria-labelledby') ?? '').split(/\s+/).filter(Boolean);
    const text = ids.map((id) => textOf(s.ownerDocument.getElementById(id))).join(' ');
    return normalize(text) === name;
  });
  if (!found) throw new Error(`No <section> named "${name}" (aria-label or aria-labelledby)`);
  return found;
}

// ---------------------------------------------------------------------------------------------
// Form accessibility convention (tasks.md, "Acessibilidade de formulários"). Every form spec
// MUST use these helpers:
// - each field error has its own `id` and the control points to it via `aria-describedby`,
//   with `aria-invalid="true"` while invalid;
// - required controls have `aria-required="true"`; the <form> has `novalidate`;
// - server errors tied to a field show up in that field's error element, inside an
//   `aria-live="polite"` region; general form errors use `role="alert"`;
// - submitting an invalid form moves focus to the first invalid control (DOM order).

/** Elements referenced by the control's `aria-describedby` (missing ids are an error). */
export function describedBy(control: Element): HTMLElement[] {
  const ids = (control.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean);
  return ids.map((id) => {
    const target = control.ownerDocument.getElementById(id);
    if (!target) throw new Error(`aria-describedby points to missing id "${id}"`);
    return target;
  });
}

/** Text of the error element(s) the control describes itself with. */
export function describedErrorText(control: Element): string {
  return describedBy(control)
    .map((el) => textOf(el))
    .join(' ')
    .trim();
}

/** Texts of the control's aria-describedby targets, keyed by id (e.g. a hint before any error). */
export type DescribedSnapshot = Map<string, string>;

export function snapshotDescribedBy(control: Element): DescribedSnapshot {
  const ids = (control.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean);
  return new Map(ids.map((id) => [id, textOf(control.ownerDocument.getElementById(id))]));
}

/**
 * Asserts the control is flagged invalid and linked via aria-describedby to an error message.
 * - with `message`: some described element contains it;
 * - without `message`: pass the `baseline` taken while no error was shown (e.g. right after
 *   render); the error must be a described element that did not exist or was empty then, so a
 *   hint (e.g. "Seu cardápio ficará em /m/…", whose text follows the value) is never mistaken
 *   for the error.
 * Returns the error element.
 */
export function expectFieldError(
  control: Element,
  message?: string,
  baseline: DescribedSnapshot = new Map(),
): HTMLElement {
  if (!isInvalid(control)) throw new Error('Expected aria-invalid="true" on the control');
  const described = describedBy(control);
  if (described.length === 0) throw new Error('Invalid control has no aria-describedby');
  const error = described.find((el) => {
    const text = textOf(el);
    if (text.length === 0) return false;
    if (message) return text.includes(message);
    return !baseline.get(el.id);
  });
  if (!error) {
    throw new Error(
      `No aria-describedby error ${message ? `containing "${message}"` : 'besides the baseline hint'}; got "${describedErrorText(control)}"`,
    );
  }
  return error;
}

export function isRequired(control: Element): boolean {
  return control.getAttribute('aria-required') === 'true';
}

/** True when the element is inside (or is) an `aria-live="polite"` region. */
export function inPoliteLiveRegion(el: Element): boolean {
  return el.closest('[aria-live="polite"]') !== null;
}

/** First control flagged invalid, in DOM order. */
export function firstInvalidControl(root: ParentNode): HTMLElement | null {
  return root.querySelector<HTMLElement>('input[aria-invalid="true"], select[aria-invalid="true"], textarea[aria-invalid="true"]');
}
