import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from 'src/environments/environment';
import { AuthenticatedUser } from '../models/authenticated-user';
import { AuthService } from './auth.service';
import { UserStoreService } from './user-store.service';

class FakeHttpClient {
  calls: Array<{ method: string; url: string; body?: unknown }> = [];
  response: unknown;

  post<T>(url: string, body: unknown) {
    this.calls.push({ method: 'POST', url, body });
    return of(this.response as T);
  }

  get<T>(url: string) {
    this.calls.push({ method: 'GET', url });
    return of(this.response as T);
  }
}

describe('AuthService', () => {
  let http: FakeHttpClient;
  let service: AuthService;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    http = new FakeHttpClient();
    const router = { navigate: () => Promise.resolve(true) } as unknown as Router;
    service = new AuthService(http as unknown as HttpClient, router, new UserStoreService());
  });

  it('stores only non-sensitive session metadata after login', () => {
    http.response = { id: 'client-id', userName: 'client', fullName: 'Client User', role: 'Client' } satisfies AuthenticatedUser;

    service.login({ userName: 'client', password: 'Strong1!' }).subscribe();

    expect(http.calls[0]).toEqual({
      method: 'POST',
      url: `${environment.apiUrl}/Auth/authenticate`,
      body: { userName: 'client', password: 'Strong1!' }
    });
    expect(service.isLoggedIn()).toBe(true);
    expect(localStorage.getItem('token')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(sessionStorage.getItem('hhSalon.session')).not.toContain('accessToken');
    expect(sessionStorage.getItem('hhSalon.session')).not.toContain('refreshToken');
  });

  it('refreshes without sending a browser-readable token', () => {
    http.response = { id: 'client-id', userName: 'client', fullName: 'Client User', role: 'Client' } satisfies AuthenticatedUser;

    service.renewSession().subscribe();

    expect(http.calls[0]).toEqual({
      method: 'POST',
      url: `${environment.apiUrl}/Auth/refresh`,
      body: {}
    });
  });
});
