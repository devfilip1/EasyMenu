import { HttpTestingController } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { rowWith, rows, textOf } from '../../../testing/dom';
import {
  apiUrl,
  expectNoRequest,
  expectRequest,
  flushProblem,
  provideApiTesting,
  settle,
} from '../../../testing/http';
import { PUBLIC_TOKEN, SLUG, publicOrder } from '../../../testing/public-menu';
import { getPublicOrder } from '../../api/generated/fn/public-orders/get-public-order';
import { OrderStatus } from '../../api/generated/models/order-status';
import { PublicOrder } from '../../api/generated/models/public-order';
import { RealtimeState } from '../../core/realtime/kitchen-realtime.service';
import { OrderRealtimeService } from '../../core/realtime/order-realtime.service';
import { OrderHistoryStore } from '../../core/storage/order-history.store';
import { OrderTrackingPage } from './order-tracking.page';

const LINE = 'order-line';
const ORDER_URL = apiUrl(getPublicOrder.PATH, { publicToken: PUBLIC_TOKEN });
const READY_BANNER = 'Seu pedido está pronto — retire no balcão';

/** Stand-in for the root OrderRealtimeService, driven by the spec. */
class FakeOrderRealtime {
  readonly state = signal<RealtimeState>('connected');
  readonly orderUpdated$ = new Subject<PublicOrder>();
  readonly resync$ = new Subject<void>();
  readonly watch = vi.fn(async (_token: string) => {});
  readonly stop = vi.fn(async () => {});
}

describe('OrderTrackingPage', () => {
  let fixture: ComponentFixture<OrderTrackingPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;
  let realtime: FakeOrderRealtime;

  beforeEach(() => {
    localStorage.clear();
    realtime = new FakeOrderRealtime();
    TestBed.configureTestingModule({
      providers: [...provideApiTesting(), { provide: OrderRealtimeService, useValue: realtime }],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  /** Renders the page for PUBLIC_TOKEN (route input) and answers getPublicOrder. */
  async function render(order: PublicOrder | 'not-found' = publicOrder()): Promise<void> {
    fixture = TestBed.createComponent(OrderTrackingPage);
    fixture.componentRef.setInput('publicToken', PUBLIC_TOKEN);
    el = fixture.nativeElement;
    await settle(fixture);

    const req = expectRequest(httpMock, 'GET', ORDER_URL);
    if (order === 'not-found') flushProblem(req, 404, 'order_not_found');
    else req.flush(order);
    await settle(fixture);
  }

  const status = () => textOf(el.querySelector('[data-testid="order-status"]'));
  const banner = () =>
    Array.from(el.querySelectorAll('[role="alert"]')).find((a) => textOf(a).includes(READY_BANNER));

  describe('static content', () => {
    it('loads getPublicOrder for the publicToken route input', async () => {
      await render();
      expect(textOf(el)).toContain('K7QX');
    });

    it('highlights the pickup code', async () => {
      await render();

      expect(textOf(el.querySelector('[data-testid="pickup-code"]'))).toBe('K7QX');
    });

    it('lists each item with quantity, unit price and subtotal, and the total', async () => {
      await render();

      expect(rows(el, LINE)).toHaveLength(2);
      const caipirinha = textOf(rowWith(el, LINE, 'Caipirinha'));
      expect(caipirinha).toMatch(/\b2\b/);
      expect(caipirinha).toContain('R$ 19,90');
      expect(caipirinha).toContain('R$ 39,80');
      const batata = textOf(rowWith(el, LINE, 'Batata frita'));
      expect(batata).toMatch(/\b1\b/);
      expect(batata).toContain('R$ 30,00');

      expect(textOf(el.querySelector('[data-testid="order-total"]'))).toContain('R$ 69,80');
    });

    it('shows the establishment and the customer name when present', async () => {
      await render(publicOrder({ customerName: '<b>Ana</b>' }));

      expect(textOf(el)).toContain('Bar do Zé');
      expect(textOf(el)).toContain('<b>Ana</b>');
      expect(Array.from(el.querySelectorAll('b')).some((b) => textOf(b) === 'Ana')).toBe(false);
    });

    it('404 shows "Pedido não encontrado"', async () => {
      await render('not-found');

      expect(textOf(el)).toContain('Pedido não encontrado');
      expect(el.querySelector('[data-testid="pickup-code"]')).toBeNull();
    });
  });

  describe('status labels', () => {
    it.each<[OrderStatus, string]>([
      ['paid', 'Pago / aguardando preparo'],
      ['in_preparation', 'Em preparo'],
      ['picked_up', 'Retirado'],
    ])('%s shows "%s"', async (orderStatus, label) => {
      await render(publicOrder({ status: orderStatus }));

      expect(status()).toContain(label);
      expect(banner()).toBeUndefined();
    });

    it('ready shows the highlighted banner with the pickup code in role="alert"', async () => {
      await render(publicOrder({ status: 'ready' }));

      const alert = banner();
      expect(alert, `role="alert" with "${READY_BANNER}"`).toBeDefined();
      expect(textOf(alert)).toContain('K7QX');
    });
  });

  describe('realtime', () => {
    it('watches the order on the orders hub', async () => {
      await render();

      expect(realtime.watch).toHaveBeenCalledWith(PUBLIC_TOKEN);
    });

    it('applies OrderUpdated without reloading', async () => {
      await render();

      realtime.orderUpdated$.next(publicOrder({ status: 'in_preparation' }));
      await settle(fixture);
      expect(status()).toContain('Em preparo');

      realtime.orderUpdated$.next(publicOrder({ status: 'ready' }));
      await settle(fixture);
      expect(banner()).toBeDefined();

      expectNoRequest(httpMock, 'GET', ORDER_URL);
    });

    it('ignores OrderUpdated events of another order', async () => {
      await render();

      realtime.orderUpdated$.next(publicOrder({ publicToken: 'x'.repeat(43), status: 'ready' }));
      await settle(fixture);

      expect(status()).toContain('Pago / aguardando preparo');
      expect(banner()).toBeUndefined();
    });

    it('reloads the order through getPublicOrder on resync', async () => {
      await render();

      realtime.resync$.next();
      await settle(fixture);
      expectRequest(httpMock, 'GET', ORDER_URL).flush(publicOrder({ status: 'picked_up' }));
      await settle(fixture);

      expect(status()).toContain('Retirado');
    });

    it('stops watching when the page is destroyed', async () => {
      await render();

      fixture.destroy();
      await Promise.resolve();

      expect(realtime.stop).toHaveBeenCalled();
    });
  });

  it('opening the page remembers the order in OrderHistoryStore', async () => {
    const remember = vi.spyOn(TestBed.inject(OrderHistoryStore), 'remember');
    const order = publicOrder();

    await render(order);

    expect(remember).toHaveBeenCalledWith({
      publicToken: PUBLIC_TOKEN,
      slug: SLUG,
      pickupCode: order.pickupCode,
      createdAt: order.createdAt,
    });
  });
});
