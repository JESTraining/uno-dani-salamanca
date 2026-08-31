import { Injectable, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import {
  OrderCompletedNotification,
  OrderCreatedNotification,
  OrderStatusChangedNotification,
} from '../models/order.models';
import { OrdersActions } from '../../features/orders/store/orders.actions';
import { selectFilters, selectPage, selectPageSize } from '../../features/orders/store/orders.reducer';
import { firstValueFrom } from 'rxjs';

/// Builds and owns the single SignalR connection to OrderHub, dispatching
/// NgRx actions as events arrive. Connection construction is factored into
/// its own method so tests can substitute a fake HubConnection.
@Injectable({ providedIn: 'root' })
export class OrderSignalrService {
  private readonly store = inject(Store);
  private hubConnection: signalR.HubConnection | null = null;

  connect(): void {
    if (this.hubConnection) return;

    this.hubConnection = this.createConnection();
    this.registerHandlers(this.hubConnection);

    this.hubConnection.start().catch((err) => console.error('SignalR connection failed', err));
  }

  protected createConnection(): signalR.HubConnection {
    return new signalR.HubConnectionBuilder()
      .withUrl(environment.orderHubUrl, { withCredentials: false })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();
  }

  private registerHandlers(connection: signalR.HubConnection): void {
    connection.on('OrderCreated', (notification: OrderCreatedNotification) =>
      this.store.dispatch(OrdersActions.orderCreatedRealtime({ notification })),
    );
    connection.on('OrderStatusChanged', (notification: OrderStatusChangedNotification) =>
      this.store.dispatch(OrdersActions.orderStatusChangedRealtime({ notification })),
    );
    connection.on('OrderCompleted', (notification: OrderCompletedNotification) =>
      this.store.dispatch(OrdersActions.orderCompletedRealtime({ notification })),
    );

    // Events missed while disconnected are not replayed (no groups, no
    // buffering) - reconcile by refetching the currently-viewed page/filters.
    connection.onreconnected(async () => {
      const [page, pageSize, filters] = await Promise.all([
        firstValueFrom(this.store.select(selectPage)),
        firstValueFrom(this.store.select(selectPageSize)),
        firstValueFrom(this.store.select(selectFilters)),
      ]);
      this.store.dispatch(OrdersActions.loadOrders({ query: { page, pageSize, ...filters } }));
    });
  }
}
