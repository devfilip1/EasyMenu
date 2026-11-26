import { ChangeDetectionStrategy, Component, input } from '@angular/core';

// Stub (TDD red phase, T054). Implemented in T055.
@Component({
  selector: 'app-cart-sheet',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
})
export class CartSheetComponent {
  /** Establishment whose cart is shown and where the order is placed. */
  readonly slug = input.required<string>();
}
