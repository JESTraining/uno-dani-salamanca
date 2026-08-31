import { createSelector } from '@ngrx/store';
import { selectSelectedOrder } from './orders.reducer';

/// Client-side mirror of the server's cancellation rule ("only Pending or
/// PaymentProcessing can be cancelled" - see Order.cs, Cancel()) - used only
/// to gate the Cancel button. The server remains the actual authority and
/// still enforces this itself (can still respond 409).
export const selectCanCancelSelectedOrder = createSelector(
  selectSelectedOrder,
  (order) => order != null && (order.status === 'Pending' || order.status === 'PaymentProcessing'),
);
