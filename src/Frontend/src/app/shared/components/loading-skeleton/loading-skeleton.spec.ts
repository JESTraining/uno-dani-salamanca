import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoadingSkeleton } from './loading-skeleton';

describe('LoadingSkeleton', () => {
  let fixture: ComponentFixture<LoadingSkeleton>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [LoadingSkeleton] }).compileComponents();
    fixture = TestBed.createComponent(LoadingSkeleton);
  });

  it('renders the default number of rows', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.skeleton__row').length).toBe(3);
  });

  it('renders a custom number of rows', () => {
    fixture.componentRef.setInput('rows', 5);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.skeleton__row').length).toBe(5);
  });
});
