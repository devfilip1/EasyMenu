import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../../../api/generated/api';
import { listOrders } from '../../../api/generated/fn/orders/list-orders';
import { OrderStatus } from '../../../api/generated/models/order-status';
import { OrderSummaryPage } from '../../../api/generated/models/order-summary-page';
import { errorCode, problemMessage } from '../../../core/errors/problem';
import { BrlPipe } from '../../../shared/brl.pipe';
import { ORDER_STATUS_LABELS } from './labels';

const PAGE_SIZE = 20;

/** Order history of the establishment, filterable by status and paginated. */
@Component({
  selector: 'app-orders-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrlPipe, DatePipe, RouterLink],
  template: `
    <div class="stack">
      <div class="row heading">
        <h1>Pedidos</h1>
        <div class="field filter">
          <label for="orders-status">Status</label>
          <select id="orders-status" (change)="filter($any($event.target).value)">
            <option value="" [selected]="status() === null">Todos</option>
            @for (option of statusOptions; track option.value) {
              <option [value]="option.value" [selected]="status() === option.value">
                {{ option.label }}
              </option>
            }
          </select>
        </div>
      </div>

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      <ul class="list">
        @for (order of result()?.items ?? []; track order.id) {
          <li data-testid="order-row">
            <a class="list-row order" [routerLink]="['/admin/pedidos', order.id]">
              <strong class="code">{{ order.pickupCode }}</strong>
              <span class="grow">{{ order.customerName || '—' }}</span>
              <span class="muted">{{ order.createdAt | date: 'dd/MM/yyyy HH:mm' }}</span>
              <strong>{{ order.totalCents | brl }}</strong>
              <span class="status">{{ statusLabel(order.status) }}</span>
            </a>
          </li>
        } @empty {
          @if (result()) {
            <li class="muted">Nenhum pedido encontrado.</li>
          }
        }
      </ul>

      <nav class="row pager" aria-label="Paginação">
        <button
          class="btn btn-sm"
          type="button"
          [disabled]="page() <= 1 || loading()"
          (click)="goTo(page() - 1)"
        >
          Anterior
        </button>
        <span aria-live="polite">Página {{ page() }} de {{ totalPages() }}</span>
        <button
          class="btn btn-sm"
          type="button"
          [disabled]="page() >= totalPages() || loading()"
          (click)="goTo(page() + 1)"
        >
          Próxima
        </button>
      </nav>
    </div>
  `,
  styles: `
    .heading {
      justify-content: space-between;
      align-items: flex-end;
    }
    h1 {
      margin: 0;
    }
    .filter {
      min-width: 12rem;
    }
    .order {
      color: inherit;
      text-decoration: none;
    }
    .order:hover {
      border-color: var(--primary);
    }
    .code {
      letter-spacing: 0.08em;
    }
    .status {
      font-weight: 600;
    }
    .pager {
      justify-content: center;
    }
  `,
})
export class OrdersPage implements OnInit {
  private readonly api = inject(Api);

  protected readonly statusOptions = (Object.keys(ORDER_STATUS_LABELS) as OrderStatus[]).map(
    (value) => ({ value, label: ORDER_STATUS_LABELS[value] }),
  );

  protected readonly status = signal<OrderStatus | null>(null);
  protected readonly page = signal(1);
  protected readonly result = signal<OrderSummaryPage | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil((this.result()?.totalCount ?? 0) / PAGE_SIZE)),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected statusLabel(status: OrderStatus): string {
    return ORDER_STATUS_LABELS[status] ?? status;
  }

  protected filter(value: string): void {
    this.status.set(value ? (value as OrderStatus) : null);
    this.page.set(1);
    void this.load();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const status = this.status() ?? undefined;
      this.result.set(
        await this.api.invoke(listOrders, { status, page: this.page(), pageSize: PAGE_SIZE }),
      );
    } catch (err) {
      this.error.set(problemMessage(errorCode(err)));
    } finally {
      this.loading.set(false);
    }
  }
}
