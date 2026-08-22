import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { OrderSignalrService } from './order-signalr.service';
import { OrdersActions } from '../../features/orders/store/orders.actions';
import { initialOrdersState } from '../../features/orders/store/orders.state';

class FakeHubConnection {
  handlers = new Map<string, (payload: unknown) => void>();
  reconnectedHandler: (() => void | Promise<void>) | null = null;

  on(event: string, handler: (payload: unknown) => void): void {
    this.handlers.set(event, handler);
  }

  onreconnected(handler: () => void | Promise<void>): void {
    this.reconnectedHandler = handler;
  }

  start(): Promise<void> {
    return Promise.resolve();
  }

  trigger(event: string, payload: unknown): void {
    this.handlers.get(event)?.(payload);
  }
}

class TestableOrderSignalrService extends OrderSignalrService {
  readonly fakeConnection = new FakeHubConnection();

  protected override createConnection() {
    return this.fakeConnection as never;
  }
}

describe('OrderSignalrService', () => {
  let service: TestableOrderSignalrService;
  let store: MockStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TestableOrderSignalrService,
        provideMockStore({ initialState: { orders: initialOrdersState } }),
      ],
    });
    service = TestBed.inject(TestableOrderSignalrService);
    store = TestBed.inject(MockStore);
  });

  it('dispatches orderCreatedRealtime when the hub raises OrderCreated', () => {
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    service.connect();

    const notification = {
      orderId: 'order-1',
      customerId: 'c1',
      customerName: 'Alice',
      status: 'PaymentProcessing' as const,
      totalAmount: 10,
      createdAt: '2026-01-01T00:00:00Z',
    };
    service.fakeConnection.trigger('OrderCreated', notification);

    expect(dispatchSpy).toHaveBeenCalledWith(OrdersActions.orderCreatedRealtime({ notification }));
  });

  it('dispatches orderStatusChangedRealtime when the hub raises OrderStatusChanged', () => {
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    service.connect();

    const notification = {
      orderId: 'order-1',
      previousStatus: 'Pending' as const,
      newStatus: 'PaymentProcessing' as const,
      timestamp: '2026-01-01T00:00:00Z',
    };
    service.fakeConnection.trigger('OrderStatusChanged', notification);

    expect(dispatchSpy).toHaveBeenCalledWith(OrdersActions.orderStatusChangedRealtime({ notification }));
  });

  it('dispatches orderCompletedRealtime when the hub raises OrderCompleted', () => {
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    service.connect();

    const notification = { orderId: 'order-1', status: 'Completed' as const, timestamp: '2026-01-01T00:00:00Z' };
    service.fakeConnection.trigger('OrderCompleted', notification);

    expect(dispatchSpy).toHaveBeenCalledWith(OrdersActions.orderCompletedRealtime({ notification }));
  });

  it('does not build a second connection on a repeated connect() call', () => {
    const createConnectionSpy = vi.spyOn(service as never, 'createConnection');
    service.connect();
    service.connect();
    expect(createConnectionSpy).toHaveBeenCalledOnce();
  });
});
