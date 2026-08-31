import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { Observable, firstValueFrom, of, throwError, toArray, take } from 'rxjs';
import { OrdersEffects } from './orders.effects';
import { OrdersActions } from './orders.actions';
import { initialOrdersState } from './orders.state';
import { OrderApiService } from '../../../core/services/order-api.service';
import { InventoryApiService } from '../../../core/services/inventory-api.service';
import { OrderResponse } from '../../../core/models/order.models';

function order(): OrderResponse {
  return {
    id: 'aaaaaaaa-0000-0000-0000-000000000000',
    customerId: 'bbbbbbbb-0000-0000-0000-000000000000',
    customerName: 'Alice',
    customerEmail: 'alice@example.com',
    status: 'PaymentProcessing',
    totalAmount: 10,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    items: [],
  };
}

describe('OrdersEffects', () => {
  let actions$: Observable<unknown>;
  let orderApi: {
    listOrders: ReturnType<typeof vi.fn>;
    getOrder: ReturnType<typeof vi.fn>;
    createOrder: ReturnType<typeof vi.fn>;
    cancelOrder: ReturnType<typeof vi.fn>;
  };

  function setup(state = initialOrdersState) {
    TestBed.configureTestingModule({
      providers: [
        OrdersEffects,
        provideMockActions(() => actions$),
        provideMockStore({ initialState: { orders: state } }),
        { provide: OrderApiService, useValue: orderApi },
        { provide: InventoryApiService, useValue: { listProducts: vi.fn(), getProduct: vi.fn() } },
      ],
    });
    return TestBed.inject(OrdersEffects);
  }

  beforeEach(() => {
    orderApi = {
      listOrders: vi.fn(),
      getOrder: vi.fn(),
      createOrder: vi.fn(),
      cancelOrder: vi.fn(),
    };
  });

  it('loadOrders$ maps a successful API call to loadOrdersSuccess', async () => {
    const result = { items: [order()], page: 1, pageSize: 20, totalCount: 1 };
    orderApi.listOrders.mockReturnValue(of(result));
    actions$ = of(OrdersActions.loadOrders({ query: { page: 1, pageSize: 20 } }));

    const effects = setup();
    const emitted = await firstValueFrom(effects.loadOrders$);

    expect(emitted).toEqual(OrdersActions.loadOrdersSuccess({ result }));
  });

  it('loadOrders$ maps a failed API call to loadOrdersFailure', async () => {
    orderApi.listOrders.mockReturnValue(throwError(() => ({ error: { title: 'Server exploded' } })));
    actions$ = of(OrdersActions.loadOrders({ query: { page: 1, pageSize: 20 } }));

    const effects = setup();
    const emitted = await firstValueFrom(effects.loadOrders$);

    expect(emitted).toEqual(OrdersActions.loadOrdersFailure({ error: 'Server exploded' }));
  });

  it('createOrder$ maps a successful API call to createOrderSuccess', async () => {
    const created = order();
    orderApi.createOrder.mockReturnValue(of(created));
    actions$ = of(
      OrdersActions.createOrder({
        request: { customerId: 'c1', customerName: 'Alice', customerEmail: 'a@example.com', items: [] },
      }),
    );

    const effects = setup();
    const emitted = await firstValueFrom(effects.createOrder$);

    expect(emitted).toEqual(OrdersActions.createOrderSuccess({ order: created }));
  });

  it('cancelOrder$ maps a successful API call to cancelOrderSuccess', async () => {
    const cancelled = { ...order(), status: 'Cancelled' as const };
    orderApi.cancelOrder.mockReturnValue(of(cancelled));
    actions$ = of(OrdersActions.cancelOrder({ id: cancelled.id }));

    const effects = setup();
    const emitted = await firstValueFrom(effects.cancelOrder$);

    expect(emitted).toEqual(OrdersActions.cancelOrderSuccess({ order: cancelled }));
  });

  it('refetchOnOrderCreated$ dispatches loadOrders when on page 1', async () => {
    orderApi.listOrders.mockReturnValue(of({ items: [], page: 1, pageSize: 20, totalCount: 0 }));
    actions$ = of(
      OrdersActions.orderCreatedRealtime({
        notification: {
          orderId: 'order-1',
          customerId: 'c1',
          customerName: 'Alice',
          status: 'PaymentProcessing',
          totalAmount: 10,
          createdAt: '2026-01-01T00:00:00Z',
        },
      }),
    );

    const effects = setup({ ...initialOrdersState, page: 1 });
    const emitted = await firstValueFrom(effects.refetchOnOrderCreated$);

    expect(emitted).toEqual(OrdersActions.loadOrders({ query: { page: 1, pageSize: 20, status: null, dateFrom: null, dateTo: null, customerId: null } }));
  });

  it('refetchOnOrderCreated$ emits nothing when not on page 1', async () => {
    actions$ = of(
      OrdersActions.orderCreatedRealtime({
        notification: {
          orderId: 'order-1',
          customerId: 'c1',
          customerName: 'Alice',
          status: 'PaymentProcessing',
          totalAmount: 10,
          createdAt: '2026-01-01T00:00:00Z',
        },
      }),
    );

    const effects = setup({ ...initialOrdersState, page: 2 });
    const emissions = await firstValueFrom(effects.refetchOnOrderCreated$.pipe(take(1), toArray()));

    expect(emissions).toEqual([]);
  });
});
