import { describe, it, expect } from 'vitest';
import { OrdersActions } from './orders.actions';
import { ordersReducer } from './orders.reducer';
import { initialOrdersState } from './orders.state';
import { CreateOrderRequest, OrderResponse } from '../../../core/models/order.models';

function order(overrides: Partial<OrderResponse>): OrderResponse {
  return {
    id: 'aaaaaaaa-0000-0000-0000-000000000000',
    customerId: 'bbbbbbbb-0000-0000-0000-000000000000',
    customerName: 'Alice',
    customerEmail: 'alice@example.com',
    status: 'Pending',
    totalAmount: 10,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    items: [],
    ...overrides,
  };
}

describe('ordersReducer', () => {
  it('sets loading on loadOrders and populates items on success', () => {
    const loading = ordersReducer(initialOrdersState, OrdersActions.loadOrders({ query: { page: 1, pageSize: 20 } }));
    expect(loading.loading).toBe(true);

    const items = [order({})];
    const success = ordersReducer(
      loading,
      OrdersActions.loadOrdersSuccess({ result: { items, page: 1, pageSize: 20, totalCount: 1 } }),
    );
    expect(success.loading).toBe(false);
    expect(success.items).toEqual(items);
    expect(success.totalCount).toBe(1);
  });

  it('records the error and clears loading on loadOrdersFailure', () => {
    const state = ordersReducer(initialOrdersState, OrdersActions.loadOrdersFailure({ error: 'boom' }));
    expect(state.loading).toBe(false);
    expect(state.error).toBe('boom');
  });

  it('resets to page 1 when filters change', () => {
    const withPage = { ...initialOrdersState, page: 5 };
    const state = ordersReducer(withPage, OrdersActions.setFilters({ filters: { status: 'Completed' } }));
    expect(state.page).toBe(1);
    expect(state.filters.status).toBe('Completed');
  });

  it('sets the page number on setPage', () => {
    const state = ordersReducer(initialOrdersState, OrdersActions.setPage({ page: 3 }));
    expect(state.page).toBe(3);
  });

  it('sets loadingDetail on loadOrderDetail and populates selectedOrder on success', () => {
    const loading = ordersReducer(initialOrdersState, OrdersActions.loadOrderDetail({ id: 'order-1' }));
    expect(loading.loadingDetail).toBe(true);

    const detail = order({ id: 'order-1' });
    const success = ordersReducer(loading, OrdersActions.loadOrderDetailSuccess({ order: detail }));
    expect(success.loadingDetail).toBe(false);
    expect(success.selectedOrder).toEqual(detail);
  });

  it('records the error and clears loadingDetail on loadOrderDetailFailure', () => {
    const state = ordersReducer(initialOrdersState, OrdersActions.loadOrderDetailFailure({ error: 'not found' }));
    expect(state.loadingDetail).toBe(false);
    expect(state.error).toBe('not found');
  });

  it('sets creating on createOrder and populates selectedOrder on success', () => {
    const request: CreateOrderRequest = {
      customerId: 'bbbbbbbb-0000-0000-0000-000000000000',
      customerName: 'Alice',
      customerEmail: 'alice@example.com',
      items: [{ productId: 'cccccccc-0000-0000-0000-000000000000', productName: 'Widget', quantity: 1, unitPrice: 9.99 }],
    };
    const creating = ordersReducer(initialOrdersState, OrdersActions.createOrder({ request }));
    expect(creating.creating).toBe(true);

    const created = order({ id: 'order-new' });
    const success = ordersReducer(creating, OrdersActions.createOrderSuccess({ order: created }));
    expect(success.creating).toBe(false);
    expect(success.selectedOrder).toEqual(created);
  });

  it('records the error and clears creating on createOrderFailure', () => {
    const state = ordersReducer(initialOrdersState, OrdersActions.createOrderFailure({ error: 'validation failed' }));
    expect(state.creating).toBe(false);
    expect(state.error).toBe('validation failed');
  });

  it('records the error on cancelOrderFailure', () => {
    const state = ordersReducer(initialOrdersState, OrdersActions.cancelOrderFailure({ error: 'cannot cancel' }));
    expect(state.error).toBe('cannot cancel');
  });

  it('clears selectedOrder on clearSelectedOrder', () => {
    const seeded = { ...initialOrdersState, selectedOrder: order({ id: 'order-1' }) };
    const state = ordersReducer(seeded, OrdersActions.clearSelectedOrder());
    expect(state.selectedOrder).toBeNull();
  });

  describe('realtime status patching', () => {
    const seeded = {
      ...initialOrdersState,
      items: [order({ id: 'order-1', status: 'PaymentProcessing' }), order({ id: 'order-2', status: 'Pending' })],
      selectedOrder: order({ id: 'order-1', status: 'PaymentProcessing' }),
    };

    it('patches only the matching item on orderStatusChangedRealtime', () => {
      const state = ordersReducer(
        seeded,
        OrdersActions.orderStatusChangedRealtime({
          notification: {
            orderId: 'order-1',
            previousStatus: 'PaymentProcessing',
            newStatus: 'InventoryProcessing',
            timestamp: '2026-01-01T00:00:01Z',
          },
        }),
      );

      expect(state.items.find((i) => i.id === 'order-1')?.status).toBe('InventoryProcessing');
      expect(state.items.find((i) => i.id === 'order-2')?.status).toBe('Pending');
      expect(state.selectedOrder?.status).toBe('InventoryProcessing');
    });

    it('does not touch selectedOrder when the notification is for a different order', () => {
      const state = ordersReducer(
        seeded,
        OrdersActions.orderStatusChangedRealtime({
          notification: {
            orderId: 'order-2',
            previousStatus: 'Pending',
            newStatus: 'PaymentProcessing',
            timestamp: '2026-01-01T00:00:01Z',
          },
        }),
      );

      expect(state.selectedOrder?.status).toBe('PaymentProcessing');
      expect(state.items.find((i) => i.id === 'order-2')?.status).toBe('PaymentProcessing');
    });

    it('patches the matching item on orderCompletedRealtime', () => {
      const state = ordersReducer(
        seeded,
        OrdersActions.orderCompletedRealtime({
          notification: { orderId: 'order-1', status: 'Completed', timestamp: '2026-01-01T00:00:02Z' },
        }),
      );

      expect(state.items.find((i) => i.id === 'order-1')?.status).toBe('Completed');
    });

    it('does not splice a new order into items on orderCreatedRealtime (pagination safety)', () => {
      const state = ordersReducer(
        seeded,
        OrdersActions.orderCreatedRealtime({
          notification: {
            orderId: 'order-3',
            customerId: 'customer-3',
            customerName: 'Carol',
            status: 'PaymentProcessing',
            totalAmount: 5,
            createdAt: '2026-01-01T00:00:03Z',
          },
        }),
      );

      expect(state.items).toHaveLength(2);
      expect(state.items.some((i) => i.id === 'order-3')).toBe(false);
    });
  });

  it('replaces the matching item and selectedOrder on cancelOrderSuccess', () => {
    const seeded = {
      ...initialOrdersState,
      items: [order({ id: 'order-1', status: 'PaymentProcessing' })],
      selectedOrder: order({ id: 'order-1', status: 'PaymentProcessing' }),
    };
    const cancelled = order({ id: 'order-1', status: 'Cancelled' });

    const state = ordersReducer(seeded, OrdersActions.cancelOrderSuccess({ order: cancelled }));

    expect(state.items[0].status).toBe('Cancelled');
    expect(state.selectedOrder?.status).toBe('Cancelled');
  });
});
