import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  button,
  byLabel,
  chooseOption,
  rowWith,
  rows,
  textOf,
} from '../../../../testing/dom';
import { apiUrl, expectRequest, provideApiTesting, settle } from '../../../../testing/http';
import { listOrders } from '../../../api/generated/fn/orders/list-orders';
import { OrderStatus } from '../../../api/generated/models/order-status';
import { OrderSummary } from '../../../api/generated/models/order-summary';
import { OrderSummaryPage } from '../../../api/generated/models/order-summary-page';
import { OrdersPage } from './orders.page';

const ROW = 'order-row';
const LIST_URL = apiUrl(listOrders.PATH);

/** Admin labels of OrderStatus. */
const STATUS_LABELS: Record<OrderStatus, string> = {
  paid: 'Pago',
  in_preparation: 'Em preparo',
  ready: 'Pronto',
  picked_up: 'Retirado',
};

function summary(n: number, overrides: Partial<OrderSummary> = {}): OrderSummary {
  return {
    id: `order-${n}`,
    pickupCode: `K${n}${n}${n}`.slice(0, 4),
    customerName: null,
    status: 'paid',
    totalCents: 1990 * n,
    createdAt: new Date(Date.UTC(2026, 10, 26, 15, n)).toISOString(),
    ...overrides,
  };
}

function page(items: OrderSummary[], pageNumber = 1, totalCount = items.length): OrderSummaryPage {
  return { items, page: pageNumber, pageSize: 20, totalCount };
}

/** dd/MM/yyyy HH:mm in the local time zone (same as Angular's DatePipe). */
function dateTime(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

describe('OrdersPage', () => {
  let fixture: ComponentFixture<OrdersPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;

  const ANA = summary(2, { customerName: 'Ana', status: 'in_preparation', totalCents: 6980 });
  const NO_NAME = summary(1, { customerName: null, status: 'picked_up' });

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(OrdersPage);
    el = fixture.nativeElement;
    await settle(fixture);
  });

  afterEach(() => httpMock.verify());

  function expectList(): TestRequest {
    return expectRequest(httpMock, 'GET', LIST_URL);
  }

  async function answer(req: TestRequest, body: OrderSummaryPage): Promise<void> {
    req.flush(body);
    await settle(fixture);
  }

  it('loads the first page of all orders on init', async () => {
    const req = expectList();

    expect(req.request.params.has('status')).toBe(false);
    expect(req.request.params.get('page') ?? '1').toBe('1');
    await answer(req, page([ANA, NO_NAME]));

    expect(rows(el, ROW)).toHaveLength(2);
  });

  it('shows code, customer ("—" when absent), date/time, total and status in pt-BR', async () => {
    await answer(expectList(), page([ANA, NO_NAME]));

    const ana = textOf(rowWith(el, ROW, ANA.pickupCode));
    expect(ana).toContain('Ana');
    expect(ana).toContain(dateTime(ANA.createdAt));
    expect(ana).toContain('R$ 69,80');
    expect(ana).toContain(STATUS_LABELS.in_preparation);

    const anonymous = textOf(rowWith(el, ROW, NO_NAME.pickupCode));
    expect(anonymous).toContain('—');
    expect(anonymous).toContain(STATUS_LABELS.picked_up);
    expect(anonymous).toContain('R$ 19,90');
  });

  it('the status select filters through listOrders({status})', async () => {
    await answer(expectList(), page([ANA, NO_NAME]));
    const select = byLabel<HTMLSelectElement>(el, /status/i);

    chooseOption(select, /^retirado$/i);
    await settle(fixture);

    const req = expectList();
    expect(req.request.params.get('status')).toBe('picked_up');
    expect(req.request.params.get('page') ?? '1').toBe('1');
    await answer(req, page([NO_NAME]));
    expect(rows(el, ROW)).toHaveLength(1);

    chooseOption(select, /^todos$/i);
    await settle(fixture);
    const all = expectList();
    expect(all.request.params.has('status')).toBe(false);
    await answer(all, page([ANA, NO_NAME]));
  });

  describe('pagination', () => {
    it('moves between pages keeping the filter', async () => {
      const first = Array.from({ length: 20 }, (_, i) => summary(i + 1));
      await answer(expectList(), page(first, 1, 45));

      expect(textOf(el)).toContain('Página 1 de 3');
      expect(button(el, /anterior/i).disabled).toBe(true);

      chooseOption(byLabel<HTMLSelectElement>(el, /status/i), /^pago$/i);
      await settle(fixture);
      await answer(expectList(), page(first, 1, 45));

      button(el, /pr[óo]xima/i).click();
      await settle(fixture);
      const second = expectList();
      expect(second.request.params.get('page')).toBe('2');
      expect(second.request.params.get('status')).toBe('paid');
      await answer(second, page([summary(21)], 2, 45));

      expect(textOf(el)).toContain('Página 2 de 3');
      expect(button(el, /anterior/i).disabled).toBe(false);

      button(el, /anterior/i).click();
      await settle(fixture);
      const back = expectList();
      expect(back.request.params.get('page') ?? '1').toBe('1');
      await answer(back, page(first, 1, 45));
    });

    it('disables "Próxima" on the last page', async () => {
      await answer(expectList(), page([ANA, NO_NAME], 1, 2));

      expect(button(el, /pr[óo]xima/i).disabled).toBe(true);
    });
  });

  it('each row links to /admin/pedidos/:orderId', async () => {
    await answer(expectList(), page([ANA]));

    const anchor = rowWith(el, ROW, ANA.pickupCode).querySelector('a');
    expect(anchor?.getAttribute('href')).toBe(`/admin/pedidos/${ANA.id}`);
  });

  it('renders the customer name as text', async () => {
    await answer(expectList(), page([summary(3, { customerName: '<b>Zé</b>' })]));

    expect(textOf(el)).toContain('<b>Zé</b>');
    expect(Array.from(el.querySelectorAll('b')).some((b) => textOf(b) === 'Zé')).toBe(false);
  });
});
