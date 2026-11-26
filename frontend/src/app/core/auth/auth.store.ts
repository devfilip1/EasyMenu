import { Injectable, computed, signal } from '@angular/core';
import { AuthResponse } from '../../api/generated/models/auth-response';

/** localStorage key holding the serialized `AuthResponse` of the current session. */
export const AUTH_STORAGE_KEY = 'easymenu.session';

/** Holds the authenticated session (JWT + current user), persisted in localStorage. */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly session = signal<AuthResponse | null>(null);

  readonly token = computed(() => this.session()?.accessToken ?? null);
  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isOwner = computed(() => this.user()?.role === 'owner');
  readonly isKitchen = computed(() => this.user()?.role === 'kitchen');

  setSession(session: AuthResponse): void {
    const { accessToken, expiresAt, user } = session;
    const stored: AuthResponse = { accessToken, expiresAt, user };
    this.session.set(stored);
    writeStorage(JSON.stringify(stored));
  }

  /** Rehydrates the session from localStorage; expired or corrupted sessions are discarded. */
  restore(): void {
    const stored = parseSession(readStorage());
    if (stored && !isExpired(stored)) {
      this.session.set(stored);
      return;
    }
    this.logout();
  }

  logout(): void {
    this.session.set(null);
    writeStorage(null);
  }
}

function isExpired(session: AuthResponse): boolean {
  const expiresAt = Date.parse(session.expiresAt);
  return Number.isNaN(expiresAt) || expiresAt <= Date.now();
}

function parseSession(raw: string | null): AuthResponse | null {
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as Partial<AuthResponse> | null;
    if (
      typeof value?.accessToken === 'string' &&
      typeof value.expiresAt === 'string' &&
      typeof value.user === 'object' &&
      value.user !== null
    ) {
      return { accessToken: value.accessToken, expiresAt: value.expiresAt, user: value.user };
    }
  } catch {
    // Corrupted value: treated as logged out.
  }
  return null;
}

function readStorage(): string | null {
  try {
    return localStorage.getItem(AUTH_STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStorage(value: string | null): void {
  try {
    if (value === null) localStorage.removeItem(AUTH_STORAGE_KEY);
    else localStorage.setItem(AUTH_STORAGE_KEY, value);
  } catch {
    // Storage unavailable (private mode, quota): the session lives only in memory.
  }
}
