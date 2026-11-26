import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentProviders, Provider } from '@angular/core';
import { ComponentFixture } from '@angular/core/testing';
import { Routes, provideRouter } from '@angular/router';
import { environment } from '../environments/environment';
import { provideApiConfiguration } from '../app/api/generated/api-configuration';
import { ErrorCode } from '../app/api/generated/models/error-code';

/** Providers for component specs: generated client over HttpTestingController + router. */
export function provideApiTesting(routes: Routes = []): (Provider | EnvironmentProviders)[] {
  return [
    provideRouter(routes),
    provideHttpClient(),
    provideHttpClientTesting(),
    provideApiConfiguration(environment.apiUrl),
  ];
}

/** Absolute URL of a generated function's PATH (`fn.PATH`), filling `{param}` placeholders. */
export function apiUrl(path: string, params: Record<string, string> = {}): string {
  const filled = path.replace(/\{(\w+)\}/g, (_, key: string) => {
    if (!(key in params)) throw new Error(`Missing path param ${key} for ${path}`);
    return encodeURIComponent(params[key]);
  });
  return `${environment.apiUrl}${filled}`;
}

/** Expects exactly one request with this method and URL (query string ignored). */
export function expectRequest(
  httpMock: HttpTestingController,
  method: string,
  url: string,
): TestRequest {
  return httpMock.expectOne(
    (req) => req.method === method && req.urlWithParams.split('?')[0] === url,
    `${method} ${url}`,
  );
}

export function expectNoRequest(httpMock: HttpTestingController, method: string, url: string): void {
  httpMock.expectNone(
    (req) => req.method === method && req.urlWithParams.split('?')[0] === url,
    `${method} ${url}`,
  );
}

/** Flushes a ProblemDetails error response (`application/problem+json`). */
export function flushProblem(
  req: TestRequest,
  status: number,
  code: ErrorCode,
  extra: Record<string, unknown> = {},
): void {
  req.flush({ status, title: 'Error', code, ...extra }, { status, statusText: 'Error' });
}

export function flushNoContent(req: TestRequest): void {
  req.flush(null, { status: 204, statusText: 'No Content' });
}

/** Lets pending promises/HTTP callbacks run and re-renders the fixture. */
export async function settle(fixture: ComponentFixture<unknown>, rounds = 3): Promise<void> {
  for (let i = 0; i < rounds; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }
}
