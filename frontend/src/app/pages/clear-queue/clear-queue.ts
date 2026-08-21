import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { QueueApiService, TicketResponse } from '../../core/queue-api.service';

@Component({
  selector: 'app-clear-queue',
  imports: [],
  templateUrl: './clear-queue.html',
  styleUrl: './clear-queue.css',
})
export class ClearQueue implements OnInit {
  private readonly queueApi = inject(QueueApiService);
  private readonly router = inject(Router);

  protected readonly ticket = signal<TicketResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.queueApi.getCurrent().subscribe({
      next: (current) => this.ticket.set(current),
      error: () => this.errorMessage.set('เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง'),
    });
  }

  protected onClearQueue(): void {
    this.errorMessage.set(null);
    this.queueApi.clearQueue().subscribe({
      next: (current) => this.ticket.set(current),
      error: () => this.errorMessage.set('เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง'),
    });
  }

  protected onBack(): void {
    this.router.navigate(['/']);
  }
}
