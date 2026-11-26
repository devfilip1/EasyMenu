import { Injectable, inject, signal } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { KitchenOrder } from '../../api/generated/models/kitchen-order';
import { OrderPickedUpEvent } from '../../api/generated/models/order-picked-up-event';
import { AuthStore } from '../auth/auth.store';
import { HUB_CONNECTION_FACTORY } from './hub-connection.factory';

export type RealtimeState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/** Delay before trying again when the very first `start()` fails (automatic reconnect only
 * covers connections that were established once). */
const START_RETRY_MS = 5_000;

/** Kitchen hub (`/hubs/kitchen`, contracts/realtime.md): live orders of the establishment. */
@Injectable({ providedIn: 'root' })
export class KitchenRealtimeService {
  private readonly auth = inject(AuthStore);
  private readonly createConnection = inject(HUB_CONNECTION_FACTORY);

  private readonly currentState = signal<RealtimeState>('disconnected');
  private readonly created = new Subject<KitchenOrder>();
  private readonly updated = new Subject<KitchenOrder>();
  private readonly pickedUp = new Subject<OrderPickedUpEvent>();
  private readonly resync = new Subject<void>();

  private connection: HubConnection | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  readonly state = this.currentState.asReadonly();
  readonly orderCreated$ = this.created.asObservable();
  readonly orderUpdated$ = this.updated.asObservable();
  readonly orderPickedUp$ = this.pickedUp.asObservable();
  /** Emits after every (re)connection: the page must reload the full state via REST. */
  readonly resync$ = this.resync.asObservable();

  async connect(): Promise<void> {
    if (this.connection) return;
    const connection = this.createConnection(`${environment.apiUrl}/hubs/kitchen`, {
      // Read on every (re)connection, so a renewed session is picked up.
      accessTokenFactory: () => this.auth.token() ?? '',
    });
    this.connection = connection;

    connection.on('OrderCreated', (order: KitchenOrder) => this.created.next(order));
    connection.on('OrderUpdated', (order: KitchenOrder) => this.updated.next(order));
    connection.on('OrderPickedUp', (event: OrderPickedUpEvent) => this.pickedUp.next(event));
    connection.onreconnecting(() => this.currentState.set('reconnecting'));
    connection.onreconnected(() => {
      this.currentState.set('connected');
      this.resync.next();
    });
    connection.onclose(() => {
      if (this.connection === connection) this.currentState.set('disconnected');
    });

    await this.start(connection);
  }

  async disconnect(): Promise<void> {
    this.clearRetry();
    const connection = this.connection;
    this.connection = null;
    this.currentState.set('disconnected');
    await connection?.stop();
  }

  private async start(connection: HubConnection): Promise<void> {
    this.currentState.set('connecting');
    try {
      await connection.start();
    } catch {
      if (this.connection !== connection) return;
      this.currentState.set('disconnected');
      // REST fallback while the hub is down: the page reloads the board, and a revoked session
      // (401 on the hub's negotiate, which bypasses HttpClient) is caught by the auth interceptor.
      this.resync.next();
      this.retryTimer = setTimeout(() => void this.start(connection), START_RETRY_MS);
      return;
    }
    if (this.connection !== connection) return;
    this.currentState.set('connected');
    this.resync.next();
  }

  private clearRetry(): void {
    if (this.retryTimer) clearTimeout(this.retryTimer);
    this.retryTimer = null;
  }
}
