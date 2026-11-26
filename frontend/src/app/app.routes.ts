import { Routes } from '@angular/router';
import { guestGuard, kitchenGuard, ownerGuard } from './core/auth/guards';

export const routes: Routes = [
  {
    path: 'm/:slug',
    title: 'Cardápio',
    loadComponent: () =>
      import('./features/public-menu/public-menu.page').then((m) => m.PublicMenuPage),
  },
  {
    path: 'pedido/:publicToken',
    title: 'Pedido',
    loadComponent: () =>
      import('./features/order-tracking/order-tracking.page').then((m) => m.OrderTrackingPage),
  },
  {
    path: 'admin',
    title: 'Admin',
    canActivate: [ownerGuard],
    loadComponent: () =>
      import('./features/admin/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'cardapio' },
      {
        path: 'cardapio',
        title: 'Cardápio',
        loadComponent: () => import('./features/admin/menu/menu.page').then((m) => m.MenuPage),
      },
      {
        path: 'equipe',
        title: 'Equipe',
        loadComponent: () => import('./features/admin/staff/staff.page').then((m) => m.StaffPage),
      },
      {
        path: 'pedidos',
        title: 'Pedidos',
        loadComponent: () =>
          import('./features/admin/orders/orders.page').then((m) => m.OrdersPage),
      },
      {
        path: 'pedidos/:orderId',
        title: 'Pedido',
        loadComponent: () =>
          import('./features/admin/orders/order-detail.page').then((m) => m.OrderDetailPage),
      },
      {
        path: 'estabelecimento',
        title: 'Estabelecimento',
        loadComponent: () =>
          import('./features/admin/settings/settings.page').then((m) => m.SettingsPage),
      },
    ],
  },
  {
    path: 'cozinha',
    title: 'Cozinha',
    canActivate: [kitchenGuard],
    loadComponent: () => import('./features/kitchen/kitchen.page').then((m) => m.KitchenPage),
  },
  {
    path: 'login',
    title: 'Entrar',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'cadastro',
    title: 'Cadastro',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' },
];
