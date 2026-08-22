import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  showError(message: string): void {
    this.snackBar.open(message, 'Dismiss', { duration: 6000, panelClass: 'notification-error' });
  }

  showSuccess(message: string): void {
    this.snackBar.open(message, undefined, { duration: 3000, panelClass: 'notification-success' });
  }
}
