import { InjectionToken, Provider } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export interface HubConnectionOptions {
  /** Called on every (re)connection; returns the current JWT (sent as `?access_token=`). */
  accessTokenFactory?: () => string | Promise<string>;
}

/** Builds a HubConnection for an absolute hub URL (e.g. `${apiUrl}/hubs/kitchen`). */
export type HubConnectionFactory = (url: string, options?: HubConnectionOptions) => HubConnection;

/** Retry delays after a dropped connection: quick first attempts, then every 30 s. */
const RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000];
const MAX_RECONNECT_DELAY_MS = 30_000;

/** Default factory: SignalR `HubConnectionBuilder` with automatic reconnection that never gives up. */
export const createHubConnection: HubConnectionFactory = (url, options = {}) =>
  new HubConnectionBuilder()
    .withUrl(url, { accessTokenFactory: options.accessTokenFactory })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
        RECONNECT_DELAYS_MS[previousRetryCount] ?? MAX_RECONNECT_DELAY_MS,
    })
    .configureLogging(LogLevel.Warning)
    .build();

/** Injectable factory, so specs can swap in a fake (`src/testing/fake-hub-connection.ts`). */
export const HUB_CONNECTION_FACTORY = new InjectionToken<HubConnectionFactory>(
  'HUB_CONNECTION_FACTORY',
  { providedIn: 'root', factory: () => createHubConnection },
);

/** Explicit provider of the default factory (the token already defaults to it). */
export function provideHubConnectionFactory(
  factory: HubConnectionFactory = createHubConnection,
): Provider {
  return { provide: HUB_CONNECTION_FACTORY, useValue: factory };
}
