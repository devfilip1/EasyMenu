import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';

/** Layout of the owner area: header, section navigation and the child route. */
@Component({
  selector: 'app-admin-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <header class="topbar">
      <div class="page row topbar-inner">
        <div class="grow">
          <span class="brand">EasyMenu</span>
          @if (establishmentName(); as name) {
            <span class="muted establishment">{{ name }}</span>
          }
        </div>
        <a class="btn btn-sm" routerLink="/cozinha">Painel da cozinha</a>
        <button class="btn btn-sm" type="button" (click)="logout()">Sair</button>
      </div>
      <nav class="page tabs" aria-label="Seções do painel">
        @for (section of sections; track section.path) {
          <a
            class="tab"
            [routerLink]="section.path"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
            >{{ section.label }}</a
          >
        }
      </nav>
    </header>
    <main class="page">
      <router-outlet />
    </main>
  `,
  styles: `
    .topbar {
      background: var(--surface);
      border-bottom: 1px solid var(--border);
      position: sticky;
      top: 0;
      z-index: 10;
    }
    .topbar-inner {
      padding-bottom: 0.25rem;
    }
    .grow {
      flex: 1 1 auto;
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.5rem;
    }
    .tabs {
      display: flex;
      gap: 0.25rem;
      overflow-x: auto;
      padding-top: 0;
      padding-bottom: 0;
    }
    .tab {
      padding: 0.6rem 0.75rem;
      color: var(--text-muted);
      text-decoration: none;
      font-weight: 600;
      white-space: nowrap;
      border-bottom: 3px solid transparent;
    }
    .tab.active {
      color: var(--primary);
      border-bottom-color: var(--primary);
    }
  `,
})
export class AdminShellComponent {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly establishmentName = computed(
    () => this.auth.user()?.establishment.name ?? null,
  );

  protected readonly sections = [
    { path: '/admin/cardapio', label: 'Cardápio' },
    { path: '/admin/equipe', label: 'Equipe' },
    { path: '/admin/pedidos', label: 'Pedidos' },
    { path: '/admin/estabelecimento', label: 'Estabelecimento' },
  ];

  protected logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
