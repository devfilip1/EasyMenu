import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { authSession } from '../../../testing/auth-session';
import {
  alertText,
  button,
  byLabel,
  fieldOf,
  isInvalid,
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
import { Api } from '../../api/generated/api';
import { login } from '../../api/generated/fn/auth/login';
import { Role } from '../../api/generated/models/role';
import { AuthStore } from '../../core/auth/auth.store';
import { LoginPage } from './login.page';

const LOGIN_URL = apiUrl(login.PATH);

describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
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

    fixture = TestBed.createComponent(LoginPage);
    el = fixture.nativeElement;
    await settle(fixture);
  });

  afterEach(() => httpMock.verify());

  const email = () => byLabel(el, /e-?mail/i);
  const password = () => byLabel(el, /senha/i);
  const submit = () => button(el, /entrar/i);

  async function fillAndSubmit(mail = 'dono@bar.test', pass = 'Senha-forte-1'): Promise<void> {
    typeInto(email(), mail);
    typeInto(password(), pass);
    await settle(fixture);
    submit().click();
    await settle(fixture);
  }

  function lastNavigation(): string | undefined {
    const target = vi.mocked(router.navigateByUrl).mock.calls.at(-1)?.[0];
    return target instanceof UrlTree ? router.serializeUrl(target) : target;
  }

  it('sends the credentials through the generated `login` function', async () => {
    const invoke = vi.spyOn(TestBed.inject(Api), 'invoke');

    await fillAndSubmit('dono@bar.test', 'Senha-forte-1');

    const req = expectRequest(httpMock, 'POST', LOGIN_URL);
    expect(req.request.body).toEqual({ email: 'dono@bar.test', password: 'Senha-forte-1' });
    expect(invoke).toHaveBeenCalled();
    expect(invoke.mock.calls[0][0]).toBe(login);
    req.flush(authSession('owner'));
    await settle(fixture);
  });

  it.each<[Role, string]>([
    ['owner', '/admin/cardapio'],
    ['kitchen', '/cozinha'],
  ])('stores the session and sends %s to %s', async (role, destination) => {
    const session = authSession(role);

    await fillAndSubmit();
    expectRequest(httpMock, 'POST', LOGIN_URL).flush(session);
    await settle(fixture);

    expect(setSession).toHaveBeenCalledWith(session);
    expect(lastNavigation()).toBe(destination);
  });

  it('shows a generic message on 401 without pointing at a field', async () => {
    await fillAndSubmit();
    flushProblem(expectRequest(httpMock, 'POST', LOGIN_URL), 401, 'invalid_credentials');
    await settle(fixture);

    expect(alertText(el)).toContain('E-mail ou senha inválidos');
    for (const control of [email(), password()]) {
      expect(isInvalid(control)).toBe(false);
      expect(textOf(fieldOf(control))).not.toContain('E-mail ou senha inválidos');
    }
    expect(setSession).not.toHaveBeenCalled();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('requires e-mail and password', async () => {
    submit().click();
    await settle(fixture);

    expectNoRequest(httpMock, 'POST', LOGIN_URL);
    expect(isInvalid(email())).toBe(true);
    expect(isInvalid(password())).toBe(true);
  });

  it('disables the submit button while the request is in flight', async () => {
    await fillAndSubmit();
    const req = expectRequest(httpMock, 'POST', LOGIN_URL);

    expect(submit().disabled).toBe(true);

    flushProblem(req, 401, 'invalid_credentials');
    await settle(fixture);
    expect(submit().disabled).toBe(false);
  });
});
