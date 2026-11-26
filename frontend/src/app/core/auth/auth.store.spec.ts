import { isSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { authSession, expiredAuthSession } from '../../../testing/auth-session';
import { AUTH_STORAGE_KEY, AuthStore } from './auth.store';

describe('AuthStore', () => {
  let store: AuthStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    store = TestBed.inject(AuthStore);
  });

  afterEach(() => localStorage.clear());

  describe('setSession', () => {
    it('exposes token and user', () => {
      const session = authSession('owner');

      store.setSession(session);

      expect(store.token()).toBe(session.accessToken);
      expect(store.user()).toEqual(session.user);
      expect(store.isAuthenticated()).toBe(true);
    });

    it('persists token, expiration and user in localStorage', () => {
      const session = authSession('kitchen');

      store.setSession(session);

      const stored = JSON.parse(localStorage.getItem(AUTH_STORAGE_KEY) ?? 'null');
      expect(stored).toEqual({
        accessToken: session.accessToken,
        expiresAt: session.expiresAt,
        user: session.user,
      });
    });
  });

  describe('restore', () => {
    it('rehydrates a valid session from localStorage', () => {
      const session = authSession('owner');
      localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));

      store.restore();

      expect(store.token()).toBe(session.accessToken);
      expect(store.user()).toEqual(session.user);
      expect(store.isAuthenticated()).toBe(true);
      expect(store.isOwner()).toBe(true);
    });

    it('treats an expired session as logged out and clears it', () => {
      localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(expiredAuthSession('owner')));

      store.restore();

      expect(store.token()).toBeNull();
      expect(store.user()).toBeNull();
      expect(store.isAuthenticated()).toBe(false);
      expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    });

    it('stays logged out when nothing is stored', () => {
      store.restore();

      expect(store.token()).toBeNull();
      expect(store.isAuthenticated()).toBe(false);
    });

    it('stays logged out when the stored value is corrupted', () => {
      localStorage.setItem(AUTH_STORAGE_KEY, '{not json');

      store.restore();

      expect(store.token()).toBeNull();
      expect(store.isAuthenticated()).toBe(false);
    });
  });

  describe('logout', () => {
    it('clears the session from memory and localStorage', () => {
      store.setSession(authSession('owner'));

      store.logout();

      expect(store.token()).toBeNull();
      expect(store.user()).toBeNull();
      expect(store.isAuthenticated()).toBe(false);
      expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    });
  });

  describe('roles', () => {
    it('exposes isOwner and isKitchen as read-only computed signals', () => {
      for (const derived of [store.isOwner, store.isKitchen]) {
        expect(isSignal(derived)).toBe(true);
        expect('set' in derived).toBe(false);
      }
    });

    it('derives isOwner/isKitchen from the current session', () => {
      expect(store.isOwner()).toBe(false);
      expect(store.isKitchen()).toBe(false);

      store.setSession(authSession('owner'));
      expect(store.isOwner()).toBe(true);
      expect(store.isKitchen()).toBe(false);

      store.setSession(authSession('kitchen'));
      expect(store.isOwner()).toBe(false);
      expect(store.isKitchen()).toBe(true);

      store.logout();
      expect(store.isOwner()).toBe(false);
      expect(store.isKitchen()).toBe(false);
    });
  });
});
