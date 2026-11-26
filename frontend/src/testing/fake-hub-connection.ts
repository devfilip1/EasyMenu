import type { HubConnection } from '@microsoft/signalr';
import { HubConnectionFactory, HubConnectionOptions } from '../app/core/realtime/hub-connection.factory';

type Handler = (...args: unknown[]) => void;

/** In-memory stand-in for a SignalR HubConnection, driven by the spec. */
export class FakeHubConnection {
  readonly handlers = new Map<string, Handler[]>();
  readonly reconnectingCallbacks: ((error?: Error) => void)[] = [];
  readonly reconnectedCallbacks: ((connectionId?: string) => void)[] = [];
  readonly closeCallbacks: ((error?: Error) => void)[] = [];
  startCalls = 0;
  stopCalls = 0;
  state = 'Disconnected';

  constructor(
    readonly url: string,
    readonly options: HubConnectionOptions | undefined,
  ) {}

  on(event: string, handler: Handler): void {
    this.handlers.set(event, [...(this.handlers.get(event) ?? []), handler]);
  }

  off(event: string): void {
    this.handlers.delete(event);
  }

  async start(): Promise<void> {
    this.startCalls++;
    this.state = 'Connected';
  }

  async stop(): Promise<void> {
    this.stopCalls++;
    this.state = 'Disconnected';
    this.closeCallbacks.forEach((cb) => cb());
  }

  invoke(): Promise<unknown> {
    return Promise.resolve(true);
  }

  onreconnecting(cb: (error?: Error) => void): void {
    this.reconnectingCallbacks.push(cb);
  }

  onreconnected(cb: (connectionId?: string) => void): void {
    this.reconnectedCallbacks.push(cb);
  }

  onclose(cb: (error?: Error) => void): void {
    this.closeCallbacks.push(cb);
  }

  // ---- test drivers

  emit(event: string, ...args: unknown[]): void {
    (this.handlers.get(event) ?? []).forEach((h) => h(...args));
  }

  simulateReconnecting(error = new Error('network')): void {
    this.state = 'Reconnecting';
    this.reconnectingCallbacks.forEach((cb) => cb(error));
  }

  simulateReconnected(): void {
    this.state = 'Connected';
    this.reconnectedCallbacks.forEach((cb) => cb('conn-2'));
  }

  simulateClose(error?: Error): void {
    this.state = 'Disconnected';
    this.closeCallbacks.forEach((cb) => cb(error));
  }
}

/** A factory for `HUB_CONNECTION_FACTORY` that records every connection it builds. */
export function fakeHubFactory(): { factory: HubConnectionFactory; connections: FakeHubConnection[] } {
  const connections: FakeHubConnection[] = [];
  const factory: HubConnectionFactory = (url, options) => {
    const connection = new FakeHubConnection(url, options);
    connections.push(connection);
    return connection as unknown as HubConnection;
  };
  return { factory, connections };
}
