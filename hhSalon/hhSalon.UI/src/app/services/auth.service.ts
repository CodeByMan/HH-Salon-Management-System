import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { finalize, tap } from 'rxjs';
import { environment } from 'src/environments/environment';
import { AuthenticatedUser } from '../models/authenticated-user';
import { UserStoreService } from './user-store.service';

interface ApiMessageResponse { message: string; }
interface WorkerRegistrationResponse extends ApiMessageResponse { workerId: string; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly url = 'Auth';
  role = '';
  id = '';

  constructor(private http: HttpClient, private router: Router, private userStore: UserStoreService) {
    this.userStore.getRoleFromStore().subscribe(value => this.role = value);
    this.userStore.getIdFromStore().subscribe(value => this.id = value);
  }

  signUp(userObj: unknown) { return this.http.post<ApiMessageResponse>(`${environment.apiUrl}/${this.url}/register`, userObj); }
  createAdmin(userObj: unknown) { return this.http.post<ApiMessageResponse>(`${environment.apiUrl}/${this.url}/admin-register`, userObj); }
  signUpWorker(worker: unknown) { return this.http.post<WorkerRegistrationResponse>(`${environment.apiUrl}/${this.url}/worker-register`, worker); }

  login(loginObj: unknown) {
    return this.http.post<AuthenticatedUser>(`${environment.apiUrl}/${this.url}/authenticate`, loginObj)
      .pipe(tap(user => this.applySession(user)));
  }

  getCurrentUser() {
    return this.http.get<AuthenticatedUser>(`${environment.apiUrl}/${this.url}/me`)
      .pipe(tap(user => this.applySession(user)));
  }

  renewSession() {
    return this.http.post<AuthenticatedUser>(`${environment.apiUrl}/${this.url}/refresh`, {})
      .pipe(tap(user => this.applySession(user)));
  }

  signOut() {
    return this.http.post(`${environment.apiUrl}/${this.url}/logout`, {}).pipe(
      finalize(() => {
        this.clearSession();
        this.router.navigate(['login']);
      })
    );
  }

  applySession(user: AuthenticatedUser) { this.userStore.setSession(user); }
  clearSession() { this.userStore.clearSession(); }
  isLoggedIn() { return this.userStore.currentSession() !== null; }
  decodedToken() { return this.userStore.currentSession(); }
  getFullNameFromToken() { return this.userStore.currentSession()?.fullName ?? ''; }
  getRoleFromToken() { return this.userStore.currentSession()?.role ?? ''; }
  getIdFromToken() { return this.userStore.currentSession()?.id ?? ''; }
}
