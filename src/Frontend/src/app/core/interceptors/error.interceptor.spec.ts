import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { errorInterceptor } from './error.interceptor';
import { NotificationService } from '../services/notification.service';

describe('errorInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let showError: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    showError = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: { showError, showSuccess: vi.fn() } },
      ],
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('shows the problem+json title when the server returns a structured error', async () => {
    const promise = firstValueFrom(httpClient.get('/api/orders/123')).catch((error) => error);
    httpMock.expectOne('/api/orders/123').flush(
      { status: 409, title: 'Order in status Completed cannot be cancelled.' },
      { status: 409, statusText: 'Conflict' },
    );
    await promise;

    expect(showError).toHaveBeenCalledWith('Order in status Completed cannot be cancelled.');
  });

  it('shows a generic message for a bodyless network error', async () => {
    const promise = firstValueFrom(httpClient.get('/api/orders/123')).catch((error) => error);
    httpMock.expectOne('/api/orders/123').error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });
    await promise;

    expect(showError).toHaveBeenCalledWith('Could not reach the server. Check your connection and try again.');
  });

  it('shows a generic message for a bodyless 500', async () => {
    const promise = firstValueFrom(httpClient.get('/api/orders/123')).catch((error) => error);
    httpMock.expectOne('/api/orders/123').flush(null, { status: 500, statusText: 'Internal Server Error' });
    await promise;

    expect(showError).toHaveBeenCalledWith('Something went wrong (HTTP 500). Please try again.');
  });
});
