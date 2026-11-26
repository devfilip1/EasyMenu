import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  button,
  byLabel,
  expectFieldError,
  firstInvalidControl,
  inPoliteLiveRegion,
  isRequired,
  queryButton,
  rowWith,
  rows,
  snapshotDescribedBy,
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
import { createStaffMember } from '../../../api/generated/fn/staff/create-staff-member';
import { deactivateStaffMember } from '../../../api/generated/fn/staff/deactivate-staff-member';
import { listStaff } from '../../../api/generated/fn/staff/list-staff';
import { StaffMember } from '../../../api/generated/models/staff-member';
import { problemMessage } from '../../../core/errors/problem';
import { StaffPage } from './staff.page';

const ROW = 'staff-row';
const LIST_URL = apiUrl(listStaff.PATH);
const CREATE_URL = apiUrl(createStaffMember.PATH);

const JOAO: StaffMember = {
  id: 'staff-joao',
  name: 'João da Chapa',
  email: 'joao@bar.test',
  isActive: true,
  createdAt: '2026-11-20T12:00:00Z',
};
const MARIA: StaffMember = {
  id: 'staff-maria',
  name: 'Maria do Bar',
  email: 'maria@bar.test',
  isActive: false,
  createdAt: '2026-11-21T12:00:00Z',
};

const VALID = { name: 'Ana Cozinha', email: 'ana@bar.test', password: 'Senha-forte-1' };
type Field = keyof typeof VALID;

describe('StaffPage', () => {
  let fixture: ComponentFixture<StaffPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(StaffPage);
    el = fixture.nativeElement;
    await settle(fixture);
    expectRequest(httpMock, 'GET', LIST_URL).flush([JOAO, MARIA]);
    await settle(fixture);
  });

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  const form = () => el.querySelector('form')!;
  const controls: Record<Field, () => HTMLInputElement> = {
    name: () => byLabel(form(), /^nome/i),
    email: () => byLabel(form(), /e-?mail/i),
    password: () => byLabel(form(), /senha/i),
  };
  const create = () => button(form(), /cadastrar/i);

  async function fill(values: Partial<Record<Field, string>>): Promise<void> {
    for (const field of Object.keys(values) as Field[]) typeInto(controls[field](), values[field]!);
    await settle(fixture);
  }

  async function submit(): Promise<void> {
    create().click();
    await settle(fixture);
  }

  describe('list', () => {
    it('shows name, e-mail and status of each kitchen member', () => {
      expect(rows(el, ROW)).toHaveLength(2);

      const joao = textOf(rowWith(el, ROW, 'João da Chapa'));
      expect(joao).toContain('joao@bar.test');
      expect(joao).toContain('Ativo');
      expect(joao).not.toContain('Desativado');

      const maria = textOf(rowWith(el, ROW, 'Maria do Bar'));
      expect(maria).toContain('maria@bar.test');
      expect(maria).toContain('Desativado');
    });

    it('offers "Desativar" only for active members', () => {
      expect(queryButton(rowWith(el, ROW, 'João da Chapa'), /desativar/i)).not.toBeNull();
      expect(queryButton(rowWith(el, ROW, 'Maria do Bar'), /desativar/i)).toBeNull();
    });

    it('renders names as text', async () => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ providers: provideApiTesting() });
      httpMock = TestBed.inject(HttpTestingController);
      fixture = TestBed.createComponent(StaffPage);
      el = fixture.nativeElement;
      await settle(fixture);
      expectRequest(httpMock, 'GET', LIST_URL).flush([{ ...JOAO, name: '<b>João</b>' }]);
      await settle(fixture);

      expect(textOf(rowWith(el, ROW, 'joao@bar.test'))).toContain('<b>João</b>');
      expect(el.querySelector('b')).toBeNull();
    });
  });

  describe('create', () => {
    it('creates through createStaffMember and adds the member to the list', async () => {
      await fill(VALID);
      await submit();

      const req = expectRequest(httpMock, 'POST', CREATE_URL);
      expect(req.request.body).toEqual(VALID);
      req.flush(
        { id: 'staff-ana', name: 'Ana Cozinha', email: 'ana@bar.test', isActive: true, createdAt: '2026-11-26T12:00:00Z' },
        { status: 201, statusText: 'Created' },
      );
      await settle(fixture);

      expect(textOf(rowWith(el, ROW, 'Ana Cozinha'))).toContain('Ativo');
      expect(controls.password().value).toBe('');
    });

    it.each<[Field, string]>([
      ['name', ''],
      ['name', '   '],
      ['email', 'nao-e-email'],
      ['email', ''],
      ['password', '1234567'],
    ])('rejects %s = "%s" without calling the API', async (field, value) => {
      const baseline = snapshotDescribedBy(controls[field]());
      await fill({ ...VALID, [field]: value });
      await submit();

      expectNoRequest(httpMock, 'POST', CREATE_URL);
      expectFieldError(controls[field](), undefined, baseline);
      expect(document.activeElement).toBe(controls[field]());
    });

    it('accepts a password with exactly 8 characters', async () => {
      await fill({ ...VALID, password: '12345678' });
      await submit();

      expectRequest(httpMock, 'POST', CREATE_URL).flush(
        { id: 'staff-ana', name: VALID.name, email: VALID.email, isActive: true, createdAt: '2026-11-26T12:00:00Z' },
        { status: 201, statusText: 'Created' },
      );
      await settle(fixture);
    });

    it('409 email_taken is the e-mail field error, inside a polite live region', async () => {
      await fill(VALID);
      await submit();
      flushProblem(expectRequest(httpMock, 'POST', CREATE_URL), 409, 'email_taken');
      await settle(fixture);

      const error = expectFieldError(controls.email(), problemMessage('email_taken'));
      expect(inPoliteLiveRegion(error)).toBe(true);
      expect(rows(el, ROW)).toHaveLength(2);
    });

    describe('form accessibility (convention)', () => {
      it('marks name, e-mail and password as required and the form as novalidate', () => {
        for (const field of Object.keys(controls) as Field[]) {
          expect(isRequired(controls[field]()), field).toBe(true);
        }
        expect(form().hasAttribute('novalidate')).toBe(true);
      });

      it('links every missing field to its own error and focuses the first one', async () => {
        const fields = Object.keys(controls) as Field[];
        const baselines = fields.map((f) => snapshotDescribedBy(controls[f]()));

        await submit();

        expectNoRequest(httpMock, 'POST', CREATE_URL);
        const errors = fields.map((f, i) => expectFieldError(controls[f](), undefined, baselines[i]));
        expect(new Set(errors.map((e) => e.id)).size).toBe(errors.length);
        expect(document.activeElement).toBe(firstInvalidControl(form()));
      });
    });
  });

  describe('deactivate', () => {
    const url = apiUrl(deactivateStaffMember.PATH, { id: JOAO.id });

    it('asks for confirmation and does nothing when cancelled', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

      button(rowWith(el, ROW, 'João da Chapa'), /desativar/i).click();
      await settle(fixture);

      expect(confirm).toHaveBeenCalled();
      expectNoRequest(httpMock, 'PUT', url);
    });

    it('calls deactivateStaffMember and updates the row', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      button(rowWith(el, ROW, 'João da Chapa'), /desativar/i).click();
      await settle(fixture);
      expectRequest(httpMock, 'PUT', url).flush({ ...JOAO, isActive: false });
      await settle(fixture);

      const row = rowWith(el, ROW, 'João da Chapa');
      expect(textOf(row)).toContain('Desativado');
      expect(queryButton(row, /desativar/i)).toBeNull();
    });
  });
});
