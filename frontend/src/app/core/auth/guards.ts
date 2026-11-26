import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

/** Owner-only area (/admin). Kitchen staff go to /cozinha; visitors to /login. */
export const ownerGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  if (!store.isAuthenticated()) return router.parseUrl('/login');
  return store.isOwner() ? true : router.parseUrl('/cozinha');
};

/** Kitchen panel (/cozinha): owners and kitchen staff. */
export const kitchenGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  return store.isOwner() || store.isKitchen() ? true : router.parseUrl('/login');
};

/** Login/register: visitors only; signed-in users go to their home area. */
export const guestGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  if (store.isOwner()) return router.parseUrl('/admin');
  if (store.isKitchen()) return router.parseUrl('/cozinha');
  return true;
};
