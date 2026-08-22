import { describe, it, expect } from 'vitest';
import { selectCanCancelSelectedOrder } from './orders.selectors';
import { initialOrdersState } from './orders.state';
import { OrderResponse, OrderStatus, ORDER_STATUS_VALUES } from '../../../core/models/order.models';

function order(status: OrderStatus): OrderResponse {
  return {
    id: 'aaaaaaaa-0000-0000-0000-000000000000',
    customerId: 'bbbbbbbb-0000-0000-0000-000000000000',
    customerName: 'Alice',
    customerEmail: 'alice@example.com',
    status,
    totalAmount: 10,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    items: [],
  };
}

describe('selectCanCancelSelectedOrder', () => {
  it('is false when no order is selected', () => {
    const result = selectCanCancelSelectedOrder.projector(null);
    expect(result).toBe(false);
  });

  // Mirrors Order.cs Cancel(): only Pending or PaymentProcessing may be cancelled.
  const expectations: Record<OrderStatus, boolean> = {
    Pending: true,
    PaymentProcessing: true,
    PaymentFailed: false,
    InventoryProcessing: false,
    InventoryFailed: false,
    Completed: false,
    Cancelled: false,
    Shipped: false,
    Delivered: false,
  };

  for (const status of ORDER_STATUS_VALUES) {
    it(`is ${expectations[status]} for status ${status}`, () => {
      const result = selectCanCancelSelectedOrder.projector(order(status));
      expect(result).toBe(expectations[status]);
    });
  }
});
