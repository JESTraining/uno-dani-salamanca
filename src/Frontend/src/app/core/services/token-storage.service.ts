import { Injectable } from '@angular/core';

const STORAGE_KEY = 'auth_token';

/// Minimal token storage - no login screen exists yet in the frontend (the
/// only endpoint the backend actually gates is product creation, and there
/// is no product-management UI either, both explicit bonus-dashboard scope
/// deferred since Phase 4). This exists as a hook for a future admin login,
/// not as a claim that authentication is fully wired end-to-end today.
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  getToken(): string | null {
    return sessionStorage.getItem(STORAGE_KEY);
  }

  setToken(token: string): void {
    sessionStorage.setItem(STORAGE_KEY, token);
  }

  clearToken(): void {
    sessionStorage.removeItem(STORAGE_KEY);
  }
}
