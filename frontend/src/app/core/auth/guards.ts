import { CanActivateFn } from '@angular/router';

// Stubs (TDD red phase, T016). Implemented in T018.
export const ownerGuard: CanActivateFn = () => {
  throw new Error('Not implemented');
};

export const kitchenGuard: CanActivateFn = () => {
  throw new Error('Not implemented');
};

export const guestGuard: CanActivateFn = () => {
  throw new Error('Not implemented');
};
