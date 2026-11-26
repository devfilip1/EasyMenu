import { HttpErrorResponse } from '@angular/common/http';
import { ErrorCode } from '../../api/generated/models/error-code';

// Stubs (TDD red phase, T016). Implemented in T018.

/** Extracts the stable `code` extension from a ProblemDetails error response, or `null`. */
export function problemCode(_error: HttpErrorResponse): ErrorCode | null {
  throw new Error('Not implemented');
}

/** pt-BR message for an `ErrorCode`; a generic message when the code is unknown or absent. */
export function problemMessage(_code: ErrorCode | null | undefined): string {
  throw new Error('Not implemented');
}
