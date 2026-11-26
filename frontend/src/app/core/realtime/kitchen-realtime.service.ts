import { Injectable, Signal, signal } from '@angular/core';
import { NEVER, Observable } from 'rxjs';
import { KitchenOrder } from '../../api/generated/models/kitchen-order';
import { OrderPickedUpEvent } from '../../api/generated/models/order-picked-up-event';

export type RealtimeState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

// Stub (TDD red phase, T064). Implemented in T065.
@Injectable({ providedIn: 'root' })
export class KitchenRealtimeService {
  readonly state: Signal<RealtimeState> = signal('disconnected');
  readonly orderCreated$: Observable<KitchenOrder> = NEVER;
  readonly orderUpdated$: Observable<KitchenOrder> = NEVER;
  readonly orderPickedUp$: Observable<OrderPickedUpEvent> = NEVER;
  /** Emits after every (re)connection: the page must reload the full state via REST. */
  readonly resync$: Observable<void> = NEVER;

  connect(): Promise<void> {
    throw new Error('Not implemented');
  }

  disconnect(): Promise<void> {
    throw new Error('Not implemented');
  }
}
