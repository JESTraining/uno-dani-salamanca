import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { OrderList } from './order-list';
import { OrdersActions } from '../../store/orders.actions';
import { initialOrdersState } from '../../store/orders.state';
import { OrderResponse } from '../../../../core/models/order.models';

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

describe('OrderList', () => {
  let fixture: ComponentFixture<OrderList>;
  let store: MockStore;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideMockStore({
          initialState: {
            orders: {
              ...initialOrdersState,
              items: [order({ id: 'aaaaaaaa-0000-0000-0000-000000000000', customerName: 'Alice' }), order({ id: 'cccccccc-0000-0000-0000-000000000000', customerName: 'Bob' })],
              totalCount: 2,
            },
          },
        }),
      ],
    }).compileComponents();

    store = TestBed.inject(MockStore);
    fixture = TestBed.createComponent(OrderList);
  });

  it('dispatches loadOrders on init', () => {
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    fixture.detectChanges();
    expect(dispatchSpy).toHaveBeenCalledWith(
      OrdersActions.loadOrders({ query: { page: 1, pageSize: 20 } }),
    );
  });

  it('filters the visible rows by search term (customer name)', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(component['filteredItems']().length).toBe(2);

    component['searchTerm'].set('bob');
    expect(component['filteredItems']().length).toBe(1);
    expect(component['filteredItems']()[0].customerName).toBe('Bob');
  });

  it('filters the visible rows by a partial order id', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component['searchTerm'].set('cccccccc');
    expect(component['filteredItems']().length).toBe(1);
    expect(component['filteredItems']()[0].id).toBe('cccccccc-0000-0000-0000-000000000000');
  });

  it('dispatches setFilters when the status filter changes', () => {
    fixture.detectChanges();
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    fixture.componentInstance['onStatusFilterChange']('Completed');
    expect(dispatchSpy).toHaveBeenCalledWith(OrdersActions.setFilters({ filters: { status: 'Completed' } }));
  });

  it('dispatches setPage on paginator page change', () => {
    fixture.detectChanges();
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    fixture.componentInstance['onPageEvent']({ pageIndex: 2, pageSize: 20, length: 100 });
    expect(dispatchSpy).toHaveBeenCalledWith(OrdersActions.setPage({ page: 3 }));
  });
});
