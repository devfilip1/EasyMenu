import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { Api } from '../../../api/generated/api';
import { reorderCategoryItems } from '../../../api/generated/fn/categories/reorder-category-items';
import { deleteMenuItem } from '../../../api/generated/fn/menu-items/delete-menu-item';
import { listMenuItems } from '../../../api/generated/fn/menu-items/list-menu-items';
import { setMenuItemAvailability } from '../../../api/generated/fn/menu-items/set-menu-item-availability';
import { MenuItem } from '../../../api/generated/models/menu-item';
import { errorCode, problemMessage } from '../../../core/errors/problem';
import { BrlPipe } from '../../../shared/brl.pipe';
import { MediaUrlPipe } from '../../../shared/media-url.pipe';
import { byPosition, moved } from './reorder';

/** Items of one category, with availability, edit, reorder and delete actions. */
@Component({
  selector: 'app-menu-items-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrlPipe, MediaUrlPipe],
  template: `
    @if (error()) {
      <p class="alert" role="alert">{{ error() }}</p>
    }
    @if (loaded() && items().length === 0) {
      <p class="muted">Nenhum item nesta categoria.</p>
    }
    <ol class="list">
      @for (item of items(); track item.id; let first = $first, last = $last) {
        <li class="list-row" data-testid="menu-item-row">
          <img class="thumb" [src]="item.photoUrl | mediaUrl" [alt]="item.name" loading="lazy" />
          <div class="grow">
            <div class="row">
              <strong>{{ item.name }}</strong>
              @if (!item.isAvailable) {
                <span class="badge">Esgotado</span>
              }
            </div>
            @if (item.description) {
              <p class="muted description">{{ item.description }}</p>
            }
            <p class="price">
              <!-- The sr-only labels carry their own spaces: whitespace between tags is dropped. -->
              @if (item.promotionalPriceCents != null) {
                <span class="sr-only">Preço original </span>
                <s class="muted">{{ item.priceCents | brl }}</s>
                <span class="sr-only"> Preço promocional </span>
              }
              <strong>{{ item.effectivePriceCents | brl }}</strong>
            </p>
          </div>
          <div class="actions">
            <label class="check">
              <input
                type="checkbox"
                [checked]="item.isAvailable"
                [disabled]="busy()"
                (change)="toggle(item, $event)"
              />
              Disponível
            </label>
            <button class="btn btn-sm" type="button" (click)="edit.emit(item)">Editar</button>
            <button
              class="btn btn-sm"
              type="button"
              [attr.aria-label]="'Subir ' + item.name"
              [disabled]="first || busy()"
              (click)="move(item, -1)"
            >
              Subir
            </button>
            <button
              class="btn btn-sm"
              type="button"
              [attr.aria-label]="'Descer ' + item.name"
              [disabled]="last || busy()"
              (click)="move(item, 1)"
            >
              Descer
            </button>
            <button
              class="btn btn-sm btn-danger"
              type="button"
              [disabled]="busy()"
              (click)="remove(item)"
            >
              Excluir
            </button>
          </div>
        </li>
      }
    </ol>
  `,
  styles: `
    :host {
      display: block;
    }
    .thumb {
      width: 64px;
      height: 64px;
      object-fit: cover;
      border-radius: var(--radius-sm);
      background: var(--surface-muted);
    }
    .description,
    .price {
      margin: 0.15rem 0 0;
    }
    .price {
      display: flex;
      gap: 0.5rem;
    }
  `,
})
export class MenuItemsListComponent {
  private readonly api = inject(Api);

  /** Category whose items are listed (loaded with `listMenuItems({ categoryId })`). */
  readonly categoryId = input.required<string>();
  /** Bump to reload the items (e.g. after the item form saved). */
  readonly refresh = input(0);
  /** The owner asked to edit an item. */
  readonly edit = output<MenuItem>();

  protected readonly items = signal<MenuItem[]>([]);
  protected readonly loaded = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const categoryId = this.categoryId();
      this.refresh();
      untracked(() => void this.load(categoryId));
    });
  }

  protected async toggle(item: MenuItem, event: Event): Promise<void> {
    const checkbox = event.target as HTMLInputElement;
    const isAvailable = checkbox.checked;
    const ok = await this.run(async () => {
      const updated = await this.api.invoke(setMenuItemAvailability, {
        id: item.id,
        body: { isAvailable },
      });
      this.items.update((list) => list.map((i) => (i.id === item.id ? updated : i)));
    });
    if (!ok) checkbox.checked = !isAvailable;
  }

  protected async move(item: MenuItem, delta: -1 | 1): Promise<void> {
    const list = this.items();
    const next = moved(list, list.indexOf(item), delta);
    await this.run(async () => {
      await this.api.invoke(reorderCategoryItems, {
        id: this.categoryId(),
        body: { ids: next.map((i) => i.id) },
      });
      this.items.set(next.map((i, position) => ({ ...i, position })));
    });
  }

  protected async remove(item: MenuItem): Promise<void> {
    if (!window.confirm(`Excluir o item "${item.name}"?`)) return;
    await this.run(async () => {
      await this.api.invoke(deleteMenuItem, { id: item.id });
      this.items.update((list) => list.filter((i) => i.id !== item.id));
    });
  }

  private async load(categoryId: string): Promise<void> {
    await this.run(async () => {
      this.items.set(byPosition(await this.api.invoke(listMenuItems, { categoryId })));
      this.loaded.set(true);
    });
  }

  private async run(action: () => Promise<void>): Promise<boolean> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
      return true;
    } catch (err) {
      this.error.set(problemMessage(errorCode(err)));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
