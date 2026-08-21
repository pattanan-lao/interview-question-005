import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Location, DatePipe } from '@angular/common';
import { QueueApiService, TicketResponse } from '../../core/queue-api.service';

@Component({
  selector: 'app-show-ticket',
  imports: [DatePipe],
  templateUrl: './show-ticket.html',
  styleUrl: './show-ticket.css',
})
export class ShowTicket implements OnInit {
  private readonly queueApi = inject(QueueApiService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  protected readonly ticket = signal<TicketResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const state = this.location.getState() as { ticket?: TicketResponse };

    if (state?.ticket) {
      this.ticket.set(state.ticket);
      return;
    }

    this.queueApi.getCurrent().subscribe({
      next: (current) => this.ticket.set(current),
      error: () => this.errorMessage.set('เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง'),
    });
  }

  protected onBack(): void {
    this.router.navigate(['/']);
  }
}
