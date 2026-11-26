import { HttpErrorResponse } from '@angular/common/http';
import { ERROR_CODE } from '../../api/generated/models/error-code-array';
import { ProblemDetails } from '../../api/generated/models/problem-details';
import { problemCode, problemMessage } from './problem';

describe('problemCode', () => {
  it('extracts the code from a ProblemDetails body', () => {
    const body: ProblemDetails = { status: 409, title: 'Conflict', code: 'slug_taken' };
    const error = new HttpErrorResponse({ status: 409, error: body });

    expect(problemCode(error)).toBe('slug_taken');
  });

  it('returns null when the body is not a ProblemDetails', () => {
    expect(problemCode(new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') }))).toBeNull();
    expect(problemCode(new HttpErrorResponse({ status: 500, error: 'Internal Server Error' }))).toBeNull();
    expect(problemCode(new HttpErrorResponse({ status: 502, error: null }))).toBeNull();
  });
});

describe('problemMessage', () => {
  it.each(ERROR_CODE)('has a pt-BR message for "%s"', (code) => {
    const message = problemMessage(code);

    expect(message.trim().length).toBeGreaterThan(0);
    expect(message).not.toBe(code);
    expect(message).not.toMatch(/_/);
  });

  it.each([
    ['unauthorized', 'Sua sessão expirou. Entre novamente.'],
    ['forbidden', 'Você não tem permissão para esta ação.'],
  ] as const)('"%s" → "%s"', (code, message) => {
    expect(ERROR_CODE).toContain(code);
    expect(problemMessage(code)).toBe(message);
  });

  it('extracts the auth error codes from 401/403 bodies', () => {
    const unauthorized: ProblemDetails = { status: 401, code: 'unauthorized' };
    const forbidden: ProblemDetails = { status: 403, code: 'forbidden' };

    expect(problemCode(new HttpErrorResponse({ status: 401, error: unauthorized }))).toBe('unauthorized');
    expect(problemCode(new HttpErrorResponse({ status: 403, error: forbidden }))).toBe('forbidden');
  });

  it.each([
    ['rate_limited', 'Muitas tentativas. Aguarde um instante e tente novamente.'],
    ['payload_too_large', 'Arquivo grande demais (máx. 5 MB).'],
    ['image_too_large', 'Arquivo grande demais (máx. 5 MB).'],
  ] as const)('"%s" → "%s" (T102)', (code, message) => {
    expect(problemMessage(code)).toBe(message);
  });

  it('uses a distinct message per code (only the two "too large" codes share one)', () => {
    // payload_too_large (any request body) and image_too_large (photo) mean the same to the user.
    const shared = new Set(['payload_too_large', 'image_too_large']);
    const distinctCodes = ERROR_CODE.filter((code) => !shared.has(code));
    const messages = distinctCodes.map((code) => problemMessage(code));

    expect(new Set(messages).size).toBe(distinctCodes.length);
    expect(messages).not.toContain(problemMessage('image_too_large'));
  });

  it('falls back to a generic message when the code is missing', () => {
    const generic = problemMessage(null);

    expect(generic.trim().length).toBeGreaterThan(0);
    expect(problemMessage(undefined)).toBe(generic);
    expect(ERROR_CODE.map((code) => problemMessage(code))).not.toContain(generic);
  });
});
