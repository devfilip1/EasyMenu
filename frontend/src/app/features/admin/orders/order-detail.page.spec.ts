import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { rowWith, rows, textOf } from '../../../../testing/dom';
import { apiUrl, expectRequest, flushProblem, provideApiTesting, settle } from '../../../../testing/http';
import { getOrder } from '../../../api/generated/fn/orders/get-order';
import { OrderDetail } from '../../../api/generated/models/order-detail';
import { OrderDetailPage } from './order-detail.page';

const ORDER_ID = '0193f6c2-0000-7000-8000-0000000000aa';
const URL = apiUrl(getOrder.PATH, { orderId: ORDER_ID });

/** Snapshot prices: the menu may have changed since, the detail must show these. */
const DETAIL: OrderDetail = {
  id: ORDER_ID,
  pickupCode: 'K7QX',
  customerName: 'Ana',
  status: 'in_preparation',
  totalCents: 6980,
  createdAt: '2026-11-26T15:00:00Z',
  pickedUpAt: null,
  items: [
    { name: 'Caipirinha', unitPriceCents: 1990, quantity: 2, subtotalCents: 3980, station: 'bar' },
    { name: 'Batata frita', unitPriceCents: 3000, quantity: 1, subtotalCents: 3000, station: 'kitchen' },
  ],
  parts: [
    { station: 'bar', status: 'ready' },
    { station: 'kitchen', status: 'in_preparation' },
  ],
};

describe('OrderDetailPage', () => {
  let fixture: ComponentFixture<OrderDetailPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** Renders for ORDER_ID (route input `orderId`) and answers getOrder. */
  async function render(detail: OrderDetail | 'not-found' = DETAIL): Promise<void> {
    fixture = TestBed.createComponent(OrderDetailPage);
    fixture.componentRef.setInput('orderId', ORDER_ID);
    el = fixture.nativeElement;
    await settle(fixture);

    const req = expectRequest(httpMock, 'GET', URL);
    if (detail === 'not-found') flushProblem(req, 404, 'not_found');
    else req.flush(detail);
    await settle(fixture);
  }

  it('loads getOrder for the orderId route input and shows code and customer', async () => {
    await render();

    expect(textOf(el)).toContain('K7QX');
    expect(textOf(el)).toContain('Ana');
  });

  it('lists the items with the snapshot unit price, quantity and subtotal, and the total', async () => {
    await render();

    expect(rows(el, 'order-line')).toHaveLength(2);
    const caipirinha = textOf(rowWith(el, 'order-line', 'Caipirinha'));
    expect(caipirinha).toContain('R$ 19,90');
    expect(caipirinha).toMatch(/\b2\b/);
    expect(caipirinha).toContain('R$ 39,80');
    expect(textOf(rowWith(el, 'order-line', 'Batata frita'))).toContain('R$ 30,00');

    expect(textOf(el.querySelector('[data-testid="order-total"]'))).toContain('R$ 69,80');
  });

  it('shows each part with its station and status in pt-BR', async () => {
    await render();

    const parts = rows(el, 'order-part');
    expect(parts).toHaveLength(2);
    expect(textOf(rowWith(el, 'order-part', 'Bar'))).toContain('Pronto');
    expect(textOf(rowWith(el, 'order-part', 'Cozinha'))).toContain('Em preparo');
  });

  it('shows the order status in pt-BR', async () => {
    await render();

    expect(textOf(el.querySelector('[data-testid="order-status"]'))).toContain('Em preparo');
  });

  it('404 shows "Pedido não encontrado"', async () => {
    await render('not-found');

    expect(textOf(el)).toContain('Pedido não encontrado');
    expect(rows(el, 'order-line')).toEqual([]);
  });
});
