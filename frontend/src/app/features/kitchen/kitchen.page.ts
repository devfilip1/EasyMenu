import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
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
import { FieldErrorComponent } from '../../shared/forms/field-error.component';
import { fieldErrorId } from '../../shared/forms/form-a11y';

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
  imports: [RouterLink, FieldErrorComponent],
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
                </div>
                @if (ticket.order.status === 'ready') {
                  @if (ticket.part.station === pickupStation(ticket.order)) {
                    <form
                      class="pickup"
                      novalidate
                      (submit)="$event.preventDefault(); pickUp(ticket.order)"
                    >
                      <div class="field">
                        <label [for]="pickupInputId(ticket.order)">Código de retirada</label>
                        <input
                          type="text"
                          maxlength="20"
                          autocomplete="off"
                          autocapitalize="characters"
                          spellcheck="false"
                          aria-required="true"
                          [id]="pickupInputId(ticket.order)"
                          [value]="pickupCodes()[ticket.order.id] ?? ''"
                          [attr.aria-invalid]="!!pickupErrors()[ticket.order.id]"
                          [attr.aria-describedby]="pickupErrorId(ticket.order)"
                          (input)="setPickupCode(ticket.order, $any($event.target).value)"
                        />
                        <app-field-error
                          [id]="pickupErrorId(ticket.order)"
                          [message]="pickupErrors()[ticket.order.id] ?? null"
                        />
                      </div>
                      <button
                        class="btn btn-sm btn-primary"
                        type="submit"
                        [disabled]="busy() === ticket.order.id"
                      >
                        Confirmar retirada
                      </button>
                    </form>
                  } @else {
                    <p class="muted">Pedido pronto: confira o código no cartão do Bar.</p>
                  }
                }
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
    .pickup {
      gap: 0.5rem;
      margin-top: 0.5rem;
      padding-top: 0.5rem;
      border-top: 1px dashed var(--border);
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

  private readonly host: HTMLElement = inject(ElementRef).nativeElement;

  protected readonly orders = signal<KitchenOrder[]>([]);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  /** Pickup code typed per ready order, and its field error. */
  protected readonly pickupCodes = signal<Record<string, string>>({});
  protected readonly pickupErrors = signal<Record<string, string | null>>({});

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

  /** The single card of a ready order that carries the pickup form (Bar when present). */
  protected pickupStation(order: KitchenOrder): Station {
    return order.parts.some((p) => p.station === 'bar') ? 'bar' : 'kitchen';
  }

  protected pickupInputId(order: KitchenOrder): string {
    return `pickup-code-${order.id}`;
  }

  protected pickupErrorId(order: KitchenOrder): string {
    return fieldErrorId('pickup-code', order.id);
  }

  protected setPickupCode(order: KitchenOrder, value: string): void {
    this.pickupCodes.update((codes) => ({ ...codes, [order.id]: value }));
    this.setPickupError(order.id, null);
  }

  /** Confirms the pickup with the code the customer shows; the server normalizes it. */
  protected async pickUp(order: KitchenOrder): Promise<void> {
    if (this.busy() === order.id) return;
    const pickupCode = this.pickupCodes()[order.id] ?? '';
    this.error.set(null);
    if (!pickupCode.trim()) {
      this.setPickupError(order.id, problemMessage('pickup_code_required'));
      this.focusPickupInput(order);
      return;
    }

    this.busy.set(order.id);
    try {
      await this.api.invoke(markOrderPickedUp, { orderId: order.id, body: { pickupCode } });
      this.removeOrder(order.id);
    } catch (err) {
      const code = errorCode(err);
      if (code === 'invalid_pickup_code' || code === 'pickup_code_required') {
        this.setPickupError(order.id, problemMessage(code));
        this.focusPickupInput(order);
      } else {
        this.error.set(problemMessage(code));
        if (isStale(err)) await this.load();
      }
    } finally {
      this.busy.set(null);
    }
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
      this.error.set(problemMessage(errorCode(err)));
      if (isStale(err)) await this.load();
    } finally {
      this.busy.set(null);
    }
  }

  private setPickupError(orderId: string, message: string | null): void {
    this.pickupErrors.update((errors) => ({ ...errors, [orderId]: message }));
  }

  private focusPickupInput(order: KitchenOrder): void {
    this.host.querySelector<HTMLInputElement>(`#${CSS.escape(this.pickupInputId(order))}`)?.focus();
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
    this.pickupCodes.update(({ [id]: _code, ...rest }) => rest);
    this.pickupErrors.update(({ [id]: _error, ...rest }) => rest);
  }
}

/** 409 (e.g. order_already_picked_up, invalid transition) or 404: the board is out of date. */
function isStale(err: unknown): boolean {
  return err instanceof HttpErrorResponse && (err.status === 409 || err.status === 404);
}
