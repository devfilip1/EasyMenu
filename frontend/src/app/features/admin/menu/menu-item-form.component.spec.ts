import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  alertText,
  button,
  byLabel,
  chooseOption,
  expectFieldError,
  firstInvalidControl,
  inPoliteLiveRegion,
  isInvalid,
  isRequired,
  setFiles,
  snapshotDescribedBy,
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
import { createMenuItem } from '../../../api/generated/fn/menu-items/create-menu-item';
import { deleteMenuItemPhoto } from '../../../api/generated/fn/menu-items/delete-menu-item-photo';
import { updateMenuItem } from '../../../api/generated/fn/menu-items/update-menu-item';
import { uploadMenuItemPhoto } from '../../../api/generated/fn/menu-items/upload-menu-item-photo';
import { Category } from '../../../api/generated/models/category';
import { ErrorCode } from '../../../api/generated/models/error-code';
import { MenuItem } from '../../../api/generated/models/menu-item';
import { problemMessage } from '../../../core/errors/problem';
import { MenuItemFormComponent } from './menu-item-form.component';

const PROMO_RULE = 'O preço promocional deve ser maior que zero e menor que o preço';
const CREATE_URL = apiUrl(createMenuItem.PATH);

const CATEGORIES: Category[] = [
  { id: 'cat-drinks', name: 'Drinks', position: 0, itemCount: 1 },
  { id: 'cat-porcoes', name: 'Porções', position: 1, itemCount: 0 },
];

const CAIPIRINHA: MenuItem = {
  id: 'item-caipirinha',
  categoryId: 'cat-drinks',
  name: 'Caipirinha',
  description: 'Limão, cachaça e açúcar',
  photoUrl: '/media/items/caipirinha.jpg',
  priceCents: 2500,
  promotionalPriceCents: 1990,
  effectivePriceCents: 1990,
  isAvailable: true,
  station: 'bar',
  position: 0,
};

