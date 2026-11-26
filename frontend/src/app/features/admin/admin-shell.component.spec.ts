import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { button, link } from '../../../testing/dom';
import { provideApiTesting, settle } from '../../../testing/http';
import { AuthStore } from '../../core/auth/auth.store';
import { AdminShellComponent } from './admin-shell.component';

describe('AdminShellComponent', () => {
  let fixture: ComponentFixture<AdminShellComponent>;
  let el: HTMLElement;
  let router: Router;
  let logout: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    router = TestBed.inject(Router);
    logout = vi.spyOn(TestBed.inject(AuthStore), 'logout').mockImplementation(() => {});
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture = TestBed.createComponent(AdminShellComponent);
    el = fixture.nativeElement;
    await settle(fixture);
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it.each([
    [/^card[áa]pio$/i, '/admin/cardapio'],
    [/^equipe$/i, '/admin/equipe'],
    [/^pedidos$/i, '/admin/pedidos'],
    [/^estabelecimento$/i, '/admin/estabelecimento'],
  ])('has a navigation link %s → %s', (name, href) => {
    expect(link(el, name).getAttribute('href')).toBe(href);
  });

  it('links to the kitchen panel', () => {
    expect(link(el, /painel da cozinha/i).getAttribute('href')).toBe('/cozinha');
  });

  it('renders the child routes in a router outlet', () => {
    expect(el.querySelector('router-outlet')).not.toBeNull();
  });

  it('"Sair" logs out and goes to /login', async () => {
    button(el, /^sair$/i).click();
    await settle(fixture);

    expect(logout).toHaveBeenCalled();
    const target = vi.mocked(router.navigateByUrl).mock.calls.at(-1)?.[0];
    const url = target instanceof UrlTree ? router.serializeUrl(target) : target;
    expect(url).toBe('/login');
  });
});
