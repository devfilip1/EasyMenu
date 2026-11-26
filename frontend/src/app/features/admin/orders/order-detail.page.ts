import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../../../api/generated/api';
import { getOrder } from '../../../api/generated/fn/orders/get-order';
import { OrderDetail } from '../../../api/generated/models/order-detail';
import { errorCode, problemMessage } from '../../../core/errors/problem';
import { BrlPipe } from '../../../shared/brl.pipe';
import { ORDER_STATUS_LABELS, PART_STATUS_LABELS, STATION_LABELS } from './labels';

/** One order of the establishment, with the price snapshots taken at purchase. */
@Component({
  selector: 'app-order-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrlPipe, DatePipe, RouterLink],
  template: `
    <div class="stack">
      <a routerLink="/admin/pedidos">← Voltar para pedidos</a>

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      } @else if (order(); as current) {
        <header class="row heading">
          <h1>Pedido {{ current.pickupCode }}</h1>
          <span class="status" data-testid="order-status">{{ statusLabels[current.status] }}</span>
        </header>
        <p class="muted">
          {{ current.customerName || 'Sem nome' }} ·
          {{ current.createdAt | date: 'dd/MM/yyyy HH:mm' }}
          @if (current.pickedUpAt) {
            · retirado às {{ current.pickedUpAt | date: 'HH:mm' }}
          }
        </p>

        <section class="card stack" aria-labelledby="detail-parts">
          <h2 id="detail-parts">Setores</h2>
          <ul class="list">
            @for (part of current.parts; track part.station) {
              <li class="row part" data-testid="order-part">
                <strong>{{ stationLabels[part.station] }}</strong>
                <span>{{ partLabels[part.status] }}</span>
              </li>
            }
          </ul>
        </section>

        <section class="card stack" aria-labelledby="detail-items">
          <h2 id="detail-items">Itens</h2>
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
      } @else {
        <p class="muted" aria-live="polite">Carregando pedido…</p>
      }
    </div>
  `,
  styles: `
    .heading {
      justify-content: space-between;
    }
    h1 {
      margin: 0;
    }
    .status {
      font-weight: 700;
      padding: 0.2rem 0.75rem;
      border-radius: 999px;
      background: var(--surface-muted);
    }
    .part {
      justify-content: space-between;
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
export class OrderDetailPage {
  private readonly api = inject(Api);

  /** Route parameter `:orderId` (component input binding). */
  readonly orderId = input.required<string>();

  protected readonly statusLabels = ORDER_STATUS_LABELS;
  protected readonly partLabels = PART_STATUS_LABELS;
  protected readonly stationLabels = STATION_LABELS;
  protected readonly order = signal<OrderDetail | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const orderId = this.orderId();
      untracked(() => void this.load(orderId));
    });
  }

  private async load(orderId: string): Promise<void> {
    this.order.set(null);
    this.error.set(null);
    try {
      this.order.set(await this.api.invoke(getOrder, { orderId }));
    } catch (err) {
      const notFound = err instanceof HttpErrorResponse && err.status === 404;
      this.error.set(problemMessage(notFound ? 'order_not_found' : errorCode(err)));
    }
  }
}
