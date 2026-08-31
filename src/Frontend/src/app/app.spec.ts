import { describe, it, expect, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { OrderSignalrService } from './core/services/order-signalr.service';

describe('App', () => {
  async function setup() {
    const connect = vi.fn();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), { provide: OrderSignalrService, useValue: { connect } }],
    }).compileComponents();
    return { connect };
  }

  it('should create the app', async () => {
    await setup();
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the toolbar title', async () => {
    await setup();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.app-title')?.textContent).toContain('Order Processing System');
  });

  it('connects to the SignalR hub on init', async () => {
    const { connect } = await setup();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(connect).toHaveBeenCalledOnce();
  });
});
