import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  alertText,
  button,
  byLabel,
  expectFieldError,
  isRequired,
  queryButton,
  rowWith,
  rows,
  textOf,
  typeInto,
} from '../../../../testing/dom';
import {
  apiUrl,
  expectNoRequest,
  expectRequest,
  flushNoContent,
  flushProblem,
  provideApiTesting,
  settle,
} from '../../../../testing/http';
import { createCategory } from '../../../api/generated/fn/categories/create-category';
import { deleteCategory } from '../../../api/generated/fn/categories/delete-category';
import { listCategories } from '../../../api/generated/fn/categories/list-categories';
import { renameCategory } from '../../../api/generated/fn/categories/rename-category';
import { reorderCategories } from '../../../api/generated/fn/categories/reorder-categories';
import { Category } from '../../../api/generated/models/category';
import { CategoriesPanelComponent } from './categories-panel.component';

const ROW = 'category-row';

const DRINKS: Category = { id: 'cat-drinks', name: 'Drinks', position: 0, itemCount: 2 };
const PORCOES: Category = { id: 'cat-porcoes', name: 'Porções', position: 1, itemCount: 0 };
const SOBREMESAS: Category = { id: 'cat-sobremesas', name: 'Sobremesas', position: 2, itemCount: 1 };

describe('CategoriesPanelComponent', () => {
  let fixture: ComponentFixture<CategoriesPanelComponent>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(CategoriesPanelComponent);
    el = fixture.nativeElement;
    await settle(fixture);
    // Deliberately out of order: the panel must sort by `position`.
    expectRequest(httpMock, 'GET', apiUrl(listCategories.PATH)).flush([SOBREMESAS, DRINKS, PORCOES]);
    await settle(fixture);
  });

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  const names = () => rows(el, ROW).map((r) => textOf(r));
  const order = () => {
    const all = [DRINKS, PORCOES, SOBREMESAS, { name: 'Bebidas' }];
    return names().map((text) => all.find((c) => text.includes(c.name))?.name);
  };

  it('lists the categories ordered by position', () => {
    expect(order()).toEqual(['Drinks', 'Porções', 'Sobremesas']);
  });

  describe('create', () => {
    const newName = () => byLabel(el, /nova categoria/i);
    const add = () => button(el, /adicionar/i);

    it('keeps "Adicionar" disabled while the name is empty or blank', async () => {
      expect(add().disabled).toBe(true);

      typeInto(newName(), '   ');
      await settle(fixture);
      expect(add().disabled).toBe(true);

      typeInto(newName(), 'Petiscos');
      await settle(fixture);
      expect(add().disabled).toBe(false);
    });

    it('creates through createCategory and appends the new category', async () => {
      typeInto(newName(), 'Petiscos');
      await settle(fixture);
      add().click();
      await settle(fixture);

      const req = expectRequest(httpMock, 'POST', apiUrl(createCategory.PATH));
      expect(req.request.body).toEqual({ name: 'Petiscos' });
      req.flush(
        { id: 'cat-petiscos', name: 'Petiscos', position: 3, itemCount: 0 },
        { status: 201, statusText: 'Created' },
      );
      await settle(fixture);

      expect(textOf(rows(el, ROW).at(-1))).toContain('Petiscos');
    });
  });

  it('renames inline through renameCategory', async () => {
    button(rowWith(el, ROW, 'Drinks'), /renomear/i).click();
    await settle(fixture);

    // Drinks is the first row (position 0); while editing, its name lives in an inline input.
    const row = rows(el, ROW)[0];
    const input = row.querySelector<HTMLInputElement>('input');
    expect(input, 'inline input in the row').not.toBeNull();
    expect(input!.value).toBe('Drinks');
    typeInto(input!, 'Bebidas');
    await settle(fixture);
    button(row, /salvar/i).click();
    await settle(fixture);

    const req = expectRequest(httpMock, 'PUT', apiUrl(renameCategory.PATH, { id: DRINKS.id }));
    expect(req.request.body).toEqual({ name: 'Bebidas' });
    req.flush({ ...DRINKS, name: 'Bebidas' });
    await settle(fixture);

    expect(order()).toEqual(['Bebidas', 'Porções', 'Sobremesas']);
  });

  describe('reorder', () => {
    it('"Subir" sends the complete id list in the new order', async () => {
      button(rowWith(el, ROW, 'Sobremesas'), /subir/i).click();
      await settle(fixture);

      const req = expectRequest(httpMock, 'PUT', apiUrl(reorderCategories.PATH));
      expect(req.request.body).toEqual({ ids: [DRINKS.id, SOBREMESAS.id, PORCOES.id] });
      flushNoContent(req);
      await settle(fixture);

      expect(order()).toEqual(['Drinks', 'Sobremesas', 'Porções']);
    });

    it('"Descer" sends the complete id list in the new order', async () => {
      button(rowWith(el, ROW, 'Drinks'), /descer/i).click();
      await settle(fixture);

      const req = expectRequest(httpMock, 'PUT', apiUrl(reorderCategories.PATH));
      expect(req.request.body).toEqual({ ids: [PORCOES.id, DRINKS.id, SOBREMESAS.id] });
      flushNoContent(req);
      await settle(fixture);

      expect(order()).toEqual(['Porções', 'Drinks', 'Sobremesas']);
    });
  });

  describe('delete', () => {
    const deleteUrl = (id: string) => apiUrl(deleteCategory.PATH, { id });

    it('asks for confirmation and does nothing when cancelled', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

      button(rowWith(el, ROW, 'Porções'), /excluir/i).click();
      await settle(fixture);

      expect(confirm).toHaveBeenCalled();
      expectNoRequest(httpMock, 'DELETE', deleteUrl(PORCOES.id));
      expect(order()).toContain('Porções');
    });

    it('deletes after confirmation', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      button(rowWith(el, ROW, 'Porções'), /excluir/i).click();
      await settle(fixture);
      flushNoContent(expectRequest(httpMock, 'DELETE', deleteUrl(PORCOES.id)));
      await settle(fixture);

      expect(order()).toEqual(['Drinks', 'Sobremesas']);
    });

    it('409 category_not_empty explains how to proceed and keeps the category', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      button(rowWith(el, ROW, 'Drinks'), /excluir/i).click();
      await settle(fixture);
      flushProblem(expectRequest(httpMock, 'DELETE', deleteUrl(DRINKS.id)), 409, 'category_not_empty');
      await settle(fixture);

      expect(alertText(el)).toContain('Remova ou mova os itens antes de excluir a categoria');
      expect(order()).toContain('Drinks');
    });
  });

  describe('form accessibility (convention)', () => {
    it('the new category name is required', () => {
      expect(isRequired(byLabel(el, /nova categoria/i))).toBe(true);
    });

    it('the inline rename input is required and, when left blank, linked to its error', async () => {
      button(rowWith(el, ROW, 'Drinks'), /renomear/i).click();
      await settle(fixture);
      const row = rows(el, ROW)[0];
      const input = row.querySelector<HTMLInputElement>('input')!;
      expect(isRequired(input)).toBe(true);

      typeInto(input, '   ');
      await settle(fixture);
      const save = queryButton(row, /salvar/i);
      if (save && !save.disabled) {
        save.click();
        await settle(fixture);
      }

      expectNoRequest(httpMock, 'PUT', apiUrl(renameCategory.PATH, { id: DRINKS.id }));
      expectFieldError(input);
      expect(document.activeElement).toBe(input);
    });

    it('409 category_not_empty is a general error in role="alert"', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      button(rowWith(el, ROW, 'Drinks'), /excluir/i).click();
      await settle(fixture);
      flushProblem(
        expectRequest(httpMock, 'DELETE', apiUrl(deleteCategory.PATH, { id: DRINKS.id })),
        409,
        'category_not_empty',
      );
      await settle(fixture);

      expect(alertText(el)).toContain('Remova ou mova os itens antes de excluir a categoria');
    });
  });
});
