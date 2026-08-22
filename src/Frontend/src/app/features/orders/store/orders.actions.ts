import { createActionGroup, emptyProps, props } from '@ngrx/store';
import {
  CreateOrderRequest,
  OrderCompletedNotification,
  OrderCreatedNotification,
  OrderListQuery,
  OrderResponse,
  OrderStatusChangedNotification,
  PagedResult,
} from '../../../core/models/order.models';

export const OrdersActions = createActionGroup({
  source: 'Orders',
  events: {
    'Load Orders': props<{ query: Partial<OrderListQuery> }>(),
    'Load Orders Success': props<{ result: PagedResult<OrderResponse> }>(),
    'Load Orders Failure': props<{ error: string }>(),

    'Load Order Detail': props<{ id: string }>(),
    'Load Order Detail Success': props<{ order: OrderResponse }>(),
    'Load Order Detail Failure': props<{ error: string }>(),

    'Create Order': props<{ request: CreateOrderRequest }>(),
    'Create Order Success': props<{ order: OrderResponse }>(),
    'Create Order Failure': props<{ error: string }>(),

    'Cancel Order': props<{ id: string }>(),
    'Cancel Order Success': props<{ order: OrderResponse }>(),
    'Cancel Order Failure': props<{ error: string }>(),

    'Set Filters': props<{ filters: Partial<OrdersFilters> }>(),
    'Set Page': props<{ page: number }>(),

    'Order Created Realtime': props<{ notification: OrderCreatedNotification }>(),
    'Order Status Changed Realtime': props<{ notification: OrderStatusChangedNotification }>(),
    'Order Completed Realtime': props<{ notification: OrderCompletedNotification }>(),

    'Clear Selected Order': emptyProps(),
  },
});

export interface OrdersFilters {
  status: OrderListQuery['status'];
  dateFrom: OrderListQuery['dateFrom'];
  dateTo: OrderListQuery['dateTo'];
  customerId: OrderListQuery['customerId'];
}
