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

  ngOnInit(): void {
    this.queueApi.getCurrent().subscribe((current) => this.ticket.set(current));
  }

  protected onClearQueue(): void {
    this.queueApi.clearQueue().subscribe((current) => this.ticket.set(current));
  }

  protected onBack(): void {
    this.router.navigate(['/']);
  }
}
