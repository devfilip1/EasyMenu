import { ChangeDetectionStrategy, Component, input } from '@angular/core';

// Stub (TDD red phase, T039). Implemented in T040.
@Component({
  selector: 'app-menu-items-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
})
export class MenuItemsListComponent {
  /** Category whose items are listed (loaded with `listMenuItems({ categoryId })`). */
  readonly categoryId = input.required<string>();
}
