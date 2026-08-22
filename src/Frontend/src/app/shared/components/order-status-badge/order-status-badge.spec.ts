import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OrderStatusBadge } from './order-status-badge';

describe('OrderStatusBadge', () => {
  let fixture: ComponentFixture<OrderStatusBadge>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [OrderStatusBadge] }).compileComponents();
    fixture = TestBed.createComponent(OrderStatusBadge);
  });

  it('renders the status text', () => {
    fixture.componentRef.setInput('status', 'Completed');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Completed');
  });

  it('applies the success tone class for Completed', () => {
    fixture.componentRef.setInput('status', 'Completed');
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge.className).toContain('status-badge--success');
  });

  it('applies the error tone class for PaymentFailed', () => {
    fixture.componentRef.setInput('status', 'PaymentFailed');
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge.className).toContain('status-badge--error');
  });

  it('applies the progress tone class for PaymentProcessing', () => {
    fixture.componentRef.setInput('status', 'PaymentProcessing');
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge.className).toContain('status-badge--progress');
  });
});
