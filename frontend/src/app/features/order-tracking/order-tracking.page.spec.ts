import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { rowWith, rows, textOf } from '../../../testing/dom';
import { apiUrl, expectRequest, flushProblem, provideApiTesting, settle } from '../../../testing/http';
import { PUBLIC_TOKEN, publicOrder } from '../../../testing/public-menu';
import { getPublicOrder } from '../../api/generated/fn/public-orders/get-public-order';
import { PublicOrder } from '../../api/generated/models/public-order';
import { OrderTrackingPage } from './order-tracking.page';

const LINE = 'order-line';
const ORDER_URL = apiUrl(getPublicOrder.PATH, { publicToken: PUBLIC_TOKEN });

describe('OrderTrackingPage (static)', () => {
  let fixture: ComponentFixture<OrderTrackingPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: provideApiTesting() });
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

  it('status paid shows "Pago / aguardando preparo"', async () => {
    await render();

    expect(textOf(el.querySelector('[data-testid="order-status"]'))).toContain(
      'Pago / aguardando preparo',
    );
  });

  it('404 shows "Pedido não encontrado"', async () => {
    await render('not-found');

    expect(textOf(el)).toContain('Pedido não encontrado');
    expect(el.querySelector('[data-testid="pickup-code"]')).toBeNull();
  });
});
