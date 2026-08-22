import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { OrderStatus } from '../../../core/models/order.models';

type BadgeTone = 'neutral' | 'progress' | 'success' | 'error';

const TONE_BY_STATUS: Record<OrderStatus, BadgeTone> = {
  Pending: 'neutral',
  PaymentProcessing: 'progress',
  InventoryProcessing: 'progress',
  Completed: 'success',
  PaymentFailed: 'error',
  InventoryFailed: 'error',
  Cancelled: 'error',
  Shipped: 'success',
  Delivered: 'success',
};

@Component({
  selector: 'app-order-status-badge',
  templateUrl: './order-status-badge.html',
  styleUrl: './order-status-badge.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderStatusBadge {
  readonly status = input.required<OrderStatus>();
  protected readonly tone = computed<BadgeTone>(() => TONE_BY_STATUS[this.status()]);
}
