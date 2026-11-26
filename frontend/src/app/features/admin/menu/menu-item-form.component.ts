import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Category } from '../../../api/generated/models/category';
import { MenuItem } from '../../../api/generated/models/menu-item';

// Stub (TDD red phase, T038). Implemented in T040.
@Component({
  selector: 'app-menu-item-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
})
export class MenuItemFormComponent {
  /** Categories offered in the "Categoria" select. */
  readonly categories = input<Category[]>([]);
  /** Item being edited; `null` means create. */
  readonly item = input<MenuItem | null>(null);
  /** Emitted with the saved item (also when only the photo upload failed). */
  readonly saved = output<MenuItem>();
}
