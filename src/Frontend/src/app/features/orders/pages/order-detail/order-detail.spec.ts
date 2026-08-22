import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { of } from 'rxjs';
import { OrderDetail } from './order-detail';
import { PaymentApiService } from '../../../../core/services/payment-api.service';
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

describe('OrderDetail', () => {
  let fixture: ComponentFixture<OrderDetail>;
  let store: MockStore;

  async function setup(selectedOrder: OrderResponse | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [OrderDetail],
      providers: [
        provideNoopAnimations(),
        provideMockStore({
          initialState: { orders: { ...initialOrdersState, selectedOrder } },
        }),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'aaaaaaaa-0000-0000-0000-000000000000' }) } },
        },
        { provide: PaymentApiService, useValue: { getPayment: vi.fn().mockReturnValue(of(null)) } },
      ],
    }).compileComponents();

    store = TestBed.inject(MockStore);
    fixture = TestBed.createComponent(OrderDetail);
  }

  it('dispatches loadOrderDetail with the route id on init', async () => {
    await setup(null);
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    fixture.detectChanges();
    expect(dispatchSpy).toHaveBeenCalledWith(
      OrdersActions.loadOrderDetail({ id: 'aaaaaaaa-0000-0000-0000-000000000000' }),
    );
  });

  it('marks steps up to and including the current status as done/current', async () => {
    await setup(order({ status: 'InventoryProcessing' }));
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(component['timelineStepState']('Pending')).toBe('done');
    expect(component['timelineStepState']('PaymentProcessing')).toBe('done');
    expect(component['timelineStepState']('InventoryProcessing')).toBe('current');
    expect(component['timelineStepState']('Completed')).toBe('upcoming');
  });

  it('treats a failure status as stopping after Pending', async () => {
    await setup(order({ status: 'PaymentFailed' }));
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(component['isFailure']('PaymentFailed')).toBe(true);
    expect(component['timelineStepState']('Pending')).toBe('done');
    expect(component['timelineStepState']('PaymentProcessing')).toBe('upcoming');
  });
});
