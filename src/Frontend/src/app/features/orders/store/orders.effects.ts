import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { InventoryApiService } from '../../../core/services/inventory-api.service';
import { OrderApiService } from '../../../core/services/order-api.service';
import { OrdersActions } from './orders.actions';
import { selectFilters, selectPage, selectPageSize } from './orders.reducer';

@Injectable()
export class OrdersEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store);
  private readonly orderApi = inject(OrderApiService);
  // Kept as a dependency for future stock-check use (async form validator) -
  // not used by an effect yet, listed here so it's clear the service exists.
  private readonly inventoryApi = inject(InventoryApiService);

  loadOrders$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrdersActions.loadOrders),
      switchMap(({ query }) =>
        this.orderApi.listOrders(query).pipe(
          map((result) => OrdersActions.loadOrdersSuccess({ result })),
          catchError((error: unknown) => of(OrdersActions.loadOrdersFailure({ error: describeError(error) }))),
        ),
      ),
    ),
  );

  loadOrderDetail$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrdersActions.loadOrderDetail),
      switchMap(({ id }) =>
        this.orderApi.getOrder(id).pipe(
          map((order) => OrdersActions.loadOrderDetailSuccess({ order })),
          catchError((error: unknown) => of(OrdersActions.loadOrderDetailFailure({ error: describeError(error) }))),
        ),
      ),
    ),
  );

  createOrder$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrdersActions.createOrder),
      switchMap(({ request }) =>
        this.orderApi.createOrder(request).pipe(
          map((order) => OrdersActions.createOrderSuccess({ order })),
          catchError((error: unknown) => of(OrdersActions.createOrderFailure({ error: describeError(error) }))),
        ),
      ),
    ),
  );

  cancelOrder$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrdersActions.cancelOrder),
      switchMap(({ id }) =>
        this.orderApi.cancelOrder(id).pipe(
          map((order) => OrdersActions.cancelOrderSuccess({ order })),
          catchError((error: unknown) => of(OrdersActions.cancelOrderFailure({ error: describeError(error) }))),
        ),
      ),
    ),
  );

  setFilters$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrdersActions.setFilters),
      withLatestFrom(this.store.select(selectPage), this.store.select(selectPageSize)),
      map(([, page, pageSize]) => OrdersActions.loadOrders({ query: { page, pageSize } })),
    ),
  );

  setPage$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrdersActions.setPage),
      withLatestFrom(this.store.select(selectFilters), this.store.select(selectPageSize)),
      map(([{ page }, filters, pageSize]) => OrdersActions.loadOrders({ query: { page, pageSize, ...filters } })),
    ),
  );

  /// A newly-created order always sorts to the top under default ordering,
  /// so refetching only matters when the user is looking at page 1 - any
  /// other page is unaffected by a new order appearing.
  refetchOnOrderCreated$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrdersActions.orderCreatedRealtime),
      withLatestFrom(this.store.select(selectPage), this.store.select(selectPageSize), this.store.select(selectFilters)),
      switchMap(([, page, pageSize, filters]) =>
        page === 1 ? of(OrdersActions.loadOrders({ query: { page, pageSize, ...filters } })) : of(),
      ),
    ),
  );
}

function describeError(error: unknown): string {
  if (error && typeof error === 'object' && 'error' in error) {
    const body = (error as { error?: { title?: string } }).error;
    if (body?.title) return body.title;
  }
  return 'Something went wrong. Please try again.';
}
