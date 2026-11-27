import type { KitchenOrder, Station } from '../../src/app/api/generated/models';
import { API_URL } from './api';

/**
 * Kitchen actions for E2E (T077), called straight on the API with the Owner's token (the Owner
 * may operate the kitchen, FR-033a). Plain `fetch`, so they work from any test without a
 * Playwright `request` context.
 */

async function kitchenCall<T>(token: string, method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(`E2E kitchen call failed: ${method} ${path} -> HTTP ${response.status} ${await response.text()}`);
  }
  return (await response.json()) as T;
}

/** GET /api/kitchen/orders and find the order a guest got by its pickup code. */
export async function orderIdByPickupCode(token: string, pickupCode: string): Promise<string> {
  const orders = await kitchenCall<KitchenOrder[]>(token, 'GET', '/api/kitchen/orders');
  const order = orders.find((o) => o.pickupCode === pickupCode);
  if (!order) throw new Error(`E2E: no kitchen order with pickup code ${pickupCode}`);
  return order.id;
}

/** PUT /api/kitchen/orders/{orderId}/parts/{station}/status. */
export function advancePart(
  token: string,
  orderId: string,
  station: Station,
  status: 'in_preparation' | 'ready',
): Promise<KitchenOrder> {
  return kitchenCall<KitchenOrder>(
    token,
    'PUT',
    `/api/kitchen/orders/${encodeURIComponent(orderId)}/parts/${station}/status`,
    { status },
  );
}

/**
 * PUT /api/kitchen/orders/{orderId}/pickup with `ConfirmPickupRequest { pickupCode }` (FR-027a):
 * only for ready orders, and the code must be this order's pickup code.
 */
export function pickUp(token: string, orderId: string, pickupCode: string): Promise<KitchenOrder> {
  return kitchenCall<KitchenOrder>(
    token,
    'PUT',
    `/api/kitchen/orders/${encodeURIComponent(orderId)}/pickup`,
    { pickupCode },
  );
}
