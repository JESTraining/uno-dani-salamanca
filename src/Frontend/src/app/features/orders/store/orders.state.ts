import { OrderResponse } from '../../../core/models/order.models';
import { OrdersFilters } from './orders.actions';

export interface OrdersState {
  items: OrderResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  filters: OrdersFilters;
  selectedOrder: OrderResponse | null;
  loading: boolean;
  loadingDetail: boolean;
  creating: boolean;
  error: string | null;
}

export const initialOrdersState: OrdersState = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  filters: { status: null, dateFrom: null, dateTo: null, customerId: null },
  selectedOrder: null,
  loading: false,
  loadingDetail: false,
  creating: false,
  error: null,
};
