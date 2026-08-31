// Mirrors OrderService.Application/Contracts/OrderDtos.cs field-for-field.
// System.Text.Json's default camelCasing (plain AddControllers(), no
// custom naming policy) is what makes this a 1:1 match.

export type OrderStatus =
  | 'Pending'
  | 'PaymentProcessing'
  | 'PaymentFailed'
  | 'InventoryProcessing'
  | 'InventoryFailed'
  | 'Completed'
  | 'Cancelled'
  | 'Shipped'
  | 'Delivered';

export const ORDER_STATUS_VALUES: readonly OrderStatus[] = [
  'Pending',
  'PaymentProcessing',
  'PaymentFailed',
  'InventoryProcessing',
  'InventoryFailed',
  'Completed',
  'Cancelled',
  'Shipped',
  'Delivered',
];

/// The fixed happy-path sequence used to render the order detail status
/// timeline. The backend has no status-history table, only the order's
/// current Status/UpdatedAt - this is an honest rendering of that, not a
/// literal audit log of every transition the order actually went through.
export const ORDER_SAGA_SEQUENCE: readonly OrderStatus[] = [
  'Pending',
  'PaymentProcessing',
  'InventoryProcessing',
  'Completed',
];

export interface CreateOrderItemRequest {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderRequest {
  customerId: string;
  customerName: string;
  customerEmail: string;
  items: CreateOrderItemRequest[];
}

export interface OrderItemResponse {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
}

export interface OrderResponse {
  id: string;
  customerId: string;
  customerName: string;
  customerEmail: string;
  status: OrderStatus;
  totalAmount: number;
  createdAt: string;
  updatedAt: string;
  items: OrderItemResponse[];
}

export interface UpdateOrderStatusRequest {
  status: OrderStatus;
}

export interface OrderListQuery {
  page: number;
  pageSize: number;
  status?: OrderStatus | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  customerId?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// SignalR hub notification payloads - OrderService.Infrastructure/Messaging/OrderHubNotifications.cs

export interface OrderCreatedNotification {
  orderId: string;
  customerId: string;
  customerName: string;
  status: OrderStatus;
  totalAmount: number;
  createdAt: string;
}

export interface OrderStatusChangedNotification {
  orderId: string;
  previousStatus: OrderStatus;
  newStatus: OrderStatus;
  timestamp: string;
}

export interface OrderCompletedNotification {
  orderId: string;
  status: OrderStatus;
  timestamp: string;
}
