import { DatePipe, DecimalPipe, SlicePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Store } from '@ngrx/store';
import { ORDER_STATUS_VALUES, OrderStatus } from '../../../../core/models/order.models';
import { OrderStatusBadge } from '../../../../shared/components/order-status-badge/order-status-badge';
import { LoadingSkeleton } from '../../../../shared/components/loading-skeleton/loading-skeleton';
import { OrdersActions } from '../../store/orders.actions';
import {
  selectFilters,
  selectItems,
  selectLoading,
  selectPage,
  selectPageSize,
  selectTotalCount,
} from '../../store/orders.reducer';

@Component({
  selector: 'app-order-list',
  imports: [
    DatePipe,
    DecimalPipe,
    SlicePipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    OrderStatusBadge,
    LoadingSkeleton,
  ],
  templateUrl: './order-list.html',
  styleUrl: './order-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderList implements OnInit {
  private readonly store = inject(Store);

  protected readonly displayedColumns = ['id', 'customerName', 'status', 'totalAmount', 'createdAt'];
  protected readonly statusOptions: readonly OrderStatus[] = ORDER_STATUS_VALUES;

  protected readonly loading = toSignal(this.store.select(selectLoading), { initialValue: false });
  protected readonly totalCount = toSignal(this.store.select(selectTotalCount), { initialValue: 0 });
  protected readonly page = toSignal(this.store.select(selectPage), { initialValue: 1 });
  protected readonly pageSize = toSignal(this.store.select(selectPageSize), { initialValue: 20 });
  protected readonly filters = toSignal(this.store.select(selectFilters), {
    initialValue: { status: null, dateFrom: null, dateTo: null, customerId: null },
  });
  private readonly items = toSignal(this.store.select(selectItems), { initialValue: [] });

  /// No backend endpoint searches by order id fragment or customer name -
  /// OrderListQuery only supports an exact customerId. This search box is a
  /// client-side filter over the currently-loaded page, not a cross-page
  /// server search, and is documented as such rather than overclaiming.
  protected readonly searchTerm = signal('');

  protected readonly filteredItems = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return this.items();
    return this.items().filter(
      (order) => order.id.toLowerCase().includes(term) || order.customerName.toLowerCase().includes(term),
    );
  });

  ngOnInit(): void {
    this.store.dispatch(OrdersActions.loadOrders({ query: { page: this.page(), pageSize: this.pageSize() } }));
  }

  protected onStatusFilterChange(status: OrderStatus | null): void {
    this.store.dispatch(OrdersActions.setFilters({ filters: { status } }));
  }

  protected onDateFromChange(value: string): void {
    this.store.dispatch(OrdersActions.setFilters({ filters: { dateFrom: value || null } }));
  }

  protected onDateToChange(value: string): void {
    this.store.dispatch(OrdersActions.setFilters({ filters: { dateTo: value || null } }));
  }

  protected onPageEvent(event: PageEvent): void {
    this.store.dispatch(OrdersActions.setPage({ page: event.pageIndex + 1 }));
  }
}
