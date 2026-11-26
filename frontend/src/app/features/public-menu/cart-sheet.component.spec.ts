import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import {
  alertText,
  button,
  byLabel,
  rowWith,
  rows,
  textOf,
  typeInto,
} from '../../../testing/dom';
import { apiUrl, expectRequest, flushProblem, provideApiTesting, settle } from '../../../testing/http';
import { BATATA, CAIPIRINHA, PUBLIC_TOKEN, SLUG, publicOrder } from '../../../testing/public-menu';
import { placeOrder } from '../../api/generated/fn/public-orders/place-order';
import { OrderHistoryStore } from '../../core/storage/order-history.store';
import { CartStore } from './cart.store';
import { CartSheetComponent } from './cart-sheet.component';

const LINE = 'cart-line';
const ORDER_URL = apiUrl(placeOrder.PATH, { slug: SLUG });

describe('CartSheetComponent', () => {
  let fixture: ComponentFixture<CartSheetComponent>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;
  let router: Router;
  let cart: CartStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    cart = TestBed.inject(CartStore);
    cart.useSlug(SLUG);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(CartSheetComponent);
    fixture.componentRef.setInput('slug', SLUG);
    el = fixture.nativeElement;
    await settle(fixture);
  }

  /** Caipirinha ×2 (19,90) + Batata ×1 (30,00) = R$ 69,80. */
  async function renderWithItems(): Promise<void> {
    cart.add(CAIPIRINHA);
    cart.add(CAIPIRINHA);
    cart.add(BATATA);
    await render();
  }

  const buy = () => button(el, /comprar/i);
  const total = () => textOf(el.querySelector('[data-testid="cart-total"]'));

  async function clickBuy(): Promise<void> {
    buy().click();
    await settle(fixture);
  }

  it('shows each line with quantity and subtotal, and the total', async () => {
    await renderWithItems();

    expect(rows(el, LINE)).toHaveLength(2);
    const caipirinha = textOf(rowWith(el, LINE, 'Caipirinha'));
    expect(caipirinha).toContain('2');
    expect(caipirinha).toContain('R$ 39,80');
    expect(textOf(rowWith(el, LINE, 'Batata frita'))).toContain('R$ 30,00');
    expect(total()).toContain('R$ 69,80');
  });

  it('quantity controls update the cart', async () => {
    await renderWithItems();

    button(rowWith(el, LINE, 'Batata frita'), /aumentar/i).click();
    await settle(fixture);
    expect(textOf(rowWith(el, LINE, 'Batata frita'))).toContain('R$ 60,00');
    expect(total()).toContain('R$ 99,80');

    button(rowWith(el, LINE, 'Caipirinha'), /diminuir/i).click();
    await settle(fixture);
    expect(textOf(rowWith(el, LINE, 'Caipirinha'))).toContain('R$ 19,90');

    button(rowWith(el, LINE, 'Caipirinha'), /remover/i).click();
    await settle(fixture);
    expect(rows(el, LINE)).toHaveLength(1);
    expect(total()).toContain('R$ 60,00');
  });

  it('has an optional name field limited to 60 characters', async () => {
    await renderWithItems();

    const name = byLabel(el, /seu nome/i);
    expect(name.getAttribute('maxlength')).toBe('60');
    expect(name.required).toBe(false);
  });

  it('"Comprar" is disabled with an empty cart', async () => {
    await render();

    expect(buy().disabled).toBe(true);
  });

  it('sends only customerName and items[{menuItemId, quantity}] through placeOrder', async () => {
    await renderWithItems();
    typeInto(byLabel(el, /seu nome/i), 'Ana');
    await settle(fixture);

    await clickBuy();

    const req = expectRequest(httpMock, 'POST', ORDER_URL);
    expect(req.request.body).toEqual({
      customerName: 'Ana',
      items: [
        { menuItemId: CAIPIRINHA.id, quantity: 2 },
        { menuItemId: BATATA.id, quantity: 1 },
      ],
    });
    req.flush(publicOrder({ customerName: 'Ana' }), { status: 201, statusText: 'Created' });
    await settle(fixture);
  });

  it('buys without a name (no prices or totals in the body)', async () => {
    await renderWithItems();

    await clickBuy();

    const req = expectRequest(httpMock, 'POST', ORDER_URL);
    const body = req.request.body as Record<string, unknown>;
    expect(Object.keys(body).every((k) => ['customerName', 'items'].includes(k))).toBe(true);
    expect(body['customerName'] ?? null).toBeNull();
    expect(JSON.stringify(body)).not.toMatch(/price|total|cents/i);
    req.flush(publicOrder(), { status: 201, statusText: 'Created' });
    await settle(fixture);
  });

  it('disables "Comprar" while the order is being placed', async () => {
    await renderWithItems();

    await clickBuy();
    const req = expectRequest(httpMock, 'POST', ORDER_URL);
    expect(buy().disabled).toBe(true);

    flushProblem(req, 409, 'items_unavailable', { unavailableItems: [] });
    await settle(fixture);
    expect(buy().disabled).toBe(false);
  });

  it('201 remembers the order, clears the cart and opens /pedido/{publicToken}', async () => {
    const remember = vi.spyOn(TestBed.inject(OrderHistoryStore), 'remember').mockImplementation(() => {});
    await renderWithItems();

    await clickBuy();
    const order = publicOrder();
    expectRequest(httpMock, 'POST', ORDER_URL).flush(order, { status: 201, statusText: 'Created' });
    await settle(fixture);

    expect(remember).toHaveBeenCalledWith({
      publicToken: PUBLIC_TOKEN,
      slug: SLUG,
      pickupCode: order.pickupCode,
      createdAt: order.createdAt,
    });
    expect(cart.lines()).toEqual([]);
    const target = vi.mocked(router.navigateByUrl).mock.calls.at(-1)?.[0];
    const url = target instanceof UrlTree ? router.serializeUrl(target) : target;
    expect(url).toBe(`/pedido/${PUBLIC_TOKEN}`);
  });

  it('409 items_unavailable flags the lines, names the items and keeps the cart', async () => {
    await renderWithItems();

    await clickBuy();
    flushProblem(expectRequest(httpMock, 'POST', ORDER_URL), 409, 'items_unavailable', {
      unavailableItems: [{ menuItemId: BATATA.id, name: 'Batata frita' }],
    });
    await settle(fixture);

    expect(alertText(el)).toContain('Itens indisponíveis: Batata frita');
    expect(textOf(rowWith(el, LINE, 'Batata frita'))).toContain('Indisponível');
    expect(textOf(rowWith(el, LINE, 'Caipirinha'))).not.toContain('Indisponível');
    expect(cart.lines()).toHaveLength(2);
    expect(cart.lines().find((l) => l.menuItemId === BATATA.id)?.unavailable).toBe(true);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('409 for a removed item (no name in the response) uses the name from the cart', async () => {
    await renderWithItems();

    await clickBuy();
    flushProblem(expectRequest(httpMock, 'POST', ORDER_URL), 409, 'items_unavailable', {
      unavailableItems: [{ menuItemId: CAIPIRINHA.id }],
    });
    await settle(fixture);

    expect(alertText(el)).toContain('Itens indisponíveis: Caipirinha');
  });

  it('429 rate_limited shows the wait message, keeps the cart and re-enables "Comprar" (T102)', async () => {
    await renderWithItems();

    await clickBuy();
    const req = expectRequest(httpMock, 'POST', ORDER_URL);
    expect(buy().disabled).toBe(true);
    req.flush(
      { status: 429, title: 'Too Many Requests', code: 'rate_limited' },
      { status: 429, statusText: 'Too Many Requests', headers: { 'Retry-After': '30' } },
    );
    await settle(fixture);

    expect(alertText(el)).toContain('Muitas tentativas. Aguarde um instante e tente novamente.');
    expect(buy().disabled).toBe(false);
    expect(cart.lines()).toHaveLength(2);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
