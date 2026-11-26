import { HttpTestingController } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { alertText, button, queryButton, region, rows, textOf } from '../../../testing/dom';
import {
  apiUrl,
  expectNoRequest,
  expectRequest,
  flushProblem,
  provideApiTesting,
  settle,
} from '../../../testing/http';
import { listKitchenOrders } from '../../api/generated/fn/kitchen/list-kitchen-orders';
import { markOrderPickedUp } from '../../api/generated/fn/kitchen/mark-order-picked-up';
import { updateOrderPartStatus } from '../../api/generated/fn/kitchen/update-order-part-status';
import { KitchenOrder } from '../../api/generated/models/kitchen-order';
import { OrderPickedUpEvent } from '../../api/generated/models/order-picked-up-event';
import { Station } from '../../api/generated/models/station';
import { KitchenRealtimeService, RealtimeState } from '../../core/realtime/kitchen-realtime.service';
import { KitchenPage } from './kitchen.page';

const CARD = 'kitchen-card';
const LIST_URL = apiUrl(listKitchenOrders.PATH);

/** Paid 12:00, bar + kitchen received. Customer name with markup. */
const ORDER_A: KitchenOrder = {
  id: 'order-a',
  pickupCode: 'AAA2',
  customerName: '<b>Ana</b>',
  status: 'paid',
  createdAt: '2026-11-26T12:00:00Z',
  parts: [
    { station: 'bar', status: 'received', items: [{ name: 'Caipirinha', quantity: 2 }] },
    { station: 'kitchen', status: 'received', items: [{ name: 'Batata frita', quantity: 1 }] },
  ],
};
/** 12:05, bar only, in preparation. */
const ORDER_B: KitchenOrder = {
  id: 'order-b',
  pickupCode: 'BBB3',
  customerName: null,
  status: 'in_preparation',
  createdAt: '2026-11-26T12:05:00Z',
  parts: [{ station: 'bar', status: 'in_preparation', items: [{ name: 'Chopp', quantity: 1 }] }],
};
/** 11:50 (oldest), everything ready. */
const ORDER_C: KitchenOrder = {
  id: 'order-c',
  pickupCode: 'CCC4',
  customerName: 'Bruno',
  status: 'ready',
  createdAt: '2026-11-26T11:50:00Z',
  parts: [
    { station: 'kitchen', status: 'ready', items: [{ name: 'Batata frita', quantity: 2 }] },
    { station: 'bar', status: 'ready', items: [{ name: 'Gin tônica', quantity: 1 }] },
  ],
};

/** 12:10, kitchen only (arrives through OrderCreated). */
const ORDER_D: KitchenOrder = {
  id: 'order-d',
  pickupCode: 'DDD5',
  customerName: null,
  status: 'paid',
  createdAt: '2026-11-26T12:10:00Z',
  parts: [{ station: 'kitchen', status: 'received', items: [{ name: 'Pastel', quantity: 3 }] }],
};

class FakeKitchenRealtime {
  readonly state = signal<RealtimeState>('connected');
  readonly orderCreated$ = new Subject<KitchenOrder>();
  readonly orderUpdated$ = new Subject<KitchenOrder>();
  readonly orderPickedUp$ = new Subject<OrderPickedUpEvent>();
  readonly resync$ = new Subject<void>();
  readonly connect = vi.fn(async () => {});
  readonly disconnect = vi.fn(async () => {});
}

const time = (iso: string) =>
  new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(new Date(iso));

