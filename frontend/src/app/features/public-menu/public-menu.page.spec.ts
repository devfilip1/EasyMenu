import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { button, link, normalize, rowWith, rows, textOf } from '../../../testing/dom';
import { apiUrl, expectRequest, flushProblem, provideApiTesting, settle } from '../../../testing/http';
import {
  BATATA,
  CAIPIRINHA,
  CHOPP,
  SLUG,
  publicItem,
  publicMenu,
} from '../../../testing/public-menu';
import { getPublicMenu } from '../../api/generated/fn/public-menu/get-public-menu';
import { PublicMenu } from '../../api/generated/models/public-menu';
import { PLACEHOLDER_ITEM_IMAGE } from '../../shared/media-url.pipe';
import { OrderHistoryEntry, OrderHistoryStore } from '../../core/storage/order-history.store';
import { CartStore } from './cart.store';
import { PublicMenuPage } from './public-menu.page';

const CARD = 'menu-item-card';
const MENU_URL = apiUrl(getPublicMenu.PATH, { slug: SLUG });

describe('PublicMenuPage', () => {
  let fixture: ComponentFixture<PublicMenuPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;
  let cart: CartStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);
    cart = TestBed.inject(CartStore);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  /** Renders the page for SLUG (route input) and answers the menu request. */
  async function render(menu: PublicMenu | 'not-found' = publicMenu()): Promise<void> {
    fixture = TestBed.createComponent(PublicMenuPage);
    fixture.componentRef.setInput('slug', SLUG);
    el = fixture.nativeElement;
    await settle(fixture);

    const req = expectRequest(httpMock, 'GET', MENU_URL);
    if (menu === 'not-found') flushProblem(req, 404, 'establishment_not_found');
    else req.flush(menu);
    await settle(fixture);
  }

  const card = (name: string) => rowWith(el, CARD, name);

  it('loads getPublicMenu for the slug route input', async () => {
    await render();
    // `render` already asserted exactly one GET to /api/public/establishments/{slug}/menu.
    expect(textOf(el)).toContain('Bar do Zé');
  });

  it('shows the establishment name as the page heading', async () => {
    await render();

    expect(textOf(el.querySelector('h1'))).toBe('Bar do Zé');
  });

  it('shows categories and items in the menu order', async () => {
    await render();

    const headings = Array.from(el.querySelectorAll('h2')).map((h) => textOf(h));
    expect(headings).toEqual(['Drinks', 'Porções']);

    const names = rows(el, CARD).map((c) =>
      [CAIPIRINHA, CHOPP, BATATA].find((i) => textOf(c).includes(i.name))?.name,
    );
    expect(names).toEqual(['Caipirinha', 'Chopp', 'Batata frita']);
  });

  it('shows name and description of an item', async () => {
    await render();

    expect(textOf(card('Caipirinha'))).toContain('Limão, cachaça e açúcar');
  });

  it('highlights the promotional price and strikes the original', async () => {
    await render();

    const caipirinha = card('Caipirinha');
    const struck = caipirinha.querySelector('s');
    expect(struck).not.toBeNull();
    expect(normalize(struck!.textContent)).toBe('R$ 25,00');
    expect(textOf(caipirinha).replace(textOf(struck), '')).toContain('R$ 19,90');

    expect(card('Batata frita').querySelector('s')).toBeNull();
    expect(textOf(card('Batata frita'))).toContain('R$ 30,00');
  });

  it('marks sold-out items and disables their "Adicionar" button', async () => {
    await render();

    expect(textOf(card('Chopp'))).toContain('Esgotado');
    expect(button(card('Chopp'), /adicionar/i).disabled).toBe(true);
    expect(textOf(card('Caipirinha'))).not.toContain('Esgotado');
    expect(button(card('Caipirinha'), /adicionar/i).disabled).toBe(false);
  });

  it('"Adicionar" puts the item in the cart of this establishment', async () => {
    await render();
    const add = vi.spyOn(cart, 'add');

    button(card('Caipirinha'), /adicionar/i).click();
    await settle(fixture);

    expect(add).toHaveBeenCalledWith(CAIPIRINHA);
  });

  it('shows the item photo from the API, or the placeholder when there is none', async () => {
    await render();

    const photo = card('Caipirinha').querySelector('img');
    expect(photo?.getAttribute('src')).toBe(`${environment.apiUrl}/media/items/caipirinha.jpg`);
    expect(photo?.getAttribute('alt')).toBe('Caipirinha');

    const placeholder = card('Batata frita').querySelector('img');
    expect(placeholder?.getAttribute('src')).toBe(PLACEHOLDER_ITEM_IMAGE);
  });

  it('404 shows "Estabelecimento não encontrado"', async () => {
    await render('not-found');

    expect(textOf(el)).toContain('Estabelecimento não encontrado');
    expect(rows(el, CARD)).toEqual([]);
  });

  it('an empty menu shows "Cardápio indisponível no momento"', async () => {
    await render(publicMenu({ categories: [] }));

    expect(textOf(el)).toContain('Cardápio indisponível no momento');
    expect(textOf(el.querySelector('h1'))).toBe('Bar do Zé');
  });

  it('renders names and descriptions with markup as plain text', async () => {
    const evil = publicItem({
      id: 'item-evil',
      name: '<script>window.__pwned = true</script>Drink',
      description: '<img src="x" onerror="window.__pwned = true">Descrição',
    });
    await render(
      publicMenu({
        establishment: { name: '<b>Bar</b>', slug: SLUG },
        categories: [{ id: 'cat-x', name: '<i>Drinks</i>', items: [evil] }],
      }),
    );

    expect(el.querySelector('script')).toBeNull();
    expect(el.querySelector('img[src="x"]')).toBeNull();
    expect(el.querySelector('h1 b, h2 i')).toBeNull();
    expect(textOf(el)).toContain('<script>window.__pwned = true</script>Drink');
    expect(textOf(el)).toContain('<img src="x" onerror="window.__pwned = true">Descrição');
    expect(textOf(el.querySelector('h1'))).toBe('<b>Bar</b>');
    expect((window as unknown as { __pwned?: boolean }).__pwned).toBeUndefined();
  });

  describe('"Acompanhar meu pedido"', () => {
    const entry = (token: string, slug: string, minute: number): OrderHistoryEntry => ({
      publicToken: token.padEnd(43, 'x'),
      slug,
      pickupCode: 'K7QX',
      createdAt: new Date(Date.UTC(2026, 10, 26, 12, minute)).toISOString(),
    });

    it('links to the most recent order of this establishment', async () => {
      const history = TestBed.inject(OrderHistoryStore);
      history.remember(entry('older-here', SLUG, 1));
      history.remember(entry('latest-here', SLUG, 2));
      history.remember(entry('other-bar', 'outro-bar', 3));

      await render();

      const track = link(el, /acompanhar meu pedido/i);
      expect(track.getAttribute('href')).toBe(`/pedido/${'latest-here'.padEnd(43, 'x')}`);
    });

    it('is not shown without an order for this establishment', async () => {
      TestBed.inject(OrderHistoryStore).remember(entry('other-bar', 'outro-bar', 3));

      await render();

      const links = Array.from(el.querySelectorAll('a')).filter((a) =>
        /acompanhar meu pedido/i.test(textOf(a)),
      );
      expect(links).toEqual([]);
    });
  });
});
