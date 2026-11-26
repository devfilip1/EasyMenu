import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Api } from '../../api/generated/api';
import { placeOrder } from '../../api/generated/fn/public-orders/place-order';
import { UnavailableItem } from '../../api/generated/models/unavailable-item';
import { errorCode, problemExtension, problemMessage } from '../../core/errors/problem';
import { OrderHistoryStore } from '../../core/storage/order-history.store';
import { BrlPipe } from '../../shared/brl.pipe';
import { CartStore } from './cart.store';

/** Cart review and checkout (simulated payment) of the public menu. */
@Component({
  selector: 'app-cart-sheet',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrlPipe],
  template: `
    <section class="stack" aria-labelledby="cart-title">
      <h2 id="cart-title">Seu carrinho</h2>

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (cart.lines().length === 0) {
        <p class="muted">Seu carrinho está vazio. Adicione itens do cardápio.</p>
      }

      <ul class="list">
        @for (line of cart.lines(); track line.menuItemId) {
          <li class="list-row line" data-testid="cart-line" [class.unavailable]="line.unavailable">
            <div class="grow">
              <strong>{{ line.name }}</strong>
              <div class="muted">{{ line.unitPriceCents | brl }} cada</div>
              @if (line.unavailable) {
                <span class="badge">Indisponível</span>
              }
            </div>
            <div class="qty" role="group" [attr.aria-label]="'Quantidade de ' + line.name">
              <button
                class="btn btn-sm"
                type="button"
                [attr.aria-label]="'Diminuir ' + line.name"
                [disabled]="line.quantity <= 1 || placing()"
                (click)="cart.decrement(line.menuItemId)"
              >
                −
              </button>
              <span class="count" aria-live="polite">{{ line.quantity }}</span>
              <button
                class="btn btn-sm"
                type="button"
                [attr.aria-label]="'Aumentar ' + line.name"
                [disabled]="line.quantity >= 99 || placing()"
                (click)="cart.increment(line.menuItemId)"
              >
                +
              </button>
            </div>
            <strong class="subtotal">{{ line.unitPriceCents * line.quantity | brl }}</strong>
            <button
              class="btn btn-sm btn-danger"
              type="button"
              [attr.aria-label]="'Remover ' + line.name"
              [disabled]="placing()"
              (click)="cart.remove(line.menuItemId)"
            >
              Remover
            </button>
          </li>
        }
      </ul>

      <p class="total">
        Total <strong data-testid="cart-total">{{ cart.total() | brl }}</strong>
      </p>

      <div class="field">
        <label for="cart-name">Seu nome</label>
        <input
          id="cart-name"
          type="text"
          maxlength="60"
          autocomplete="given-name"
          [value]="customerName()"
          (input)="customerName.set($any($event.target).value)"
        />
        <span class="hint">Opcional. Ajuda a chamar você na retirada.</span>
      </div>

      @if (hasUnavailable()) {
        <p class="muted">Remova os itens indisponíveis para concluir a compra.</p>
      }

      <button
        class="btn btn-primary btn-block"
        type="button"
        [disabled]="!canBuy()"
        (click)="buy()"
      >
        Comprar
      </button>
      <p class="muted">Pagamento simulado: nenhuma cobrança real é feita.</p>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .unavailable strong:first-child {
      text-decoration: line-through;
    }
    .qty {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
    .count {
      min-width: 1.5rem;
      text-align: center;
      font-weight: 700;
    }
    .total {
      display: flex;
      justify-content: space-between;
      font-size: 1.15rem;
      margin: 0;
    }
  `,
})
export class CartSheetComponent {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly history = inject(OrderHistoryStore);
  protected readonly cart = inject(CartStore);

  /** Establishment whose cart is shown and where the order is placed. */
  readonly slug = input.required<string>();

  protected readonly customerName = signal('');
  protected readonly placing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly hasUnavailable = computed(() => this.cart.lines().some((l) => l.unavailable));
  protected readonly canBuy = computed(
    () => this.cart.lines().length > 0 && !this.hasUnavailable() && !this.placing(),
  );

  protected async buy(): Promise<void> {
    if (!this.canBuy()) return;
    const slug = this.slug();
    const lines = this.cart.lines();
    this.placing.set(true);
    this.error.set(null);
    try {
      const order = await this.api.invoke(placeOrder, {
        slug,
        body: {
          customerName: this.customerName().trim() || null,
          items: lines.map((l) => ({ menuItemId: l.menuItemId, quantity: l.quantity })),
        },
      });
      this.history.remember({
        publicToken: order.publicToken,
        slug,
        pickupCode: order.pickupCode,
        createdAt: order.createdAt,
      });
      this.cart.clear();
      await this.router.navigateByUrl(`/pedido/${order.publicToken}`);
    } catch (err) {
      this.showError(err);
    } finally {
      this.placing.set(false);
    }
  }

  private showError(err: unknown): void {
    const code = errorCode(err);
    if (code !== 'items_unavailable') {
      this.error.set(problemMessage(code));
      return;
    }
    const raw = problemExtension(err, 'unavailableItems');
    const items = Array.isArray(raw) ? (raw as UnavailableItem[]) : [];
    if (items.length === 0) {
      this.error.set(problemMessage(code));
      return;
    }
    const lines = this.cart.lines();
    const names = items.map(
      (item) =>
        item.name ?? lines.find((l) => l.menuItemId === item.menuItemId)?.name ?? 'item removido',
    );
    this.cart.markUnavailable(items.map((item) => item.menuItemId));
    this.error.set(`Itens indisponíveis: ${names.join(', ')}.`);
  }
}
