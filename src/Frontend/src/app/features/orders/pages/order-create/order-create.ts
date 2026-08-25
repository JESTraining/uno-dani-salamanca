import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  AsyncValidatorFn,
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Store } from '@ngrx/store';
import { of, timer } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { InventoryApiService } from '../../../../core/services/inventory-api.service';
import { ProductResponse } from '../../../../core/models/product.models';
import { OrdersActions } from '../../store/orders.actions';
import { selectCreating, selectError, selectSelectedOrder } from '../../store/orders.reducer';

interface OrderItemFormGroup {
  productId: FormControl<string>;
  quantity: FormControl<number>;
}

@Component({
  selector: 'app-order-create',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  templateUrl: './order-create.html',
  styleUrl: './order-create.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderCreate implements OnInit {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly inventoryApi = inject(InventoryApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly products = signal<ProductResponse[]>([]);
  protected readonly creating = toSignal(this.store.select(selectCreating), { initialValue: false });
  protected readonly error = toSignal(this.store.select(selectError), { initialValue: null });
  private readonly submitted = signal(false);

  protected readonly form = new FormGroup({
    customerName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    customerEmail: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(200)],
    }),
    items: new FormArray([this.createItemGroup()], { validators: [Validators.required, Validators.minLength(1)] }),
  });

  ngOnInit(): void {
    this.store.dispatch(OrdersActions.clearSelectedOrder());

    this.inventoryApi
      .listProducts(1, 100)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => this.products.set(result.items));

    this.store
      .select(selectSelectedOrder)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((order) => {
        if (order && this.submitted() && !this.creating()) {
          this.router.navigate(['/orders', order.id]);
        }
      });
  }

  protected get items(): FormArray<FormGroup<OrderItemFormGroup>> {
    return this.form.controls.items as FormArray<FormGroup<OrderItemFormGroup>>;
  }

  protected addItem(): void {
    this.items.push(this.createItemGroup());
  }

  protected removeItem(index: number): void {
    if (this.items.length > 1) {
      this.items.removeAt(index);
    }
  }

  protected productFor(index: number): ProductResponse | undefined {
    const productId = this.items.at(index).controls.productId.value;
    return this.products().find((p) => p.id === productId);
  }

  protected subtotalFor(index: number): number {
    const product = this.productFor(index);
    const quantity = this.items.at(index).controls.quantity.value;
    return product ? product.unitPrice * quantity : 0;
  }

  protected get total(): number {
    return this.items.controls.reduce((sum, _, index) => sum + this.subtotalFor(index), 0);
  }

  protected submit(): void {
    // Angular's status aggregation checks PENDING before INVALID, so a
    // FormGroup can be non-invalid while an async validator (the stock
    // check below) is still resolving - form.invalid alone would let a
    // submit through mid-validation. Block on both.
    if (this.form.invalid || this.form.pending) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitted.set(true);
    const value = this.form.getRawValue();
    this.store.dispatch(
      OrdersActions.createOrder({
        request: {
          // The frontend has no login screen (deliberate scope decision - see
          // CLAUDE.md), so there is no authenticated user id to attach here.
          customerId: crypto.randomUUID(),
          customerName: value.customerName,
          customerEmail: value.customerEmail,
          items: value.items.map((item) => {
            const product = this.products().find((p) => p.id === item.productId)!;
            return {
              productId: item.productId,
              productName: product.name,
              quantity: item.quantity,
              unitPrice: product.unitPrice,
            };
          }),
        },
      }),
    );
  }

  private createItemGroup(): FormGroup<OrderItemFormGroup> {
    return new FormGroup<OrderItemFormGroup>({
      productId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      quantity: new FormControl(1, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(1)],
        asyncValidators: [this.stockAvailabilityValidator()],
      }),
    });
  }

  /// Debounced live stock check against Inventory Service - re-fetches the
  /// specific product rather than trusting the list loaded on page init,
  /// since stock can change between page load and submission.
  private stockAvailabilityValidator(): AsyncValidatorFn {
    return (control: AbstractControl) =>
      timer(300).pipe(
        switchMap(() => {
          const group = control.parent as FormGroup<OrderItemFormGroup> | null;
          const productId = group?.controls.productId.value;
          const quantity = control.value as number;
          if (!productId || !quantity) return of(null as ValidationErrors | null);

          return this.inventoryApi.getProduct(productId).pipe(
            map((product) =>
              quantity > product.availableQuantity
                ? { insufficientStock: { available: product.availableQuantity } }
                : null,
            ),
            catchError(() => of(null as ValidationErrors | null)),
          );
        }),
      );
  }
}
