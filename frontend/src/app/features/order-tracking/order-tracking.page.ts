import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Api } from '../../api/generated/api';
import { getPublicOrder } from '../../api/generated/fn/public-orders/get-public-order';
import { OrderStatus } from '../../api/generated/models/order-status';
import { PublicOrder } from '../../api/generated/models/public-order';
import { errorCode, problemMessage } from '../../core/errors/problem';
import { OrderRealtimeService } from '../../core/realtime/order-realtime.service';
import { OrderHistoryStore } from '../../core/storage/order-history.store';
import { BrlPipe } from '../../shared/brl.pipe';

const STATUS_LABELS: Record<OrderStatus, string> = {
  paid: 'Pago / aguardando preparo',
  in_preparation: 'Em preparo',
  ready: 'Pronto para retirada',
  picked_up: 'Retirado',
};

/** Guest's order page (/pedido/:publicToken): pickup code, items and status. */
@Component({
  selector: 'app-order-tracking-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrlPipe, RouterLink],
  template: `
    <main class="page stack">
      @if (error()) {
        <div class="card stack">
          <p class="brand">EasyMenu</p>
          <p class="alert" role="alert">{{ error() }}</p>
        </div>
      } @else if (order(); as current) {
        <header>
          <p class="muted">{{ current.establishment.name }}</p>
          <h1>Seu pedido</h1>
          @if (current.customerName) {
            <p>Pedido de {{ current.customerName }}</p>
          }
        </header>

        @if (current.status === 'ready') {
          <p class="ready-banner" role="alert">
            Seu pedido está pronto — retire no balcão. Código
            <strong>{{ current.pickupCode }}</strong>
          </p>
        }
        @if (realtime.state() === 'reconnecting') {
          <p class="muted" aria-live="polite">Reconectando para receber atualizações…</p>
        }

        <section class="card code-card" aria-labelledby="pickup-label">
          <p id="pickup-label" class="muted">Código de retirada</p>
          <p class="code" data-testid="pickup-code">{{ current.pickupCode }}</p>
          <p class="status" data-testid="order-status" [attr.data-status]="current.status">
            {{ statusLabel(current.status) }}
          </p>
          <p class="muted">Mostre este código no balcão para retirar o pedido.</p>
        </section>

        <section class="card stack" aria-labelledby="items-title">
          <h2 id="items-title">Itens</h2>
          <ul class="list">
            @for (line of current.items; track $index) {
              <li class="line" data-testid="order-line">
                <span class="grow">
                  <strong>{{ line.quantity }} ×</strong> {{ line.name }}
                  <span class="muted">({{ line.unitPriceCents | brl }} cada)</span>
                </span>
                <strong>{{ line.subtotalCents | brl }}</strong>
              </li>
            }
          </ul>
          <p class="total">
            Total <strong data-testid="order-total">{{ current.totalCents | brl }}</strong>
          </p>
        </section>

        <a class="btn" [routerLink]="['/m', current.establishment.slug]">Voltar ao cardápio</a>
      } @else {
        <p class="muted" aria-live="polite">Carregando pedido…</p>
      }
    </main>
  `,
  styles: `
    .code-card {
      text-align: center;
    }
    .ready-banner {
      margin: 0;
      padding: 1rem;
      border-radius: var(--radius);
      background: var(--success-bg);
      color: var(--success);
      font-size: 1.15rem;
      font-weight: 700;
      text-align: center;
    }
    .code-card p {
      margin: 0.25rem 0;
    }
    .code {
      font-size: 3rem;
      font-weight: 800;
      letter-spacing: 0.2em;
      font-family: ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace;
      color: var(--primary);
    }
    .status {
      display: inline-block;
      padding: 0.25rem 0.75rem;
      border-radius: 999px;
      font-weight: 700;
      background: var(--surface-muted);
    }
    .line {
      display: flex;
      gap: 0.75rem;
      justify-content: space-between;
      padding: 0.5rem 0;
      border-bottom: 1px solid var(--border);
    }
    .grow {
      flex: 1;
      min-width: 0;
      overflow-wrap: anywhere;
    }
    .total {
      display: flex;
      justify-content: space-between;
      font-size: 1.15rem;
      margin: 0;
    }
  `,
})
export class OrderTrackingPage {
  private readonly api = inject(Api);
  private readonly history = inject(OrderHistoryStore);
  protected readonly realtime = inject(OrderRealtimeService);

  /** Route parameter `:publicToken` (component input binding). */
  readonly publicToken = input.required<string>();

  protected readonly order = signal<PublicOrder | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const publicToken = this.publicToken();
      untracked(() => void this.open(publicToken));
    });
    this.realtime.resync$
      .pipe(takeUntilDestroyed())
      .subscribe(() => void this.load(this.publicToken()));
    this.realtime.orderUpdated$.pipe(takeUntilDestroyed()).subscribe((order) => {
      if (order.publicToken === this.publicToken()) this.show(order);
    });
    inject(DestroyRef).onDestroy(() => void this.realtime.stop());
  }

  protected statusLabel(status: OrderStatus): string {
    return STATUS_LABELS[status] ?? status;
  }

  /** First load, then live updates through the orders hub. */
  private async open(publicToken: string): Promise<void> {
    this.order.set(null);
    if (await this.load(publicToken)) await this.realtime.watch(publicToken);
  }

  private async load(publicToken: string): Promise<boolean> {
    this.error.set(null);
    try {
      const order = await this.api.invoke(getPublicOrder, { publicToken });
      this.show(order);
      this.history.remember({
        publicToken: order.publicToken,
        slug: order.establishment.slug,
        pickupCode: order.pickupCode,
        createdAt: order.createdAt,
      });
      return true;
    } catch (err) {
      this.order.set(null);
      this.error.set(problemMessage(errorCode(err, 'order_not_found')));
      return false;
    }
  }

  private show(order: PublicOrder): void {
    this.order.set(order);
  }
}
