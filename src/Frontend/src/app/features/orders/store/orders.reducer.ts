import { createFeature, createReducer, on } from '@ngrx/store';
import { OrdersActions } from './orders.actions';
import { initialOrdersState } from './orders.state';

export const ordersFeature = createFeature({
  name: 'orders',
  reducer: createReducer(
    initialOrdersState,

    on(OrdersActions.loadOrders, (state) => ({ ...state, loading: true, error: null })),
    on(OrdersActions.loadOrdersSuccess, (state, { result }) => ({
      ...state,
      loading: false,
      items: result.items,
      totalCount: result.totalCount,
      page: result.page,
      pageSize: result.pageSize,
    })),
    on(OrdersActions.loadOrdersFailure, (state, { error }) => ({ ...state, loading: false, error })),

    on(OrdersActions.loadOrderDetail, (state) => ({ ...state, loadingDetail: true, error: null })),
    on(OrdersActions.loadOrderDetailSuccess, (state, { order }) => ({
      ...state,
      loadingDetail: false,
      selectedOrder: order,
    })),
    on(OrdersActions.loadOrderDetailFailure, (state, { error }) => ({ ...state, loadingDetail: false, error })),

    on(OrdersActions.createOrder, (state) => ({ ...state, creating: true, error: null })),
    on(OrdersActions.createOrderSuccess, (state, { order }) => ({
      ...state,
      creating: false,
      selectedOrder: order,
    })),
    on(OrdersActions.createOrderFailure, (state, { error }) => ({ ...state, creating: false, error })),

    on(OrdersActions.cancelOrderSuccess, (state, { order }) => ({
      ...state,
      items: state.items.map((item) => (item.id === order.id ? order : item)),
      selectedOrder: state.selectedOrder?.id === order.id ? order : state.selectedOrder,
    })),
    on(OrdersActions.cancelOrderFailure, (state, { error }) => ({ ...state, error })),

    on(OrdersActions.setFilters, (state, { filters }) => ({
      ...state,
      filters: { ...state.filters, ...filters },
      page: 1,
    })),
    on(OrdersActions.setPage, (state, { page }) => ({ ...state, page })),

    // Realtime status patches: update the matching item/selectedOrder in
    // place, no refetch needed - the notification already carries the new
    // status. orderCreatedRealtime deliberately does NOT splice a new item
    // into `items` (unsafe against server-side pagination/filters); an
    // effect re-dispatches loadOrders instead, only when page === 1.
    on(OrdersActions.orderStatusChangedRealtime, (state, { notification }) => ({
      ...state,
      items: state.items.map((item) =>
        item.id === notification.orderId ? { ...item, status: notification.newStatus } : item,
      ),
      selectedOrder:
        state.selectedOrder?.id === notification.orderId
          ? { ...state.selectedOrder, status: notification.newStatus }
          : state.selectedOrder,
    })),
    on(OrdersActions.orderCompletedRealtime, (state, { notification }) => ({
      ...state,
      items: state.items.map((item) =>
        item.id === notification.orderId ? { ...item, status: notification.status } : item,
      ),
      selectedOrder:
        state.selectedOrder?.id === notification.orderId
          ? { ...state.selectedOrder, status: notification.status }
          : state.selectedOrder,
    })),

    on(OrdersActions.clearSelectedOrder, (state) => ({ ...state, selectedOrder: null })),
  ),
});

export const {
  name: ordersFeatureKey,
  reducer: ordersReducer,
  selectOrdersState,
  selectItems,
  selectTotalCount,
  selectPage,
  selectPageSize,
  selectFilters,
  selectSelectedOrder,
  selectLoading,
  selectLoadingDetail,
  selectCreating,
  selectError,
} = ordersFeature;
