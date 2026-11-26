import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PublicOrder } from '../../api/generated/models/public-order';
import { HUB_CONNECTION_FACTORY } from './hub-connection.factory';
import { RealtimeState } from './kitchen-realtime.service';

const START_RETRY_MS = 5_000;

/** Orders hub (`/hubs/orders`, contracts/realtime.md): anonymous, one order per subscription. */
@Injectable({ providedIn: 'root' })
export class OrderRealtimeService implements OnDestroy {
  private readonly createConnection = inject(HUB_CONNECTION_FACTORY);

  private readonly currentState = signal<RealtimeState>('disconnected');
  private readonly updated = new Subject<PublicOrder>();
  private readonly resync = new Subject<void>();

  private connection: HubConnection | null = null;
  private publicToken: string | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  readonly state = this.currentState.asReadonly();
  /** `OrderUpdated` events (full `PublicOrder` snapshot) of the watched order. */
  readonly orderUpdated$ = this.updated.asObservable();
  /** Emits after subscribing and after every reconnection: reload the order via REST. */
  readonly resync$ = this.resync.asObservable();

  /** Connects anonymously to /hubs/orders and subscribes to this order. */
  async watch(publicToken: string): Promise<void> {
    if (this.connection && this.publicToken === publicToken) return;
    await this.stop();

    // Anonymous hub: no accessTokenFactory, the public token is the capability.
    const connection = this.createConnection(`${environment.apiUrl}/hubs/orders`);
    this.connection = connection;
    this.publicToken = publicToken;

    connection.on('OrderUpdated', (order: PublicOrder) => this.updated.next(order));
    connection.onreconnecting(() => this.currentState.set('reconnecting'));
    connection.onreconnected(() => void this.subscribe(connection));
    connection.onclose(() => {
      if (this.connection === connection) this.currentState.set('disconnected');
    });

    await this.start(connection);
  }

  async stop(): Promise<void> {
    if (this.retryTimer) clearTimeout(this.retryTimer);
    this.retryTimer = null;
    const connection = this.connection;
    this.connection = null;
    this.publicToken = null;
    this.currentState.set('disconnected');
    await connection?.stop();
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private async start(connection: HubConnection): Promise<void> {
    this.currentState.set('connecting');
    try {
      await connection.start();
    } catch {
      if (this.connection !== connection) return;
      this.currentState.set('disconnected');
      this.retryTimer = setTimeout(() => void this.start(connection), START_RETRY_MS);
      return;
    }
    await this.subscribe(connection);
  }

  /** (Re)joins the order group; groups do not survive a reconnection. */
  private async subscribe(connection: HubConnection): Promise<void> {
    const publicToken = this.publicToken;
    if (this.connection !== connection || !publicToken) return;
    try {
      await connection.invoke('SubscribeToOrder', publicToken);
    } catch {
      // The REST resync below still shows the current state.
    }
    if (this.connection !== connection) return;
    this.currentState.set('connected');
    this.resync.next();
  }
}
