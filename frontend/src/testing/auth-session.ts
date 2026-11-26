import { AuthResponse } from '../app/api/generated/models/auth-response';
import { Role } from '../app/api/generated/models/role';

const HOUR_MS = 60 * 60 * 1000;

/** Builds a contract-shaped `AuthResponse` for specs. Expires in 1h unless `expiresAt` is given. */
export function authSession(role: Role = 'owner', overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    accessToken: `token-${role}`,
    expiresAt: new Date(Date.now() + HOUR_MS).toISOString(),
    user: {
      id: role === 'owner' ? '11111111-1111-4111-8111-111111111111' : '22222222-2222-4222-8222-222222222222',
      name: role === 'owner' ? 'Dona Maria' : null,
      email: `${role}@bar-do-ze.test`,
      role,
      establishment: {
        id: '33333333-3333-4333-8333-333333333333',
        name: 'Bar do Zé',
        slug: 'bar-do-ze',
      },
    },
    ...overrides,
  };
}

export function expiredAuthSession(role: Role = 'owner'): AuthResponse {
  return authSession(role, { expiresAt: new Date(Date.now() - HOUR_MS).toISOString() });
}
