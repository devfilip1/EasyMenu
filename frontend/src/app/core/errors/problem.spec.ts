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

  it('uses a distinct message per code', () => {
    const messages = ERROR_CODE.map((code) => problemMessage(code));

    expect(new Set(messages).size).toBe(ERROR_CODE.length);
  });

  it('falls back to a generic message when the code is missing', () => {
    const generic = problemMessage(null);

    expect(generic.trim().length).toBeGreaterThan(0);
    expect(problemMessage(undefined)).toBe(generic);
    expect(ERROR_CODE.map((code) => problemMessage(code))).not.toContain(generic);
  });
});
