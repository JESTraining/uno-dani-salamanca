import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { TokenStorageService } from '../services/token-storage.service';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let getToken: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getToken = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: TokenStorageService, useValue: { getToken } },
      ],
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('attaches an Authorization header when a token is present', () => {
    getToken.mockReturnValue('fake-jwt');
    httpClient.get('/api/v1/products').subscribe();

    const request = httpMock.expectOne('/api/v1/products');
    expect(request.request.headers.get('Authorization')).toBe('Bearer fake-jwt');
  });

  it('does not attach an Authorization header when no token is present', () => {
    getToken.mockReturnValue(null);
    httpClient.get('/api/v1/products').subscribe();

    const request = httpMock.expectOne('/api/v1/products');
    expect(request.request.headers.has('Authorization')).toBe(false);
  });
});
