import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthStore } from './auth.store';

const API_PREFIX = `${environment.apiUrl}/api/`;

/** Sends the Bearer token to the API only; a 401 ends the session and goes to /login. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_PREFIX)) return next(req);

  const store = inject(AuthStore);
  const router = inject(Router);
  const token = store.token();
  const authorized = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        store.logout();
        void router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};
