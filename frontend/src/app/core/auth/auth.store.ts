import { Injectable, Signal, signal } from '@angular/core';
import { AuthResponse } from '../../api/generated/models/auth-response';
import { CurrentUser } from '../../api/generated/models/current-user';

/** localStorage key holding the serialized `AuthResponse` of the current session. */
export const AUTH_STORAGE_KEY = 'easymenu.session';

// Stub (TDD red phase, T016). Implemented in T018.
@Injectable({ providedIn: 'root' })
export class AuthStore {
  readonly token: Signal<string | null> = signal(null);
  readonly user: Signal<CurrentUser | null> = signal(null);
  readonly isAuthenticated: Signal<boolean> = signal(false);
  readonly isOwner: Signal<boolean> = signal(false);
  readonly isKitchen: Signal<boolean> = signal(false);

  setSession(_session: AuthResponse): void {
    throw new Error('Not implemented');
  }

  restore(): void {
    throw new Error('Not implemented');
  }

  logout(): void {
    throw new Error('Not implemented');
  }
}
