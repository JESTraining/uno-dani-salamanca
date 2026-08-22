import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateOrderRequest,
  OrderListQuery,
  OrderResponse,
  PagedResult,
} from '../models/order.models';

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.orderServiceUrl}/api/orders`;

  createOrder(request: CreateOrderRequest, idempotencyKey?: string): Observable<OrderResponse> {
    const headers = idempotencyKey ? new HttpHeaders({ 'Idempotency-Key': idempotencyKey }) : undefined;
    return this.http.post<OrderResponse>(this.baseUrl, request, { headers });
  }

  getOrder(id: string): Observable<OrderResponse> {
    return this.http.get<OrderResponse>(`${this.baseUrl}/${id}`);
  }

  listOrders(query: Partial<OrderListQuery>): Observable<PagedResult<OrderResponse>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
    if (query.status) params = params.set('status', query.status);
    if (query.dateFrom) params = params.set('dateFrom', query.dateFrom);
    if (query.dateTo) params = params.set('dateTo', query.dateTo);
    if (query.customerId) params = params.set('customerId', query.customerId);

    return this.http.get<PagedResult<OrderResponse>>(this.baseUrl, { params });
  }

  cancelOrder(id: string): Observable<OrderResponse> {
    return this.http.delete<OrderResponse>(`${this.baseUrl}/${id}`);
  }
}
