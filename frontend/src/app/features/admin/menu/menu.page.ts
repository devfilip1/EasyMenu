import { ChangeDetectionStrategy, Component, signal, viewChild } from '@angular/core';
import { Category } from '../../../api/generated/models/category';
import { MenuItem } from '../../../api/generated/models/menu-item';
import { CategoriesPanelComponent } from './categories-panel.component';
import { MenuItemFormComponent } from './menu-item-form.component';
import { MenuItemsListComponent } from './menu-items-list.component';

interface Editor {
  /** Changes on every open, so the form is recreated even for the same item. */
  key: number;
  item: MenuItem | null;
}

/** Owner's menu editor: categories and their items. */
@Component({
  selector: 'app-menu-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CategoriesPanelComponent, MenuItemFormComponent, MenuItemsListComponent],
  template: `
    <div class="stack">
      <div class="row heading">
        <h1>Cardápio</h1>
        <button
          class="btn btn-primary"
          type="button"
          [disabled]="categories().length === 0"
          (click)="open(null)"
        >
          Novo item
        </button>
      </div>

      <app-categories-panel (categoriesChange)="categories.set($event)" />

      @for (editor of editors(); track editor.key) {
        <section class="card stack" aria-labelledby="item-form-title">
          <div class="row heading">
            <h2 id="item-form-title">{{ editor.item ? 'Editar item' : 'Novo item' }}</h2>
            <button class="btn btn-sm" type="button" (click)="close()">Fechar</button>
          </div>
          <app-menu-item-form
            [categories]="categories()"
            [item]="editor.item"
            (saved)="onSaved()"
          />
        </section>
      }

      @for (category of categories(); track category.id) {
        <section class="stack" [attr.aria-label]="'Itens de ' + category.name">
          <h2>{{ category.name }}</h2>
          <app-menu-items-list
            [categoryId]="category.id"
            [refresh]="version()"
            (edit)="open($event)"
          />
        </section>
      }
    </div>
  `,
  styles: `
    .heading {
      justify-content: space-between;
    }
    h1,
    h2 {
      margin: 0;
    }
  `,
})
export class MenuPage {
  protected readonly categories = signal<Category[]>([]);
  protected readonly editors = signal<Editor[]>([]);
  /** Bumped after a save so every items list reloads (the item may have changed category). */
  protected readonly version = signal(0);
  private readonly form = viewChild(MenuItemFormComponent);
  private nextKey = 0;

  protected open(item: MenuItem | null): void {
    this.editors.set([{ key: ++this.nextKey, item }]);
    window.scrollTo?.({ top: 0, behavior: 'smooth' });
  }

  protected close(): void {
    this.editors.set([]);
  }

  protected onSaved(): void {
    this.version.update((v) => v + 1);
    // Keep the form open when only the photo failed, so the owner sees why.
    if (!this.form()?.photoError()) this.close();
  }
}
