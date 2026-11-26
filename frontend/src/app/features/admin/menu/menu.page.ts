import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { Category } from '../../../api/generated/models/category';
import { CategoriesPanelComponent } from './categories-panel.component';

/** Owner's menu editor: categories and their items. */
@Component({
  selector: 'app-menu-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CategoriesPanelComponent],
  template: `
    <div class="stack">
      <h1>Cardápio</h1>
      <app-categories-panel (categoriesChange)="categories.set($event)" />
    </div>
  `,
})
export class MenuPage {
  protected readonly categories = signal<Category[]>([]);
}
