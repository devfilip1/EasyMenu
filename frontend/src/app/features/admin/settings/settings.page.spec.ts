import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  button,
  byLabel,
  fieldOf,
  isInvalid,
  textOf,
  typeInto,
} from '../../../../testing/dom';
import {
  apiUrl,
  expectNoRequest,
  expectRequest,
  flushProblem,
  provideApiTesting,
  settle,
} from '../../../../testing/http';
import { getEstablishment } from '../../../api/generated/fn/establishment/get-establishment';
import { updateEstablishment } from '../../../api/generated/fn/establishment/update-establishment';
import { Establishment } from '../../../api/generated/models/establishment';
import { SettingsPage } from './settings.page';

const GET_URL = apiUrl(getEstablishment.PATH);
const PUT_URL = apiUrl(updateEstablishment.PATH);

const ESTABLISHMENT: Establishment = {
  id: '33333333-3333-4333-8333-333333333333',
  name: 'Bar do Zé',
  slug: 'bar-do-ze',
};

describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(SettingsPage);
    el = fixture.nativeElement;
    await settle(fixture);
    expectRequest(httpMock, 'GET', GET_URL).flush(ESTABLISHMENT);
    await settle(fixture);
  });

  afterEach(() => httpMock.verify());

  const nameInput = () => byLabel(el, /^nome/i);
  const save = () => button(el, /salvar/i);

  it('shows the slug as read-only', () => {
    const editable = Array.from(el.querySelectorAll<HTMLInputElement>('input, textarea')).filter(
      (c) => c.value === ESTABLISHMENT.slug && !c.readOnly && !c.disabled,
    );
    const shown =
      textOf(el).includes(ESTABLISHMENT.slug) ||
      Array.from(el.querySelectorAll('input')).some((c) => c.value === ESTABLISHMENT.slug);

    expect(shown).toBe(true);
    expect(editable).toEqual([]);
  });

  it('shows the public menu link /m/{slug}', () => {
    const anchor = Array.from(el.querySelectorAll('a')).find((a) =>
      (a.getAttribute('href') ?? '').endsWith(`/m/${ESTABLISHMENT.slug}`),
    );
    expect(anchor).toBeDefined();
  });

  it('prefills the name and renames through updateEstablishment', async () => {
    expect(nameInput().value).toBe('Bar do Zé');

    typeInto(nameInput(), 'Bar do Zé 2');
    await settle(fixture);
    save().click();
    await settle(fixture);

    const req = expectRequest(httpMock, 'PUT', PUT_URL);
    expect(req.request.body).toEqual({ name: 'Bar do Zé 2' });
    req.flush({ ...ESTABLISHMENT, name: 'Bar do Zé 2' });
    await settle(fixture);

    expect(nameInput().value).toBe('Bar do Zé 2');
  });

  it.each(['A', 'x'.repeat(101)])('validates the name length (2–100): "%s"', async (value) => {
    typeInto(nameInput(), value);
    await settle(fixture);
    save().click();
    await settle(fixture);

    expectNoRequest(httpMock, 'PUT', PUT_URL);
    expect(isInvalid(nameInput())).toBe(true);
  });

  it('shows server validation errors on the field', async () => {
    typeInto(nameInput(), 'Nome válido');
    await settle(fixture);
    save().click();
    await settle(fixture);

    flushProblem(expectRequest(httpMock, 'PUT', PUT_URL), 400, 'validation_failed', {
      errors: { name: ['O nome não pode conter apenas espaços.'] },
    });
    await settle(fixture);

    expect(textOf(fieldOf(nameInput()))).toContain('O nome não pode conter apenas espaços.');
    expect(isInvalid(nameInput())).toBe(true);
  });
});
