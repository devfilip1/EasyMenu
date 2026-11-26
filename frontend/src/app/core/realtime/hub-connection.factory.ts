import { InjectionToken } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';

export interface HubConnectionOptions {
  /** Called on every (re)connection; returns the current JWT (sent as `?access_token=`). */
  accessTokenFactory?: () => string | Promise<string>;
}

/** Builds a HubConnection for an absolute hub URL (e.g. `${apiUrl}/hubs/kitchen`). */
export type HubConnectionFactory = (url: string, options?: HubConnectionOptions) => HubConnection;

// Stub (TDD red phase, T064): only the token. The real factory (HubConnectionBuilder with
// withAutomaticReconnect) and its default provider are implemented in T065.
export const HUB_CONNECTION_FACTORY = new InjectionToken<HubConnectionFactory>('HUB_CONNECTION_FACTORY');
