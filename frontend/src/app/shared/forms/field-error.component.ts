import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Error message of one form field. Always rendered and exposed (empty when valid, never
 * display:none/hidden/aria-hidden), so it works as a polite live region: screen readers only
 * announce changes to a region that already existed. The control references it through
 * `aria-describedby` (see `fieldErrorId`).
 */
@Component({
  selector: 'app-field-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'error',
    'aria-live': 'polite',
    '[id]': 'id()',
  },
  template: `{{ message() ?? '' }}`,
})
export class FieldErrorComponent {
  readonly id = input.required<string>();
  readonly message = input<string | null>(null);
}
