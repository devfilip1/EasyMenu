import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  GuardResult,
  MaybeAsync,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { firstValueFrom, isObservable } from 'rxjs';
import { authSession } from '../../../testing/auth-session';
import { AuthStore } from './auth.store';
import { guestGuard, kitchenGuard, ownerGuard } from './guards';

type SessionKind = 'owner' | 'kitchen' | 'anonymous';

describe('route guards', () => {
  let store: AuthStore;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    store = TestBed.inject(AuthStore);
    router = TestBed.inject(Router);
  });

  afterEach(() => localStorage.clear());

  function signIn(kind: SessionKind): void {
    if (kind !== 'anonymous') store.setSession(authSession(kind));
  }

  /** Runs the guard and returns `true`/`false` or the serialized redirect URL. */
  async function run(guard: CanActivateFn, url: string): Promise<boolean | string> {
    const route = {} as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;
    const raw: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(() => guard(route, state));
    const result = isObservable(raw) ? await firstValueFrom(raw) : await raw;
    if (result instanceof UrlTree) return router.serializeUrl(result);
    if (typeof result === 'boolean') return result;
    throw new Error(`Unexpected guard result: ${JSON.stringify(result)}`);
  }

  describe('ownerGuard', () => {
    it('lets an owner through', async () => {
      signIn('owner');
      expect(await run(ownerGuard, '/admin')).toBe(true);
    });

    it('blocks kitchen staff', async () => {
      signIn('kitchen');
      expect(await run(ownerGuard, '/admin')).not.toBe(true);
    });

    it('sends a logged-out visitor to /login', async () => {
      signIn('anonymous');
      expect(await run(ownerGuard, '/admin')).toBe('/login');
    });
  });

  describe('kitchenGuard', () => {
    it.each<SessionKind>(['owner', 'kitchen'])('lets %s through', async (kind) => {
      signIn(kind);
      expect(await run(kitchenGuard, '/cozinha')).toBe(true);
    });

    it('sends a logged-out visitor to /login', async () => {
      signIn('anonymous');
      expect(await run(kitchenGuard, '/cozinha')).toBe('/login');
    });
  });

  describe('guestGuard', () => {
    it('lets a logged-out visitor through', async () => {
      signIn('anonymous');
      expect(await run(guestGuard, '/login')).toBe(true);
    });

    it('redirects an owner to /admin', async () => {
      signIn('owner');
      expect(await run(guestGuard, '/login')).toBe('/admin');
    });

    it('redirects kitchen staff to /cozinha', async () => {
      signIn('kitchen');
      expect(await run(guestGuard, '/cadastro')).toBe('/cozinha');
    });
  });
});