describe('KitchenPage', () => {
  let fixture: ComponentFixture<KitchenPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;
  let realtime: FakeKitchenRealtime;

  beforeEach(async () => {
    realtime = new FakeKitchenRealtime();
    TestBed.configureTestingModule({
      providers: [...provideApiTesting(), { provide: KitchenRealtimeService, useValue: realtime }],
    });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(KitchenPage);
    el = fixture.nativeElement;
    await settle(fixture);
  });

  afterEach(() => httpMock.verify());

  /** Triggers a resync and answers the list request. */
  async function resync(orders: KitchenOrder[] = [ORDER_B, ORDER_A, ORDER_C]): Promise<void> {
    realtime.resync$.next();
    await settle(fixture);
    expectRequest(httpMock, 'GET', LIST_URL).flush(orders);
    await settle(fixture);
  }

  const section = (station: 'Bar' | 'Cozinha') => region(el, station);
  const cards = (station: 'Bar' | 'Cozinha') => rows(section(station), CARD);
  const codes = (station: 'Bar' | 'Cozinha') =>
    cards(station).map((c) => [ORDER_A, ORDER_B, ORDER_C, ORDER_D].find((o) => textOf(c).includes(o.pickupCode))?.pickupCode);
  const card = (station: 'Bar' | 'Cozinha', code: string) => {
    const found = cards(station).find((c) => textOf(c).includes(code));
    if (!found) throw new Error(`No ${station} card for ${code}`);
    return found;
  };
  const statusUrl = (orderId: string, station: Station) =>
    apiUrl(updateOrderPartStatus.PATH, { orderId, station });

  it('connects to the kitchen hub on init and loads the orders on every resync', async () => {
    expect(realtime.connect).toHaveBeenCalled();
    expectNoRequest(httpMock, 'GET', LIST_URL);

    await resync();
    await resync([ORDER_A]);

    expect(codes('Bar')).toEqual(['AAA2']);
  });

  it('disconnects when the page is destroyed', async () => {
    fixture.destroy();
    await Promise.resolve();

    expect(realtime.disconnect).toHaveBeenCalled();
  });

  it('shows each part only in its station section, oldest first', async () => {
    await resync();

    expect(codes('Bar')).toEqual(['CCC4', 'AAA2', 'BBB3']);
    expect(codes('Cozinha')).toEqual(['CCC4', 'AAA2']);

    expect(textOf(card('Bar', 'AAA2'))).toContain('Caipirinha');
    expect(textOf(card('Bar', 'AAA2'))).not.toContain('Batata frita');
    expect(textOf(card('Cozinha', 'AAA2'))).toContain('Batata frita');
    expect(textOf(card('Cozinha', 'AAA2'))).not.toContain('Caipirinha');
  });

  it('shows the pickup code, the customer name as text, the time and items × quantity', async () => {
    await resync();
    const bar = card('Bar', 'AAA2');

    expect(textOf(bar)).toContain('AAA2');
    expect(textOf(bar)).toContain('<b>Ana</b>');
    expect(bar.querySelector('b')).toBeNull();
    expect(textOf(bar)).toContain(time(ORDER_A.createdAt));
    expect(textOf(bar)).toMatch(/Caipirinha\s*×\s*2/);
    expect(textOf(card('Cozinha', 'CCC4'))).toMatch(/Batata frita\s*×\s*2/);
  });

  describe('part status buttons', () => {
    it('received shows "Em preparo" and "Pronto"; in_preparation only "Pronto"; ready none', async () => {
      await resync();

      const received = card('Bar', 'AAA2');
      expect(queryButton(received, /em preparo/i)).not.toBeNull();
      expect(queryButton(received, /^pronto$/i)).not.toBeNull();

      const preparing = card('Bar', 'BBB3');
      expect(queryButton(preparing, /em preparo/i)).toBeNull();
      expect(queryButton(preparing, /^pronto$/i)).not.toBeNull();

      const ready = card('Bar', 'CCC4');
      expect(queryButton(ready, /em preparo/i)).toBeNull();
      expect(queryButton(ready, /^pronto$/i)).toBeNull();
    });

    it('"Em preparo" calls updateOrderPartStatus and applies the returned order', async () => {
      await resync();

      button(card('Bar', 'AAA2'), /em preparo/i).click();
      await settle(fixture);

      const req = expectRequest(httpMock, 'PUT', statusUrl(ORDER_A.id, 'bar'));
      expect(req.request.body).toEqual({ status: 'in_preparation' });
      req.flush({
        ...ORDER_A,
        status: 'in_preparation',
        parts: [{ ...ORDER_A.parts[0], status: 'in_preparation' }, ORDER_A.parts[1]],
      });
      await settle(fixture);

      expect(queryButton(card('Bar', 'AAA2'), /em preparo/i)).toBeNull();
      expect(queryButton(card('Bar', 'AAA2'), /^pronto$/i)).not.toBeNull();
    });

    it('"Pronto" calls updateOrderPartStatus with ready for that station', async () => {
      await resync();

      button(card('Cozinha', 'AAA2'), /^pronto$/i).click();
      await settle(fixture);

      const req = expectRequest(httpMock, 'PUT', statusUrl(ORDER_A.id, 'kitchen'));
      expect(req.request.body).toEqual({ status: 'ready' });
      req.flush({ ...ORDER_A, parts: [ORDER_A.parts[0], { ...ORDER_A.parts[1], status: 'ready' }] });
      await settle(fixture);

      expect(queryButton(card('Cozinha', 'AAA2'), /^pronto$/i)).toBeNull();
    });

    it('409 shows a warning and resyncs the list', async () => {
      await resync();

      button(card('Bar', 'BBB3'), /^pronto$/i).click();
      await settle(fixture);
      flushProblem(expectRequest(httpMock, 'PUT', statusUrl(ORDER_B.id, 'bar')), 409, 'invalid_status_transition');
      await settle(fixture);

      expect(alertText(el).length).toBeGreaterThan(0);
      expectRequest(httpMock, 'GET', LIST_URL).flush([ORDER_A, ORDER_C]);
      await settle(fixture);
      expect(codes('Bar')).toEqual(['CCC4', 'AAA2']);
    });
  });

  describe('"Retirado"', () => {
    it('only appears once the order is ready', async () => {
      await resync();

      expect(queryButton(card('Bar', 'AAA2'), /retirado/i)).toBeNull();
      expect(queryButton(card('Bar', 'BBB3'), /retirado/i)).toBeNull();
      const ready = [card('Bar', 'CCC4'), card('Cozinha', 'CCC4')];
      expect(ready.some((c) => queryButton(c, /retirado/i) !== null)).toBe(true);
    });

    it('calls markOrderPickedUp and removes the order from both sections', async () => {
      await resync();

      const withButton = [card('Bar', 'CCC4'), card('Cozinha', 'CCC4')].find(
        (c) => queryButton(c, /retirado/i) !== null,
      )!;
      button(withButton, /retirado/i).click();
      await settle(fixture);

      expectRequest(httpMock, 'PUT', apiUrl(markOrderPickedUp.PATH, { orderId: ORDER_C.id })).flush(ORDER_C);
      await settle(fixture);

      expect(codes('Bar')).toEqual(['AAA2', 'BBB3']);
      expect(codes('Cozinha')).toEqual(['AAA2']);
    });
  });

  describe('realtime events (no reload)', () => {
    it('OrderCreated adds the order to its sections', async () => {
      await resync([ORDER_A]);

      realtime.orderCreated$.next(ORDER_D);
      await settle(fixture);

      expectNoRequest(httpMock, 'GET', LIST_URL);
      expect(codes('Cozinha')).toEqual(['AAA2', 'DDD5']);
      expect(codes('Bar')).toEqual(['AAA2']);
    });

    it('OrderUpdated replaces the order by id', async () => {
      await resync([ORDER_A]);

      realtime.orderUpdated$.next({
        ...ORDER_A,
        status: 'ready',
        parts: ORDER_A.parts.map((p) => ({ ...p, status: 'ready' as const })),
      });
      await settle(fixture);

      expectNoRequest(httpMock, 'GET', LIST_URL);
      expect(cards('Bar')).toHaveLength(1);
      expect(queryButton(card('Bar', 'AAA2'), /^pronto$/i)).toBeNull();
      const all = [card('Bar', 'AAA2'), card('Cozinha', 'AAA2')];
      expect(all.some((c) => queryButton(c, /retirado/i) !== null)).toBe(true);
    });

    it('OrderPickedUp removes the order', async () => {
      await resync();

      realtime.orderPickedUp$.next({ id: ORDER_B.id, pickupCode: ORDER_B.pickupCode });
      await settle(fixture);

      expectNoRequest(httpMock, 'GET', LIST_URL);
      expect(codes('Bar')).toEqual(['CCC4', 'AAA2']);
    });
  });

  it('shows "Reconectando…" while disconnected', async () => {
    await resync();
    expect(textOf(el)).not.toContain('Reconectando…');

    realtime.state.set('disconnected');
    await settle(fixture);

    expect(textOf(el)).toContain('Reconectando…');
  });
});
