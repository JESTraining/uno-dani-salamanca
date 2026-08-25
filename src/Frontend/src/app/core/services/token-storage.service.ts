import { Injectable } from '@angular/core';

const STORAGE_KEY = 'auth_token';

/// Minimal token storage. The frontend has no login screen by deliberate
/// scope decision - the only endpoint the backend actually gates is product
/// creation, and there is no product-management UI either (both are the
/// admin-dashboard bonus, out of scope). This is a hook for a possible
/// future admin UI, not a claim that authentication is wired end-to-end.
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