describe('MenuItemFormComponent', () => {
  let fixture: ComponentFixture<MenuItemFormComponent>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;
  let saved: MenuItem[];

  async function render(item: MenuItem | null = null): Promise<void> {
    fixture = TestBed.createComponent(MenuItemFormComponent);
    fixture.componentRef.setInput('categories', CATEGORIES);
    fixture.componentRef.setInput('item', item);
    el = fixture.nativeElement;
    saved = [];
    fixture.componentInstance.saved.subscribe((value) => saved.push(value));
    await settle(fixture);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  const name = () => byLabel(el, /^nome/i);
  const description = () => byLabel<HTMLTextAreaElement>(el, /descri[çc][ãa]o/i);
  const price = () => byLabel(el, /^pre[çc]o(?!\s*promocional)/i);
  const promo = () => byLabel(el, /promocional/i);
  const station = () => byLabel<HTMLSelectElement>(el, /setor/i);
  const category = () => byLabel<HTMLSelectElement>(el, /categoria/i);
  const photo = () => byLabel(el, /^foto/i);
  const save = () => button(el, /salvar/i);

  async function fillValid(overrides: { price?: string; promo?: string } = {}): Promise<void> {
    typeInto(name(), 'Caipirinha');
    typeInto(description(), 'Limão, cachaça e açúcar');
    typeInto(price(), overrides.price ?? '25');
    typeInto(promo(), overrides.promo ?? '19,90');
    chooseOption(station(), /^bar$/i);
    chooseOption(category(), /^drinks$/i);
    await settle(fixture);
  }

  async function submit(): Promise<void> {
    save().click();
    await settle(fixture);
  }

  it('requires name, price, station and category', async () => {
    await render();
    await submit();

    expectNoRequest(httpMock, 'POST', CREATE_URL);
    for (const control of [name(), price(), station(), category()]) {
      expect(isInvalid(control)).toBe(true);
    }
  });

  it.each([
    ['equal to the price', '25,00'],
    ['greater than the price', '30,00'],
    ['zero', '0'],
    ['zero with cents', '0,00'],
  ])('blocks a promotional price %s', async (_case, promoValue) => {
    await render();
    await fillValid({ price: '25,00', promo: promoValue });
    await submit();

    expect(textOf(el)).toContain(PROMO_RULE);
    expect(isInvalid(promo())).toBe(true);
    expectNoRequest(httpMock, 'POST', CREATE_URL);
  });

  it('creates through createMenuItem sending integer cents', async () => {
    await render();
    await fillValid({ price: '25', promo: '19,90' });
    await submit();

    const req = expectRequest(httpMock, 'POST', CREATE_URL);
    expect(req.request.body).toMatchObject({
      categoryId: 'cat-drinks',
      name: 'Caipirinha',
      description: 'Limão, cachaça e açúcar',
      priceCents: 2500,
      promotionalPriceCents: 1990,
      isAvailable: true,
      station: 'bar',
    });
    expect(Number.isInteger(req.request.body.priceCents)).toBe(true);
    expect(Number.isInteger(req.request.body.promotionalPriceCents)).toBe(true);
    req.flush(CAIPIRINHA, { status: 201, statusText: 'Created' });
    await settle(fixture);

    expect(saved).toEqual([CAIPIRINHA]);
  });

  it('sends no promotional price when the field is empty', async () => {
    await render();
    await fillValid({ promo: '' });
    await submit();

    const req = expectRequest(httpMock, 'POST', CREATE_URL);
    expect(req.request.body.promotionalPriceCents ?? null).toBeNull();
    req.flush({ ...CAIPIRINHA, promotionalPriceCents: null, effectivePriceCents: 2500 });
    await settle(fixture);
  });

  it('edits through updateMenuItem with the fields prefilled', async () => {
    await render(CAIPIRINHA);

    expect(name().value).toBe('Caipirinha');
    expect(price().value).toBe('25,00');
    expect(promo().value).toBe('19,90');

    typeInto(name(), 'Caipirinha de limão');
    await settle(fixture);
    await submit();

    expectNoRequest(httpMock, 'POST', CREATE_URL);
    const req = expectRequest(httpMock, 'PUT', apiUrl(updateMenuItem.PATH, { id: CAIPIRINHA.id }));
    expect(req.request.body).toMatchObject({
      categoryId: 'cat-drinks',
      name: 'Caipirinha de limão',
      priceCents: 2500,
      promotionalPriceCents: 1990,
      station: 'bar',
      isAvailable: true,
    });
    const updated = { ...CAIPIRINHA, name: 'Caipirinha de limão' };
    req.flush(updated);
    await settle(fixture);

    expect(saved).toEqual([updated]);
  });

  describe('photo', () => {
    const file = () => new File([new Uint8Array([0xff, 0xd8, 0xff])], 'item.jpg', { type: 'image/jpeg' });
    const photoUrl = (id: string) => apiUrl(uploadMenuItemPhoto.PATH, { id });

    it('uploads the selected file after the item is saved', async () => {
      await render();
      await fillValid();
      setFiles(photo(), [file()]);
      await settle(fixture);
      await submit();

      const create = expectRequest(httpMock, 'POST', CREATE_URL);
      expectNoRequest(httpMock, 'PUT', photoUrl(CAIPIRINHA.id));
      create.flush({ ...CAIPIRINHA, photoUrl: null }, { status: 201, statusText: 'Created' });
      await settle(fixture);

      const upload = expectRequest(httpMock, 'PUT', photoUrl(CAIPIRINHA.id));
      expect(upload.request.body).toBeInstanceOf(FormData);
      const sent = (upload.request.body as FormData).get('file') as File;
      expect(sent.name).toBe('item.jpg');
      upload.flush(CAIPIRINHA);
      await settle(fixture);

      expect(saved.at(-1)).toEqual(CAIPIRINHA);
    });

    it.each<[number, ErrorCode]>([
      [400, 'invalid_image_type'],
      [413, 'image_too_large'],
    ])('%i %s shows the message and keeps the item saved', async (status, code) => {
      await render();
      await fillValid();
      setFiles(photo(), [file()]);
      await settle(fixture);
      await submit();

      const created = { ...CAIPIRINHA, photoUrl: null };
      expectRequest(httpMock, 'POST', CREATE_URL).flush(created, { status: 201, statusText: 'Created' });
      await settle(fixture);
      flushProblem(expectRequest(httpMock, 'PUT', photoUrl(CAIPIRINHA.id)), status, code);
      await settle(fixture);

      expect(alertText(el) + ' ' + textOf(el)).toContain(problemMessage(code));
      expect(saved).toEqual([created]);
      expectNoRequest(httpMock, 'DELETE', apiUrl(updateMenuItem.PATH, { id: CAIPIRINHA.id }));
    });

    it('"Remover foto" calls deleteMenuItemPhoto', async () => {
      await render(CAIPIRINHA);

      button(el, /remover foto/i).click();
      await settle(fixture);

      flushNoContent(
        expectRequest(httpMock, 'DELETE', apiUrl(deleteMenuItemPhoto.PATH, { id: CAIPIRINHA.id })),
      );
      await settle(fixture);
    });
  });

  describe('form accessibility (convention)', () => {
    const jpeg = () => new File([new Uint8Array([0xff, 0xd8, 0xff])], 'item.jpg', { type: 'image/jpeg' });

    it('marks name, price, station and category as required and the form as novalidate', async () => {
      await render();

      for (const control of [name(), price(), station(), category()]) {
        expect(isRequired(control), control.outerHTML.slice(0, 80)).toBe(true);
      }
      expect(isRequired(promo())).toBe(false);
      expect(isRequired(description())).toBe(false);
      expect(el.querySelector('form')?.hasAttribute('novalidate')).toBe(true);
    });

    it('links each missing required field to its own error and focuses the first one', async () => {
      await render();
      const required = [name(), price(), station(), category()];
      const baselines = required.map((control) => snapshotDescribedBy(control));

      await submit();

      expectNoRequest(httpMock, 'POST', CREATE_URL);
      const errors = required.map((control, i) => expectFieldError(control, undefined, baselines[i]));
      expect(new Set(errors.map((e) => e.id)).size).toBe(errors.length);
      expect(document.activeElement).not.toBe(document.body);
      expect(document.activeElement).toBe(firstInvalidControl(el));
    });

    it('links the promotion rule to the promotional price field and focuses it', async () => {
      await render();
      await fillValid({ price: '25,00', promo: '30,00' });

      await submit();

      expectNoRequest(httpMock, 'POST', CREATE_URL);
      expectFieldError(promo(), PROMO_RULE);
      expect(document.activeElement).toBe(promo());
    });

    it.each<[number, ErrorCode]>([
      [400, 'invalid_image_type'],
      [413, 'image_too_large'],
    ])('%i %s is the photo field error, inside a polite live region', async (status, code) => {
      await render();
      await fillValid();
      setFiles(photo(), [jpeg()]);
      await settle(fixture);
      await submit();

      expectRequest(httpMock, 'POST', CREATE_URL).flush(
        { ...CAIPIRINHA, photoUrl: null },
        { status: 201, statusText: 'Created' },
      );
      await settle(fixture);
      flushProblem(
        expectRequest(httpMock, 'PUT', apiUrl(uploadMenuItemPhoto.PATH, { id: CAIPIRINHA.id })),
        status,
        code,
      );
      await settle(fixture);

      const error = expectFieldError(photo(), problemMessage(code));
      expect(inPoliteLiveRegion(error)).toBe(true);
    });
  });
});
