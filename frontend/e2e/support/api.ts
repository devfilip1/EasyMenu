import { randomUUID } from 'node:crypto';
import type { APIRequestContext, APIResponse } from '@playwright/test';
import type {
  AuthResponse,
  Category,
  MenuItem,
  MenuItemRequest,
  PlaceOrderLine,
  PublicOrder,
  RegisterOwnerRequest,
} from '../../src/app/api/generated/models';

/**
 * Test-data helpers that talk to the real API through Playwright's `request` fixture.
 * Only E2E setup code may call the API directly; the app itself always uses the generated client.
 */
export const API_URL = process.env['E2E_API_URL'] ?? 'http://localhost:5080';

export const DEFAULT_PASSWORD = 'Senha-forte-123';

/** An authenticated owner plus the credentials and slug used to create it. */
export interface OwnerSession extends AuthResponse {
  email: string;
  password: string;
  slug: string;
}

/** Short unique token (lowercase alphanumerics) so parallel tests never collide. */
export function uniqueId(): string {
  return `${Date.now().toString(36)}${randomUUID().replace(/-/g, '').slice(0, 8)}`;
}

/** Valid slug (`^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$`), unique per call. */
export function uniqueSlug(prefix = 'e2e'): string {
  return `${prefix}-${uniqueId()}`.slice(0, 50);
}

export function uniqueEmail(prefix = 'owner'): string {
  return `${prefix}-${uniqueId()}@e2e.easymenu.test`;
}

function bearer(session: Pick<AuthResponse, 'accessToken'>): Record<string, string> {
  return { Authorization: `Bearer ${session.accessToken}` };
}

async function json<T>(response: APIResponse, action: string): Promise<T> {
  if (!response.ok()) {
    throw new Error(
      `E2E setup failed: ${action} -> HTTP ${response.status()} ${await response.text()}`,
    );
  }
  return (await response.json()) as T;
}

/** POST /api/auth/register-owner with a unique e-mail and slug (overridable). */
export async function registerOwner(
  request: APIRequestContext,
  overrides: Partial<RegisterOwnerRequest> = {},
): Promise<OwnerSession> {
  const body: RegisterOwnerRequest = {
    name: 'Dono E2E',
    email: uniqueEmail(),
    password: DEFAULT_PASSWORD,
    establishmentName: 'Bar E2E',
    slug: uniqueSlug(),
    ...overrides,
  };
  const response = await request.post(`${API_URL}/api/auth/register-owner`, { data: body });
  const auth = await json<AuthResponse>(response, 'registerOwner');
  return { ...auth, email: body.email, password: body.password, slug: body.slug };
}

/** POST /api/auth/login. */
export async function login(
  request: APIRequestContext,
  email: string,
  password: string = DEFAULT_PASSWORD,
): Promise<AuthResponse> {
  const response = await request.post(`${API_URL}/api/auth/login`, {
    data: { email, password },
  });
  return json<AuthResponse>(response, 'login');
}

/** POST /api/categories as the given owner. */
export async function createCategory(
  request: APIRequestContext,
  session: Pick<AuthResponse, 'accessToken'>,
  name = `Categoria ${uniqueId()}`,
): Promise<Category> {
  const response = await request.post(`${API_URL}/api/categories`, {
    headers: bearer(session),
    data: { name },
  });
  return json<Category>(response, 'createCategory');
}

export type CreateItemInput = Pick<MenuItemRequest, 'categoryId'> & Partial<MenuItemRequest>;

/** POST /api/menu-items as the given owner (available kitchen item at R$ 19,90 by default). */
export async function createItem(
  request: APIRequestContext,
  session: Pick<AuthResponse, 'accessToken'>,
  input: CreateItemInput,
): Promise<MenuItem> {
  const body: MenuItemRequest = {
    name: `Item ${uniqueId()}`,
    priceCents: 1990,
    isAvailable: true,
    station: 'kitchen',
    ...input,
  };
  const response = await request.post(`${API_URL}/api/menu-items`, {
    headers: bearer(session),
    data: body,
  });
  return json<MenuItem>(response, 'createItem');
}

/** POST /api/public/establishments/{slug}/orders as a guest (simulated payment). */
export async function placeOrder(
  request: APIRequestContext,
  slug: string,
  items: PlaceOrderLine[],
  customerName?: string,
): Promise<PublicOrder> {
  const response = await request.post(
    `${API_URL}/api/public/establishments/${encodeURIComponent(slug)}/orders`,
    { data: { items, ...(customerName ? { customerName } : {}) } },
  );
  return json<PublicOrder>(response, 'placeOrder');
}

/** PUT /api/menu-items/{id}/availability as the given owner (e.g. sell out an item mid-test). */
export async function setItemAvailability(
  request: APIRequestContext,
  session: Pick<AuthResponse, 'accessToken'>,
  menuItemId: string,
  isAvailable: boolean,
): Promise<MenuItem> {
  const response = await request.put(
    `${API_URL}/api/menu-items/${encodeURIComponent(menuItemId)}/availability`,
    { headers: bearer(session), data: { isAvailable } },
  );
  return json<MenuItem>(response, 'setItemAvailability');
}
