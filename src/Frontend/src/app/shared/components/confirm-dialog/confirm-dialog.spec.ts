import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ConfirmDialog, ConfirmDialogData } from './confirm-dialog';

describe('ConfirmDialog', () => {
  let fixture: ComponentFixture<ConfirmDialog>;
  let dialogRef: { close: ReturnType<typeof vi.fn> };

  async function setup(data: ConfirmDialogData): Promise<void> {
    dialogRef = { close: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [ConfirmDialog],
      providers: [
        provideNoopAnimations(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ConfirmDialog);
    fixture.detectChanges();
  }

  it('renders the title and message from the injected data', async () => {
    await setup({ title: 'Cancel order', message: 'Are you sure?' });
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Cancel order');
    expect(text).toContain('Are you sure?');
  });

  it('falls back to default button labels when none are provided', async () => {
    await setup({ title: 'Cancel order', message: 'Are you sure?' });
    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons[0].textContent.trim()).toBe('Cancel');
    expect(buttons[1].textContent.trim()).toBe('Confirm');
  });

  it('uses the custom labels when provided', async () => {
    await setup({ title: 'Cancel order', message: 'Are you sure?', confirmLabel: 'Yes, cancel', cancelLabel: 'Go back' });
    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons[0].textContent.trim()).toBe('Go back');
    expect(buttons[1].textContent.trim()).toBe('Yes, cancel');
  });

  it('closes the dialog with true on confirm()', async () => {
    await setup({ title: 'Cancel order', message: 'Are you sure?' });
    fixture.componentInstance.confirm();
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('closes the dialog with false on cancel()', async () => {
    await setup({ title: 'Cancel order', message: 'Are you sure?' });
    fixture.componentInstance.cancel();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });

  it('clicking the confirm button closes the dialog with true', async () => {
    await setup({ title: 'Cancel order', message: 'Are you sure?' });
    const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    buttons[1].click();
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('clicking the cancel button closes the dialog with false', async () => {
    await setup({ title: 'Cancel order', message: 'Are you sure?' });
    const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    buttons[0].click();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });
});
