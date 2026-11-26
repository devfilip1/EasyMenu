import { Injectable, OnDestroy, Signal, signal } from '@angular/core';
import { NEVER, Observable } from 'rxjs';
import { PublicOrder } from '../../api/generated/models/public-order';
import { RealtimeState } from './kitchen-realtime.service';

// Stub (TDD red phase, T073). Implemented in T074.
@Injectable({ providedIn: 'root' })
export class OrderRealtimeService implements OnDestroy {
  readonly state: Signal<RealtimeState> = signal('disconnected');
  /** `OrderUpdated` events (full `PublicOrder` snapshot) of the watched order. */
  readonly orderUpdated$: Observable<PublicOrder> = NEVER;
  /** Emits after subscribing and after every reconnection: reload the order via REST. */
  readonly resync$: Observable<void> = NEVER;

  /** Connects anonymously to /hubs/orders and subscribes to this order. */
  watch(_publicToken: string): Promise<void> {
    throw new Error('Not implemented');
  }

  stop(): Promise<void> {
    throw new Error('Not implemented');
  }

  ngOnDestroy(): void {
    throw new Error('Not implemented');
  }
}
