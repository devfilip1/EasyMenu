import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Error message of one form field. Always rendered (empty when valid) so it works as a polite
 * live region; the control references it through `aria-describedby` (see `fieldErrorId`).
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
  styles: `
    :host:empty {
      display: none;
    }
  `,
})
export class FieldErrorComponent {
  readonly id = input.required<string>();
  readonly message = input<string | null>(null);
}
