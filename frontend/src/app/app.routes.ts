import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'm/:slug',
    title: 'Cardápio',
    loadComponent: () => import('./pages/public-menu/public-menu.page').then((m) => m.PublicMenuPage),
  },
  {
    path: 'pedido/:id',
    title: 'Pedido',
    loadComponent: () =>
      import('./pages/order-tracking/order-tracking.page').then((m) => m.OrderTrackingPage),
  },
  {
    path: 'admin',
    title: 'Admin',
    loadComponent: () => import('./pages/admin/admin.page').then((m) => m.AdminPage),
  },
  {
    path: 'cozinha',
    title: 'Cozinha',
    loadComponent: () => import('./pages/kitchen/kitchen.page').then((m) => m.KitchenPage),
  },
  {
    path: 'login',
    title: 'Entrar',
    loadComponent: () => import('./pages/login/login.page').then((m) => m.LoginPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' },
];
