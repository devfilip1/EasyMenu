import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { FakeHubConnection, fakeHubFactory } from '../../../testing/fake-hub-connection';
import { PUBLIC_TOKEN, publicOrder } from '../../../testing/public-menu';
import { PublicOrder } from '../../api/generated/models/public-order';
import { HUB_CONNECTION_FACTORY } from './hub-connection.factory';
import { OrderRealtimeService } from './order-realtime.service';

describe('OrderRealtimeService', () => {
  let service: OrderRealtimeService;
  let connections: FakeHubConnection[];

  beforeEach(() => {
    localStorage.clear();
    const fake = fakeHubFactory();
    connections = fake.connections;
    TestBed.configureTestingModule({
      providers: [{ provide: HUB_CONNECTION_FACTORY, useValue: fake.factory }],
    });
    service = TestBed.inject(OrderRealtimeService);
  });

  const hub = () => {
    expect(connections, 'one HubConnection built').toHaveLength(1);
    return connections[0];
  };
  const subscriptions = () => hub().invocations.filter((i) => i.method === 'SubscribeToOrder');

  it('watch() connects anonymously to /hubs/orders', async () => {
    await service.watch(PUBLIC_TOKEN);

    expect(hub().url).toBe(`${environment.apiUrl}/hubs/orders`);
    expect(hub().options?.accessTokenFactory).toBeUndefined();
    expect(hub().startCalls).toBe(1);
    expect(service.state()).toBe('connected');
  });

  it('watch() subscribes to the order after starting', async () => {
    await service.watch(PUBLIC_TOKEN);

    expect(subscriptions()).toEqual([{ method: 'SubscribeToOrder', args: [PUBLIC_TOKEN] }]);
  });

  it('emits resync$ after subscribing', async () => {
    let resyncs = 0;
    service.resync$.subscribe(() => resyncs++);

    await service.watch(PUBLIC_TOKEN);

    expect(resyncs).toBe(1);
  });

  it('subscribes again and emits resync$ on reconnection', async () => {
    let resyncs = 0;
    service.resync$.subscribe(() => resyncs++);
    await service.watch(PUBLIC_TOKEN);

    hub().simulateReconnecting();
    expect(service.state()).toBe('reconnecting');
    hub().simulateReconnected();
    await Promise.resolve();
    await Promise.resolve();

    expect(subscriptions()).toHaveLength(2);
    expect(subscriptions()[1].args).toEqual([PUBLIC_TOKEN]);
    expect(resyncs).toBe(2);
    expect(service.state()).toBe('connected');
  });

  it('exposes OrderUpdated', async () => {
    const updates: PublicOrder[] = [];
    service.orderUpdated$.subscribe((o) => updates.push(o));
    await service.watch(PUBLIC_TOKEN);

    expect(hub().handlers.has('OrderUpdated')).toBe(true);
    const ready = publicOrder({ status: 'ready' });
    hub().emit('OrderUpdated', ready);

    expect(updates).toEqual([ready]);
  });

  it('stop() stops the connection', async () => {
    await service.watch(PUBLIC_TOKEN);

    await service.stop();

    expect(hub().stopCalls).toBe(1);
    expect(service.state()).toBe('disconnected');
  });

  it('stops the connection when destroyed', async () => {
    await service.watch(PUBLIC_TOKEN);
    const connection = hub();

    TestBed.resetTestingModule();

    expect(connection.stopCalls).toBe(1);
  });
});
