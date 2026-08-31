import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { Store } from '@ngrx/store';
import { catchError, of } from 'rxjs';
import { PaymentApiService } from '../../../../core/services/payment-api.service';
import { PaymentResponse } from '../../../../core/models/payment.models';
import { ORDER_SAGA_SEQUENCE } from '../../../../core/models/order.models';
import { OrderStatusBadge } from '../../../../shared/components/order-status-badge/order-status-badge';
import { LoadingSkeleton } from '../../../../shared/components/loading-skeleton/loading-skeleton';
import { ConfirmDialog } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { OrdersActions } from '../../store/orders.actions';
import { selectCanCancelSelectedOrder } from '../../store/orders.selectors';
import { selectLoadingDetail, selectSelectedOrder } from '../../store/orders.reducer';

const TERMINAL_FAILURE_STATUSES = new Set(['PaymentFailed', 'InventoryFailed', 'Cancelled']);

@Component({
  selector: 'app-order-detail',
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatListModule,
    OrderStatusBadge,
    LoadingSkeleton,
  ],
  templateUrl: './order-detail.html',
  styleUrl: './order-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly store = inject(Store);
  private readonly paymentApi = inject(PaymentApiService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly sagaSequence = ORDER_SAGA_SEQUENCE;
  protected readonly order = toSignal(this.store.select(selectSelectedOrder), { initialValue: null });
  protected readonly loading = toSignal(this.store.select(selectLoadingDetail), { initialValue: false });
  protected readonly canCancel = toSignal(this.store.select(selectCanCancelSelectedOrder), { initialValue: false });
  protected readonly payment = signal<PaymentResponse | null>(null);
  protected readonly paymentLoading = signal(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.store.dispatch(OrdersActions.loadOrderDetail({ id }));

    this.paymentApi
      .getPayment(id)
      .pipe(
        catchError(() => of(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((payment) => {
        this.payment.set(payment);
        this.paymentLoading.set(false);
      });
  }

  protected isFailure(status: string): boolean {
    return TERMINAL_FAILURE_STATUSES.has(status);
  }

  protected timelineStepState(step: string): 'done' | 'current' | 'upcoming' {
    const order = this.order();
    if (!order) return 'upcoming';
    if (this.isFailure(order.status)) {
      return step === 'Pending' ? 'done' : 'upcoming';
    }
    const currentIndex = this.sagaSequence.indexOf(order.status as (typeof this.sagaSequence)[number]);
    const stepIndex = this.sagaSequence.indexOf(step as (typeof this.sagaSequence)[number]);
    if (stepIndex < currentIndex) return 'done';
    if (stepIndex === currentIndex) return 'current';
    return 'upcoming';
  }

  protected cancelOrder(): void {
    const order = this.order();
    if (!order) return;

    const dialogRef = this.dialog.open(ConfirmDialog, {
      data: {
        title: 'Cancel order',
        message: `Cancel order ${order.id}? This cannot be undone.`,
        confirmLabel: 'Cancel order',
        cancelLabel: 'Keep order',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.store.dispatch(OrdersActions.cancelOrder({ id: order.id }));
      }
    });
  }
}
