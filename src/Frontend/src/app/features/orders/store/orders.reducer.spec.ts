import { describe, it, expect } from 'vitest';
import { OrdersActions } from './orders.actions';
import { ordersReducer } from './orders.reducer';
import { initialOrdersState } from './orders.state';
import { OrderResponse } from '../../../core/models/order.models';

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
