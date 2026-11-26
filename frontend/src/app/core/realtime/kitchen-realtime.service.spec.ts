import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { authSession } from '../../../testing/auth-session';
import { FakeHubConnection, fakeHubFactory } from '../../../testing/fake-hub-connection';
import { KitchenOrder } from '../../api/generated/models/kitchen-order';
import { OrderPickedUpEvent } from '../../api/generated/models/order-picked-up-event';
import { AuthStore } from '../auth/auth.store';
import { HUB_CONNECTION_FACTORY } from './hub-connection.factory';
import { KitchenRealtimeService } from './kitchen-realtime.service';

const ORDER: KitchenOrder = {
  id: '0193f6c2-0000-7000-8000-000000000001',
  pickupCode: 'K7QX',
  customerName: null,
  status: 'paid',
  createdAt: '2026-11-26T12:00:00Z',
  parts: [{ station: 'bar', status: 'received', items: [{ name: 'Caipirinha', quantity: 2 }] }],
};

describe('KitchenRealtimeService', () => {
  let service: KitchenRealtimeService;
  let auth: AuthStore;
  let connections: FakeHubConnection[];

  beforeEach(() => {
    localStorage.clear();
    const fake = fakeHubFactory();
    connections = fake.connections;
    TestBed.configureTestingModule({
      providers: [{ provide: HUB_CONNECTION_FACTORY, useValue: fake.factory }],
    });
    auth = TestBed.inject(AuthStore);
    auth.setSession(authSession('kitchen'));
    service = TestBed.inject(KitchenRealtimeService);
  });

  afterEach(() => localStorage.clear());

  const hub = () => {
    expect(connections, 'one HubConnection built').toHaveLength(1);
    return connections[0];
  };

  it('connects to /hubs/kitchen', async () => {
    await service.connect();

    expect(hub().url).toBe(`${environment.apiUrl}/hubs/kitchen`);
    expect(hub().startCalls).toBe(1);
    expect(service.state()).toBe('connected');
  });

  it('sends the current AuthStore token through accessTokenFactory', async () => {
    await service.connect();

    const factory = hub().options?.accessTokenFactory;
    expect(factory).toBeTypeOf('function');
    expect(await factory!()).toBe('token-kitchen');

    // Read lazily on every (re)connection, so a refreshed session is picked up.
    auth.setSession(authSession('owner'));
    expect(await factory!()).toBe('token-owner');
  });

  it('exposes OrderCreated, OrderUpdated and OrderPickedUp as observables', async () => {
    const created: KitchenOrder[] = [];
    const updated: KitchenOrder[] = [];
    const pickedUp: OrderPickedUpEvent[] = [];
    service.orderCreated$.subscribe((o) => created.push(o));
    service.orderUpdated$.subscribe((o) => updated.push(o));
    service.orderPickedUp$.subscribe((e) => pickedUp.push(e));

    await service.connect();
    for (const event of ['OrderCreated', 'OrderUpdated', 'OrderPickedUp']) {
      expect(hub().handlers.has(event), `handler for ${event}`).toBe(true);
    }

    const ready: KitchenOrder = { ...ORDER, status: 'ready' };
    hub().emit('OrderCreated', ORDER);
    hub().emit('OrderUpdated', ready);
    hub().emit('OrderPickedUp', { id: ORDER.id, pickupCode: 'K7QX' });

    expect(created).toEqual([ORDER]);
    expect(updated).toEqual([ready]);
    expect(pickedUp).toEqual([{ id: ORDER.id, pickupCode: 'K7QX' }]);
  });

  it('emits resync$ after start and after every reconnection', async () => {
    let resyncs = 0;
    service.resync$.subscribe(() => resyncs++);

    await service.connect();
    expect(resyncs).toBe(1);

    hub().simulateReconnecting();
    expect(service.state()).toBe('reconnecting');
    hub().simulateReconnected();
    expect(resyncs).toBe(2);
    expect(service.state()).toBe('connected');
  });

  it('disconnect() stops the connection', async () => {
    await service.connect();

    await service.disconnect();

    expect(hub().stopCalls).toBe(1);
    expect(service.state()).toBe('disconnected');
  });

  it('exposes "disconnected" when the connection closes with an error', async () => {
    await service.connect();
    expect(service.state()).toBe('connected');

    hub().simulateClose(new Error('Server timeout'));

    expect(service.state()).toBe('disconnected');
  });
});
