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
      <div class="page title">
        <span class="brand">EasyMenu</span>
        @if (establishmentName(); as name) {
          <span class="muted establishment">{{ name }}</span>
        }
      </div>
      <!-- Wraps onto more lines instead of scrolling sideways (FR-039, 360 px and up). -->
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
        <span class="spacer" aria-hidden="true"></span>
        <a class="tab" routerLink="/cozinha">Painel da cozinha</a>
        <button class="tab tab-button" type="button" (click)="logout()">Sair</button>
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
    .title {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.5rem;
      padding-bottom: 0;
    }
    .establishment {
      overflow-wrap: anywhere;
    }
    .tabs {
      display: flex;
      flex-wrap: wrap;
      gap: 0 0.25rem;
      padding-top: 0.25rem;
      padding-bottom: 0;
    }
    .spacer {
      flex: 1 1 0;
    }
    .tab {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
      padding: 0 0.6rem;
      color: var(--text-muted);
      text-decoration: none;
      font: inherit;
      font-weight: 600;
      background: none;
      border: 0;
      border-bottom: 3px solid transparent;
      cursor: pointer;
    }
    .tab:hover {
      color: var(--text);
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
