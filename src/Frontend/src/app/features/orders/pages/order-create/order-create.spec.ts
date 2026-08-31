import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { of } from 'rxjs';
import { OrderCreate } from './order-create';
import { InventoryApiService } from '../../../../core/services/inventory-api.service';
import { OrdersActions } from '../../store/orders.actions';
import { initialOrdersState } from '../../store/orders.state';

describe('OrderCreate', () => {
  let fixture: ComponentFixture<OrderCreate>;
  let store: MockStore;
  let inventoryApi: { listProducts: ReturnType<typeof vi.fn>; getProduct: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    inventoryApi = {
      listProducts: vi.fn().mockReturnValue(of({ items: [], page: 1, pageSize: 100, totalCount: 0 })),
      getProduct: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [OrderCreate],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideMockStore({ initialState: { orders: initialOrdersState } }),
        { provide: InventoryApiService, useValue: inventoryApi },
      ],
    }).compileComponents();

    store = TestBed.inject(MockStore);
    fixture = TestBed.createComponent(OrderCreate);
    fixture.detectChanges();
  });

  it('starts with an invalid form (required fields empty)', () => {
    expect(fixture.componentInstance['form'].valid).toBe(false);
  });

  it('does not dispatch createOrder when the form is invalid', () => {
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    fixture.componentInstance['submit']();
    expect(dispatchSpy).not.toHaveBeenCalledWith(expect.objectContaining({ type: OrdersActions.createOrder.type }));
  });

  it('adds and removes item rows', () => {
    const component = fixture.componentInstance;
    expect(component['items'].length).toBe(1);
    component['addItem']();
    expect(component['items'].length).toBe(2);
    component['removeItem'](1);
    expect(component['items'].length).toBe(1);
  });

  it('does not remove the last remaining item row', () => {
    const component = fixture.componentInstance;
    component['removeItem'](0);
    expect(component['items'].length).toBe(1);
  });
});
