import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../services/notification.service';

/// ExceptionHandlingMiddleware (all three services) writes `{ status, title }`
/// as application/problem+json for every handled exception. An unhandled
/// exception has no catch-all clause today, so it reaches the client as a
/// bodyless 500 - both shapes must be handled here.
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notificationService = inject(NotificationService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const message = error.error?.title ?? describeGenericError(error);
        notificationService.showError(message);
      }
      return throwError(() => error);
    }),
  );
};

function describeGenericError(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Could not reach the server. Check your connection and try again.';
  }
  return `Something went wrong (HTTP ${error.status}). Please try again.`;
}
