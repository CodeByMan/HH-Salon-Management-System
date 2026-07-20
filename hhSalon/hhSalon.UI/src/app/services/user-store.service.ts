import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AuthenticatedUser } from '../models/authenticated-user';

@Injectable({ providedIn: 'root' })
export class UserStoreService {
  private readonly storageKey = 'hhSalon.session';
  private readonly initial = this.readSession();
  private fullName$ = new BehaviorSubject<string>(this.initial?.fullName ?? '');
  private role$ = new BehaviorSubject<string>(this.initial?.role ?? '');
  private id$ = new BehaviorSubject<string>(this.initial?.id ?? '');
  private userName$ = new BehaviorSubject<string>(this.initial?.userName ?? '');

  getRoleFromStore() { return this.role$.asObservable(); }
  getFullNameFromStore() { return this.fullName$.asObservable(); }
  getIdFromStore() { return this.id$.asObservable(); }
  getUserNameFromStore() { return this.userName$.asObservable(); }

  setRoleForStore(role: string) { this.role$.next(role); this.persistCurrent(); }
  setFullNameForStore(fullName: string) { this.fullName$.next(fullName); this.persistCurrent(); }
  setIdForStore(id: string) { this.id$.next(id); this.persistCurrent(); }
  setUserNameForStore(userName: string) { this.userName$.next(userName); this.persistCurrent(); }

  setSession(user: AuthenticatedUser) {
    this.id$.next(user.id);
    this.userName$.next(user.userName);
    this.fullName$.next(user.fullName);
    this.role$.next(user.role);
    sessionStorage.setItem(this.storageKey, JSON.stringify(user));
  }

  clearSession() {
    this.id$.next('');
    this.userName$.next('');
    this.fullName$.next('');
    this.role$.next('');
    sessionStorage.removeItem(this.storageKey);
  }

  currentSession(): AuthenticatedUser | null {
    const id = this.id$.value;
    return id ? { id, userName: this.userName$.value, fullName: this.fullName$.value, role: this.role$.value } : null;
  }

  private persistCurrent() {
    const session = this.currentSession();
    if (session) sessionStorage.setItem(this.storageKey, JSON.stringify(session));
  }

  private readSession(): AuthenticatedUser | null {
    try {
      const value = sessionStorage.getItem(this.storageKey);
      return value ? JSON.parse(value) as AuthenticatedUser : null;
    } catch {
      sessionStorage.removeItem(this.storageKey);
      return null;
    }
  }
}
