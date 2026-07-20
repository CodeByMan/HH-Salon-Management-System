import { Injectable } from '@angular/core';
import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { BehaviorSubject, catchError, filter, finalize, Observable, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';
import { SharedService } from '../services/shared.service';
import * as toastr from 'toastr';

@Injectable()
export class TokenInterceptor implements HttpInterceptor {
  private refreshing = false;
  private refreshCompleted$ = new BehaviorSubject<boolean | null>(null);

  constructor(private auth: AuthService, private router: Router, private sharedService: SharedService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const credentialedRequest = request.clone({ withCredentials: true });
    return next.handle(credentialedRequest).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 401 && !this.isAuthenticationRequest(request.url)) {
          return this.refreshAndRetry(credentialedRequest, next);
        }
        if (error instanceof HttpErrorResponse && error.status === 403) {
          toastr.error('You are not allowed to perform this action.', 'Error!');
        }
        return throwError(() => error);
      })
    );
  }

  private refreshAndRetry(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!this.auth.isLoggedIn()) {
      this.expireSession();
      return throwError(() => new Error('Authentication session has expired.'));
    }

    if (!this.refreshing) {
      this.refreshing = true;
      this.refreshCompleted$.next(null);
      return this.auth.renewSession().pipe(
        switchMap(() => {
          this.refreshCompleted$.next(true);
          return next.handle(request);
        }),
        catchError(error => {
          this.refreshCompleted$.next(false);
          this.expireSession();
          return throwError(() => error);
        }),
        finalize(() => this.refreshing = false)
      );
    }

    return this.refreshCompleted$.pipe(
      filter(result => result !== null),
      take(1),
      switchMap(result => result ? next.handle(request) : throwError(() => new Error('Authentication session has expired.')))
    );
  }

  private isAuthenticationRequest(url: string): boolean {
    return ['/Auth/me', '/Auth/authenticate', '/Auth/register', '/Auth/admin-register', '/Auth/worker-register', '/Auth/refresh', '/Auth/logout', '/Auth/send-reset-email', '/Auth/reset-password']
      .some(endpoint => url.includes(endpoint));
  }

  private expireSession() {
    this.auth.clearSession();
    this.sharedService.sendData(false);
    toastr.warning('Your session has expired. Please log in again.', 'Warning');
    this.router.navigate(['login']);
  }
}
