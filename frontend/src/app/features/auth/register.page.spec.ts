import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { authSession } from '../../../testing/auth-session';
import {
  button,
  byLabel,
  describedErrorText,
  expectFieldError,
  fieldOf,
  firstInvalidControl,
  inPoliteLiveRegion,
  isInvalid,
  isRequired,
  queryByLabel,
  snapshotDescribedBy,
  textOf,
  typeInto,
} from '../../../testing/dom';
import {
  apiUrl,
  expectNoRequest,
  expectRequest,
  flushProblem,
  provideApiTesting,
  settle,
} from '../../../testing/http';
import { registerOwner } from '../../api/generated/fn/auth/register-owner';
import { AuthStore } from '../../core/auth/auth.store';
import { problemMessage } from '../../core/errors/problem';
import { RegisterPage } from './register.page';

const REGISTER_URL = apiUrl(registerOwner.PATH);

const VALID = {
  email: 'dono@bar.test',
  password: 'Senha-forte-1',
  establishmentName: 'Bar do Zé',
  slug: 'bar-do-ze',
};

type Field = keyof typeof VALID;

describe('RegisterPage', () => {
  let fixture: ComponentFixture<RegisterPage>;
  let el: HTMLElement;
  let httpMock: HttpTestingController;
  let router: Router;
  let setSession: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    setSession = vi.spyOn(TestBed.inject(AuthStore), 'setSession').mockImplementation(() => {});
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture = TestBed.createComponent(RegisterPage);
    el = fixture.nativeElement;
    await settle(fixture);
  });

  afterEach(() => httpMock.verify());

  const controls: Record<Field, () => HTMLInputElement> = {
    email: () => byLabel(el, /e-?mail/i),
    password: () => byLabel(el, /senha/i),
    establishmentName: () => byLabel(el, /nome do estabelecimento/i),
    slug: () => byLabel(el, /endere[çc]o/i),
  };
  const submit = () => button(el, /criar conta/i);

  async function fill(values: Partial<Record<Field, string>>): Promise<void> {
    // Name first: it suggests the slug, which an explicit slug value then overrides.
    for (const field of ['establishmentName', 'email', 'password', 'slug'] as Field[]) {
      if (values[field] !== undefined) typeInto(controls[field](), values[field]!);
    }
    await settle(fixture);
  }

  async function submitForm(): Promise<void> {
    submit().click();
    await settle(fixture);
  }

  function lastNavigation(): string | undefined {
    const target = vi.mocked(router.navigateByUrl).mock.calls.at(-1)?.[0];
    return target instanceof UrlTree ? router.serializeUrl(target) : target;
  }

  describe('validation', () => {
    it.each<[Field, string]>([
      ['email', 'nao-e-email'],
      ['email', ''],
      ['password', '1234567'],
      ['establishmentName', 'A'],
      ['establishmentName', 'x'.repeat(101)],
      ['slug', 'ab'],
      ['slug', 'Bar-do-Ze'],
      ['slug', '-bar'],
      ['slug', 'bar-'],
      ['slug', 'bar_do_ze'],
      ['slug', 'bar do ze'],
    ])('rejects %s = "%s" without calling the API', async (field, value) => {
      await fill({ ...VALID, [field]: value });
      await submitForm();

      expectNoRequest(httpMock, 'POST', REGISTER_URL);
      expect(isInvalid(controls[field]())).toBe(true);
    });

    it('accepts the boundary values (password with 8 chars, name with 2 and 100 chars)', async () => {
      await fill({ ...VALID, password: '12345678', establishmentName: 'AB' });
      await submitForm();
      expectRequest(httpMock, 'POST', REGISTER_URL).flush(authSession('owner'));
      await settle(fixture);

      expect(isInvalid(controls.establishmentName())).toBe(false);
    });

    it('accepts a 100-char establishment name', async () => {
      await fill({ ...VALID, establishmentName: 'x'.repeat(100) });
      await submitForm();
      expectRequest(httpMock, 'POST', REGISTER_URL).flush(authSession('owner'));
      await settle(fixture);
    });
  });

  describe('slug suggestion', () => {
    it('suggests the slug from the establishment name (lowercase, no accents, hyphens)', async () => {
      typeInto(controls.establishmentName(), 'Bar do Zé & Cia');
      await settle(fixture);
      expect(controls.slug().value).toBe('bar-do-ze-cia');

      typeInto(controls.establishmentName(), '  Açaí  Point 2  ');
      await settle(fixture);
      expect(controls.slug().value).toBe('acai-point-2');
    });

    it('stops suggesting once the owner edits the slug', async () => {
      typeInto(controls.establishmentName(), 'Bar do Zé');
      await settle(fixture);
      typeInto(controls.slug(), 'meu-bar');
      await settle(fixture);

      typeInto(controls.establishmentName(), 'Outro Nome');
      await settle(fixture);

      expect(controls.slug().value).toBe('meu-bar');
    });
  });

  describe('conflicts', () => {
    it('409 slug_taken shows "Este endereço já está em uso" on the slug field', async () => {
      await fill(VALID);
      await submitForm();
      flushProblem(expectRequest(httpMock, 'POST', REGISTER_URL), 409, 'slug_taken');
      await settle(fixture);

      expect(textOf(fieldOf(controls.slug()))).toContain('Este endereço já está em uso');
      expect(isInvalid(controls.slug())).toBe(true);
      expect(setSession).not.toHaveBeenCalled();
    });

    it('409 email_taken shows the message on the e-mail field', async () => {
      await fill(VALID);
      await submitForm();
      flushProblem(expectRequest(httpMock, 'POST', REGISTER_URL), 409, 'email_taken');
      await settle(fixture);

      expect(textOf(fieldOf(controls.email()))).toContain(problemMessage('email_taken'));
      expect(isInvalid(controls.email())).toBe(true);
      expect(textOf(fieldOf(controls.slug()))).not.toContain(problemMessage('email_taken'));
    });
  });

  it('on success sends the contract body, stores the session and goes to /admin/cardapio', async () => {
    const session = authSession('owner');

    await fill(VALID);
    await submitForm();
    const req = expectRequest(httpMock, 'POST', REGISTER_URL);
    expect(req.request.body).toMatchObject(VALID);
    req.flush(session, { status: 201, statusText: 'Created' });
    await settle(fixture);

    expect(setSession).toHaveBeenCalledWith(session);
    expect(lastNavigation()).toBe('/admin/cardapio');
  });

  describe('form accessibility (convention)', () => {
    it('marks the required fields with aria-required and the form as novalidate', () => {
      for (const field of ['email', 'password', 'establishmentName', 'slug'] as Field[]) {
        expect(isRequired(controls[field]()), field).toBe(true);
      }
      const ownerName = queryByLabel(el, /seu nome/i);
      if (ownerName) expect(isRequired(ownerName)).toBe(false);
      expect(el.querySelector('form')?.hasAttribute('novalidate')).toBe(true);
    });

    it('links every invalid field to its own error message', async () => {
      const fields = ['email', 'password', 'establishmentName', 'slug'] as Field[];
      // Taken before any error is shown: hints (e.g. the slug's) are not errors.
      const baselines = fields.map((field) => snapshotDescribedBy(controls[field]()));

      await submitForm();

      expectNoRequest(httpMock, 'POST', REGISTER_URL);
      const errors = fields.map((field, i) => expectFieldError(controls[field](), undefined, baselines[i]));
      expect(new Set(errors.map((e) => e.id)).size).toBe(errors.length);
    });

    it('focuses the first invalid control in DOM order', async () => {
      await submitForm();

      expect(document.activeElement).not.toBe(document.body);
      expect(document.activeElement).toBe(firstInvalidControl(el));
    });

    it('focuses the slug when it is the only invalid field', async () => {
      const baseline = snapshotDescribedBy(controls.slug());
      await fill({ ...VALID, slug: 'Bar Inválido' });
      await submitForm();

      expectNoRequest(httpMock, 'POST', REGISTER_URL);
      expectFieldError(controls.slug(), undefined, baseline);
      expect(document.activeElement).toBe(controls.slug());
    });

    it('409 slug_taken is the slug field error, inside a polite live region', async () => {
      await fill(VALID);
      await submitForm();
      flushProblem(expectRequest(httpMock, 'POST', REGISTER_URL), 409, 'slug_taken');
      await settle(fixture);

      const error = expectFieldError(controls.slug(), 'Este endereço já está em uso');
      expect(inPoliteLiveRegion(error)).toBe(true);
    });

    it('409 email_taken is the e-mail field error, inside a polite live region', async () => {
      await fill(VALID);
      await submitForm();
      flushProblem(expectRequest(httpMock, 'POST', REGISTER_URL), 409, 'email_taken');
      await settle(fixture);

      const error = expectFieldError(controls.email(), problemMessage('email_taken'));
      expect(inPoliteLiveRegion(error)).toBe(true);
      expect(describedErrorText(controls.slug())).not.toContain(problemMessage('email_taken'));
    });
  });
});
