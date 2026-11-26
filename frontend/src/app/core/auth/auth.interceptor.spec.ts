import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { authSession } from '../../../testing/auth-session';
import { Api } from '../../api/generated/api';
import { provideApiConfiguration } from '../../api/generated/api-configuration';
import { getCurrentUser } from '../../api/generated/fn/auth/get-current-user';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';

const ME_URL = `${environment.apiUrl}/api/auth/me`;
const EXTERNAL_URL = 'https://cdn.example.com/api/assets/logo.png';

describe('authInterceptor', () => {
  let api: Api;
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let store: AuthStore;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideApiConfiguration(environment.apiUrl),
      ],
    });
    api = TestBed.inject(Api);
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('adds the Bearer token to API requests when logged in', async () => {
    const session = authSession('owner');
    store.setSession(session);

    const result = api.invoke(getCurrentUser);
    const req = httpMock.expectOne(ME_URL);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${session.accessToken}`);
    req.flush(session.user);

    await expect(result).resolves.toEqual(session.user);
  });

  it('does not add Authorization when logged out', async () => {
    const result = api.invoke(getCurrentUser);
    const req = httpMock.expectOne(ME_URL);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(authSession().user);

    await result;
  });

  it('does not leak the token to URLs outside the API', async () => {
    store.setSession(authSession('owner'));

    const result = firstValueFrom(http.get(EXTERNAL_URL));
    const req = httpMock.expectOne(EXTERNAL_URL);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});

    await result;
  });

  it('logs out and navigates to /login on 401', async () => {
    store.setSession(authSession('owner'));
    const navigateByUrl = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    const result = api.invoke(getCurrentUser);
    httpMock
      .expectOne(ME_URL)
      .flush(
        { status: 401, title: 'Unauthorized', code: 'unauthorized' },
        { status: 401, statusText: 'Unauthorized' },
      );

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(store.isAuthenticated()).toBe(false);
    expect(store.token()).toBeNull();
    const target = navigateByUrl.mock.calls.at(-1)?.[0];
    const url = target instanceof UrlTree ? router.serializeUrl(target) : target;
    expect(url).toBe('/login');
  });

  it('does not log out on 403 forbidden (only 401 does)', async () => {
    const session = authSession('kitchen');
    store.setSession(session);
    const navigateByUrl = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const logout = vi.spyOn(store, 'logout');

    const result = api.invoke(getCurrentUser);
    httpMock
      .expectOne(ME_URL)
      .flush(
        { status: 403, title: 'Forbidden', code: 'forbidden' },
        { status: 403, statusText: 'Forbidden' },
      );

    await expect(result).rejects.toMatchObject({ status: 403, error: { code: 'forbidden' } });
    expect(logout).not.toHaveBeenCalled();
    expect(store.token()).toBe(session.accessToken);
    expect(store.isAuthenticated()).toBe(true);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('keeps the session on other errors (e.g. 404 not_found)', async () => {
    const session = authSession('owner');
    store.setSession(session);
    const navigateByUrl = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    const result = api.invoke(getCurrentUser);
    httpMock
      .expectOne(ME_URL)
      .flush(
        { status: 404, title: 'Not Found', code: 'not_found' },
        { status: 404, statusText: 'Not Found' },
      );

    await expect(result).rejects.toMatchObject({ status: 404 });
    expect(store.token()).toBe(session.accessToken);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });
});
