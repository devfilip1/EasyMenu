import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../api/generated/api';
import { listKitchenOrders } from '../../api/generated/fn/kitchen/list-kitchen-orders';
import { markOrderPickedUp } from '../../api/generated/fn/kitchen/mark-order-picked-up';
import { updateOrderPartStatus } from '../../api/generated/fn/kitchen/update-order-part-status';
import { KitchenOrder } from '../../api/generated/models/kitchen-order';
import { KitchenOrderPart } from '../../api/generated/models/kitchen-order-part';
import { Station } from '../../api/generated/models/station';
import { UpdatePartStatusRequest } from '../../api/generated/models/update-part-status-request';
import { AuthStore } from '../../core/auth/auth.store';
import { errorCode, problemMessage } from '../../core/errors/problem';
import { KitchenRealtimeService } from '../../core/realtime/kitchen-realtime.service';

interface Ticket {
  order: KitchenOrder;
  part: KitchenOrderPart;
}

const TIME = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' });

const PART_LABELS: Record<KitchenOrderPart['status'], string> = {
  received: 'Recebido',
  in_preparation: 'Em preparo',
  ready: 'Pronto',
};

/** Live board of the bar and the kitchen: one card per order part, oldest first. */
@Component({
  selector: 'app-kitchen-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <header class="topbar">
      <div class="page row">
        <h1 class="grow">Painel da cozinha</h1>
        @if (auth.isOwner()) {
          <a class="btn btn-sm" routerLink="/admin/pedidos">Painel do dono</a>
        }
        <button class="btn btn-sm" type="button" (click)="logout()">Sair</button>
      </div>
    </header>

    <main class="page stack">
      @if (realtime.state() === 'disconnected' || realtime.state() === 'reconnecting') {
        <p class="notice-warn" aria-live="polite">Reconectando…</p>
      }
      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      <div class="board">
        @for (station of stations; track station.id) {
          <section class="stack" [attr.aria-labelledby]="'station-' + station.id">
            <h2 [id]="'station-' + station.id">{{ station.label }}</h2>
            @for (ticket of tickets()[station.id]; track ticket.order.id) {
              <article
                class="ticket"
                data-testid="kitchen-card"
                [attr.data-status]="ticket.part.status"
              >
                <div class="row head">
                  <strong class="code">{{ ticket.order.pickupCode }}</strong>
                  <span class="muted">{{ time(ticket.order.createdAt) }}</span>
                  <span class="status">{{ partLabel(ticket.part) }}</span>
                </div>
                @if (ticket.order.customerName) {
                  <p class="customer">{{ ticket.order.customerName }}</p>
                }
                <ul class="items">
                  @for (item of ticket.part.items; track $index) {
                    <li>{{ item.name }} × {{ item.quantity }}</li>
                  }
                </ul>
                <div class="actions">
                  @if (ticket.part.status === 'received') {
                    <button
                      class="btn btn-sm"
                      type="button"
                      [disabled]="busy() === ticket.order.id"
                      (click)="advance(ticket, 'in_preparation')"
                    >
                      Em preparo
                    </button>
                  }
                  @if (ticket.part.status !== 'ready') {
                    <button
                      class="btn btn-sm btn-primary"
                      type="button"
                      [disabled]="busy() === ticket.order.id"
                      (click)="advance(ticket, 'ready')"
                    >
                      Pronto
                    </button>
                  }
                  @if (ticket.order.status === 'ready') {
                    <button
                      class="btn btn-sm btn-primary"
                      type="button"
                      [disabled]="busy() === ticket.order.id"
                      (click)="pickUp(ticket.order)"
                    >
                      Retirado
                    </button>
                  }
                </div>
              </article>
            } @empty {
              <p class="muted">Nenhum pedido em aberto.</p>
            }
          </section>
        }
      </div>
    </main>
  `,
  styles: `
    .topbar {
      background: var(--surface);
      border-bottom: 1px solid var(--border);
    }
    .grow {
      flex: 1;
      margin: 0;
    }
    .board {
      display: grid;
      gap: 1.5rem;
    }
    @media (min-width: 840px) {
      .board {
        grid-template-columns: 1fr 1fr;
      }
    }
    .ticket {
      padding: 0.75rem;
      background: var(--surface);
      border: 1px solid var(--border);
      border-left: 6px solid var(--border);
      border-radius: var(--radius-sm);
    }
    .ticket[data-status='in_preparation'] {
      border-left-color: #d97706;
    }
    .ticket[data-status='ready'] {
      border-left-color: var(--success);
    }
    .head {
      justify-content: space-between;
    }
    .code {
      font-size: 1.4rem;
      letter-spacing: 0.1em;
    }
    .status {
      font-weight: 600;
      font-size: 0.85rem;
    }
    .customer {
      margin: 0.25rem 0 0;
      overflow-wrap: anywhere;
    }
    .items {
      margin: 0.5rem 0;
      padding-left: 1.1rem;
    }
    .notice-warn {
      padding: 0.75rem 1rem;
      border-radius: var(--radius-sm);
      background: #fffbeb;
      color: #92400e;
    }
  `,
})
export class KitchenPage implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthStore);
  protected readonly realtime = inject(KitchenRealtimeService);

  protected readonly stations: { id: Station; label: string }[] = [
    { id: 'bar', label: 'Bar' },
    { id: 'kitchen', label: 'Cozinha' },
  ];

  protected readonly orders = signal<KitchenOrder[]>([]);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  /** Parts grouped by station, oldest order first. */
  protected readonly tickets = computed(() => {
    const sorted = [...this.orders()].sort(
      (a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt),
    );
    const byStation: Record<Station, Ticket[]> = { bar: [], kitchen: [] };
    for (const order of sorted) {
      for (const part of order.parts) byStation[part.station]?.push({ order, part });
    }
    return byStation;
  });

  constructor() {
    const realtime = this.realtime;
    realtime.resync$.pipe(takeUntilDestroyed()).subscribe(() => void this.load());
    realtime.orderCreated$.pipe(takeUntilDestroyed()).subscribe((o) => this.upsert(o));
    realtime.orderUpdated$.pipe(takeUntilDestroyed()).subscribe((o) => this.upsert(o));
    realtime.orderPickedUp$.pipe(takeUntilDestroyed()).subscribe((e) => this.removeOrder(e.id));
    this.destroyRef.onDestroy(() => void realtime.disconnect());
  }

  ngOnInit(): void {
    // The first resync$ (after the hub connects) loads the orders.
    void this.realtime.connect();
  }

  protected time(iso: string): string {
    return TIME.format(new Date(iso));
  }

  protected partLabel(part: KitchenOrderPart): string {
    return PART_LABELS[part.status];
  }

  protected async advance(
    ticket: Ticket,
    status: UpdatePartStatusRequest['status'],
  ): Promise<void> {
    await this.act(ticket.order.id, async () => {
      const updated = await this.api.invoke(updateOrderPartStatus, {
        orderId: ticket.order.id,
        station: ticket.part.station,
        body: { status },
      });
      this.upsert(updated);
    });
  }

  protected async pickUp(order: KitchenOrder): Promise<void> {
    await this.act(order.id, async () => {
      await this.api.invoke(markOrderPickedUp, { orderId: order.id });
      this.removeOrder(order.id);
    });
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }

  private async load(): Promise<void> {
    try {
      this.orders.set(await this.api.invoke(listKitchenOrders));
    } catch (err) {
      this.error.set(problemMessage(errorCode(err)));
    }
  }

  /** Runs an action on one order; a 409 means the board is stale, so it reloads. */
  private async act(orderId: string, action: () => Promise<void>): Promise<void> {
    this.busy.set(orderId);
    this.error.set(null);
    try {
      await action();
    } catch (err) {
      const code = errorCode(err);
      this.error.set(problemMessage(code));
      if (err && (err as { status?: number }).status === 409) await this.load();
    } finally {
      this.busy.set(null);
    }
  }

  private upsert(order: KitchenOrder): void {
    this.orders.update((list) =>
      list.some((o) => o.id === order.id)
        ? list.map((o) => (o.id === order.id ? order : o))
        : [...list, order],
    );
  }

  private removeOrder(id: string): void {
    this.orders.update((list) => list.filter((o) => o.id !== id));
  }
}
