import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { button, byLabel, normalize, rowWith, rows, textOf } from '../../../../testing/dom';
import {
  apiUrl,
  expectNoRequest,
  expectRequest,
  flushNoContent,
  provideApiTesting,
  settle,
} from '../../../../testing/http';
import { reorderCategoryItems } from '../../../api/generated/fn/categories/reorder-category-items';
import { deleteMenuItem } from '../../../api/generated/fn/menu-items/delete-menu-item';
import { listMenuItems } from '../../../api/generated/fn/menu-items/list-menu-items';
import { setMenuItemAvailability } from '../../../api/generated/fn/menu-items/set-menu-item-availability';
import { MenuItem } from '../../../api/generated/models/menu-item';
import { expectNoPromotionLabels, expectPromotionLabels } from '../../../../testing/price-a11y';
import { MenuItemsListComponent } from './menu-items-list.component';

const ROW = 'menu-item-row';
const CATEGORY_ID = 'cat-drinks';

function item(overrides: Partial<MenuItem> & Pick<MenuItem, 'id' | 'name' | 'position'>): MenuItem {
  return {
    categoryId: CATEGORY_ID,
    description: null,
    photoUrl: null,
    priceCents: 1500,
    promotionalPriceCents: null,
    effectivePriceCents: 1500,
    isAvailable: true,
    station: 'bar',
    ...overrides,
  };
}

const CAIPIRINHA = item({
  id: 'item-caipirinha',
  name: 'Caipirinha',
  position: 0,
  priceCents: 2500,
  promotionalPriceCents: 1990,
  effectivePriceCents: 1990,
});
const CHOPP = item({ id: 'item-chopp', name: 'Chopp', position: 1, priceCents: 1200, effectivePriceCents: 1200 });
const GIN = item({ id: 'item-gin', name: 'Gin tônica', position: 2, isAvailable: false });

describe('MenuItemsListComponent', () => {
  let fixture: ComponentFixture<MenuItemsListComponent>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(MenuItemsListComponent);
    fixture.componentRef.setInput('categoryId', CATEGORY_ID);
    el = fixture.nativeElement;
    await settle(fixture);

    const req = expectRequest(httpMock, 'GET', apiUrl(listMenuItems.PATH));
    expect(req.request.params.get('categoryId')).toBe(CATEGORY_ID);
    // Deliberately out of order: the list must sort by `position`.
    req.flush([GIN, CAIPIRINHA, CHOPP]);
    await settle(fixture);
  });

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  const order = () =>
    rows(el, ROW).map((r) => [CAIPIRINHA, CHOPP, GIN].find((i) => textOf(r).includes(i.name))?.name);

  it('lists the category items ordered by position', () => {
    expect(order()).toEqual(['Caipirinha', 'Chopp', 'Gin tônica']);
  });

  it('shows the "Esgotado" badge only on unavailable items', () => {
    expect(textOf(rowWith(el, ROW, 'Gin tônica'))).toContain('Esgotado');
    expect(textOf(rowWith(el, ROW, 'Chopp'))).not.toContain('Esgotado');
  });

  it('shows a promotion with the original price struck through', () => {
    const row = rowWith(el, ROW, 'Caipirinha');
    const struck = row.querySelector('s, del');

    expect(struck).not.toBeNull();
    expect(normalize(struck!.textContent)).toBe('R$ 25,00');
    expect(textOf(row)).toContain('R$ 19,90');
    expect(rowWith(el, ROW, 'Chopp').querySelector('s, del')).toBeNull();
    expect(textOf(rowWith(el, ROW, 'Chopp'))).toContain('R$ 12,00');
  });

  it('the availability toggle calls setMenuItemAvailability', async () => {
    const toggle = byLabel(rowWith(el, ROW, 'Chopp'), /dispon[ií]vel/i);
    expect(toggle.checked).toBe(true);

    toggle.click();
    await settle(fixture);

    const req = expectRequest(
      httpMock,
      'PUT',
      apiUrl(setMenuItemAvailability.PATH, { id: CHOPP.id }),
    );
    expect(req.request.body).toEqual({ isAvailable: false });
    req.flush({ ...CHOPP, isAvailable: false });
    await settle(fixture);

    expect(textOf(rowWith(el, ROW, 'Chopp'))).toContain('Esgotado');
  });

  describe('delete', () => {
    const url = apiUrl(deleteMenuItem.PATH, { id: CHOPP.id });

    it('does nothing when the confirmation is cancelled', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

      button(rowWith(el, ROW, 'Chopp'), /excluir/i).click();
      await settle(fixture);

      expect(confirm).toHaveBeenCalled();
      expectNoRequest(httpMock, 'DELETE', url);
    });

    it('calls deleteMenuItem after confirmation and removes the row', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      button(rowWith(el, ROW, 'Chopp'), /excluir/i).click();
      await settle(fixture);
      flushNoContent(expectRequest(httpMock, 'DELETE', url));
      await settle(fixture);

      expect(order()).toEqual(['Caipirinha', 'Gin tônica']);
    });
  });

  describe('reorder', () => {
    const url = apiUrl(reorderCategoryItems.PATH, { id: CATEGORY_ID });

    it('"Subir" sends the complete id list in the new order', async () => {
      button(rowWith(el, ROW, 'Gin tônica'), /subir/i).click();
      await settle(fixture);

      const req = expectRequest(httpMock, 'PUT', url);
      expect(req.request.body).toEqual({ ids: [CAIPIRINHA.id, GIN.id, CHOPP.id] });
      flushNoContent(req);
      await settle(fixture);

      expect(order()).toEqual(['Caipirinha', 'Gin tônica', 'Chopp']);
    });

    it('"Descer" sends the complete id list in the new order', async () => {
      button(rowWith(el, ROW, 'Caipirinha'), /descer/i).click();
      await settle(fixture);

      const req = expectRequest(httpMock, 'PUT', url);
      expect(req.request.body).toEqual({ ids: [CHOPP.id, CAIPIRINHA.id, GIN.id] });
      flushNoContent(req);
      await settle(fixture);

      expect(order()).toEqual(['Chopp', 'Caipirinha', 'Gin tônica']);
    });
  });

  describe('accessible promotion text (T130)', () => {
    it('labels the original and the promotional price for screen readers', () => {
      expectPromotionLabels(rowWith(el, ROW, 'Caipirinha'), 'R$ 25,00', 'R$ 19,90');
    });

    it('shows neither label without a promotion', () => {
      expectNoPromotionLabels(rowWith(el, ROW, 'Chopp'));
      expectNoPromotionLabels(rowWith(el, ROW, 'Gin tônica'));
    });
  });
});
