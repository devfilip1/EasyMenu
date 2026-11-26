import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { Api } from '../../api/generated/api';
import { getPublicMenu } from '../../api/generated/fn/public-menu/get-public-menu';
import { PublicMenu } from '../../api/generated/models/public-menu';
import { PublicMenuItem } from '../../api/generated/models/public-menu-item';
import { errorCode, problemMessage } from '../../core/errors/problem';
import { BrlPipe } from '../../shared/brl.pipe';
import { CartSheetComponent } from './cart-sheet.component';
import { CartStore } from './cart.store';
import { MenuItemCardComponent } from './menu-item-card.component';

/** Public menu of an establishment (/m/:slug), mobile-first, for anonymous guests. */
@Component({
  selector: 'app-public-menu-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrlPipe, CartSheetComponent, MenuItemCardComponent],
  template: `
    <main class="page stack" [class.with-bar]="cart.count() > 0">
      @if (error()) {
        <div class="card stack">
          <p class="brand">EasyMenu</p>
          <p class="alert" role="alert">{{ error() }}</p>
        </div>
      } @else if (menu(); as current) {
        <header>
          <h1>{{ current.establishment.name }}</h1>
          <p class="muted">Escolha os itens, pague pelo app e retire no balcão.</p>
        </header>

        @for (category of current.categories; track category.id) {
          <section class="stack" [attr.aria-label]="category.name">
            <h2>{{ category.name }}</h2>
            <div class="grid">
              @for (item of category.items; track item.id) {
                <app-menu-item-card [item]="item" (add)="add($event)" />
              }
            </div>
          </section>
        } @empty {
          <p class="card muted">Cardápio indisponível no momento.</p>
        }
      } @else {
        <p class="muted" aria-live="polite">Carregando cardápio…</p>
      }
    </main>

    @if (menu() && !error()) {
      <div class="cart-bar">
        <button class="btn btn-primary btn-block" type="button" (click)="cartOpen.set(true)">
          Ver carrinho
          @if (cart.count() > 0) {
            <span>· {{ cart.count() }} {{ cart.count() === 1 ? 'item' : 'itens' }} ·</span>
            <span>{{ cart.total() | brl }}</span>
          }
        </button>
      </div>
    }

    @if (cartOpen()) {
      <div class="backdrop" (click)="cartOpen.set(false)"></div>
      <div class="sheet" role="dialog" aria-modal="true" aria-label="Carrinho">
        <div class="sheet-head">
          <button class="btn btn-sm" type="button" (click)="cartOpen.set(false)">
            Continuar comprando
          </button>
        </div>
        <app-cart-sheet [slug]="slug()" />
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    main {
      padding-bottom: 6rem;
    }
    .grid {
      display: grid;
      gap: 0.75rem;
    }
    @media (min-width: 720px) {
      .grid {
        grid-template-columns: 1fr 1fr;
      }
    }
    .cart-bar {
      position: fixed;
      inset: auto 0 0 0;
      padding: 0.75rem 1rem calc(0.75rem + env(safe-area-inset-bottom));
      background: var(--surface);
      border-top: 1px solid var(--border);
      box-shadow: var(--shadow);
    }
    .cart-bar .btn {
      max-width: 960px;
      margin: 0 auto;
    }
    .backdrop {
      position: fixed;
      inset: 0;
      background: rgb(0 0 0 / 40%);
      z-index: 20;
    }
    .sheet {
      position: fixed;
      inset: auto 0 0 0;
      max-height: 90dvh;
      overflow-y: auto;
      z-index: 21;
      padding: 1rem;
      background: var(--bg);
      border-radius: var(--radius) var(--radius) 0 0;
    }
    @media (min-width: 720px) {
      .sheet {
        inset: 0 0 0 auto;
        width: 420px;
        max-height: none;
        border-radius: 0;
      }
    }
    .sheet-head {
      display: flex;
      justify-content: flex-end;
      margin-bottom: 0.5rem;
    }
  `,
})
export class PublicMenuPage {
  private readonly api = inject(Api);
  protected readonly cart = inject(CartStore);

  /** Route parameter `:slug` (component input binding). */
  readonly slug = input.required<string>();

  protected readonly menu = signal<PublicMenu | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly cartOpen = signal(false);

  constructor() {
    effect(() => {
      const slug = this.slug();
      untracked(() => void this.load(slug));
    });
  }

  protected add(item: PublicMenuItem): void {
    this.cart.add(item);
  }

  private async load(slug: string): Promise<void> {
    this.cart.useSlug(slug);
    this.menu.set(null);
    this.error.set(null);
    try {
      const menu = await this.api.invoke(getPublicMenu, { slug });
      this.menu.set(menu);
      this.cart.syncWithMenu(menu);
    } catch (err) {
      this.error.set(problemMessage(errorCode(err, 'establishment_not_found')));
    }
  }
}
